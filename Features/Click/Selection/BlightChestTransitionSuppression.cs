namespace ClickIt.Features.Click.Selection;

internal sealed class BlightChestTransitionSuppression(Func<long?>? getAreaHash = null)
{
    private const long SuppressionDurationMs = 2000;
    private const int MaxTrackedEntities = 256;

    // Entity address -> earliest time the chest may be clicked again (TickCount64).
    private readonly Dictionary<long, long> _noClickUntilMs = [];

    // Keep the first-transition history for the current Blight area even after its timers expire.
    private readonly HashSet<long> _everTransitioned = [];
    private long? _areaHash;

    internal bool ShouldSuppressBlightChestClick(LabelOnGround? label, string? entityPath = null)
        => ShouldSuppressBlightChestClick(label, Environment.TickCount64, entityPath);

    internal bool ShouldSuppressBlightChestClick(LabelOnGround? label, long now, string? entityPath = null)
    {
        if (label == null)
            return false;

        if (!DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item))
            return false;

        if (entityPath != null)
        {
            if (!BlightChestDebug.IsBlightChestPath(entityPath))
                return false;
        }
        else
        {
            if (!DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
                || !BlightChestDebug.IsBlightChestPath(path))
                return false;
        }

        if (getAreaHash?.Invoke() is { } areaHash && areaHash != _areaHash)
        {
            _areaHash = areaHash;
            _everTransitioned.Clear();
            _noClickUntilMs.Clear();
        }

        bool isTransitioned = DynamicAccess.TryReadBool(item, static current => current.IsTransitioned, out bool value) && value;

        long key = DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Address, out object? rawAddress)
                   && rawAddress is long address
            ? address
            : 0;

        // Fail open when the address cannot be read: key 0 is shared by every unreadable chest, so suppressing on it would cross-suppress all unreadable chests for SuppressionDurationMs.
        if (key == 0)
            return false;

        if (isTransitioned && _everTransitioned.Add(key))
        {
            _noClickUntilMs[key] = now + SuppressionDurationMs;
            if (_noClickUntilMs.Count > MaxTrackedEntities)
                PruneStaleEntries(now);
            return true;
        }

        if (_noClickUntilMs.TryGetValue(key, out long noClickUntil))
        {
            if (now < noClickUntil)
                return true;

            _noClickUntilMs.Remove(key);
        }

        if (_noClickUntilMs.Count > MaxTrackedEntities)
            PruneStaleEntries(now);

        return false;
    }

    private void PruneStaleEntries(long now)
    {
        foreach (long key in _noClickUntilMs.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
            _noClickUntilMs.Remove(key);

        while (_noClickUntilMs.Count > MaxTrackedEntities)
        {
            long oldestKey = -1;
            long oldestUntil = long.MaxValue;
            foreach (KeyValuePair<long, long> entry in _noClickUntilMs)
            {
                if (entry.Value < oldestUntil)
                {
                    oldestUntil = entry.Value;
                    oldestKey = entry.Key;
                }
            }
            if (oldestKey < 0)
                break;
            _noClickUntilMs.Remove(oldestKey);
        }
    }
}
