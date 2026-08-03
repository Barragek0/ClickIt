namespace ClickIt.Features.Click.Selection;

internal sealed class BlightChestTransitionSuppression
{
    private const long SuppressionDurationMs = 2000;
    private const int MaxTrackedEntities = 256;

    // Entity address -> earliest time the chest may be clicked again (TickCount64).
    private readonly Dictionary<long, long> _noClickUntilMs = [];

    // Entity addresses that already had their false -> true transition. The blacklist is armed once
    // on that edge and never again, so these never re-arm.
    private readonly HashSet<long> _everTransitioned = [];

    internal bool ShouldSuppressBlightChestClick(LabelOnGround? label)
        => ShouldSuppressBlightChestClick(label, Environment.TickCount64);

    internal bool ShouldSuppressBlightChestClick(LabelOnGround? label, long now)
    {
        if (label == null)
            return false;

        if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
            || rawItem is not Entity item)
            return false;

        if (!DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
            || !BlightChestDebug.IsBlightChestPath(path))
            return false;

        bool isTransitioned = DynamicAccess.TryReadBool(item, static current => current.IsTransitioned, out bool value) && value;

        long key = DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Address, out object? rawAddress)
                   && rawAddress is long address
            ? address
            : 0;


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
