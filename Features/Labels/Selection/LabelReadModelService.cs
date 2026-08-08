namespace ClickIt.Features.Labels.Selection
{
    public sealed class LabelReadModelService
    {
        private readonly GameController _gameController;
        private readonly Func<Vector2, bool> _pointIsInClickableArea;
        private readonly Action<double>? _recordProcessingMs;
        private readonly Action<long>? _recordAllocationBytes;
        private readonly Action<LabelScanAllocationBreakdown>? _recordAllocationBreakdown;

        public TimeCache<List<LabelOnGround>> CachedLabels { get; }

        // Per-label scan data keyed on the label ADDRESS (stable across snapshots): the basic
        // validity check is ~6 DLR reads and the sort re-reads DistancePlayer per label, so a
        // 50ms cadence would re-run those reads every scan. Within the window the scan reuses the
        // cached result; the game's own visible-list rebuild (removed labels vanish from the list)
        // bounds staleness, and downstream consumers re-validate before acting.
        private readonly record struct CachedLabelData(bool Valid, float Distance, long AtMs);
        private readonly Dictionary<long, CachedLabelData> _labelDataCache = [];
        private long _labelDataCacheWindowStartMs;
        private const long LabelDataCacheWindowMs = 250;

        public LabelReadModelService(GameController gameController, Func<Vector2, bool> pointIsInClickableArea, Action<double>? recordProcessingMs = null, Action<long>? recordAllocationBytes = null, Action<LabelScanAllocationBreakdown>? recordAllocationBreakdown = null)
        {
            _gameController = gameController;
            _pointIsInClickableArea = pointIsInClickableArea;
            _recordProcessingMs = recordProcessingMs;
            _recordAllocationBytes = recordAllocationBytes;
            _recordAllocationBreakdown = recordAllocationBreakdown;
            CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, 50);
        }

        public bool GroundItemsVisible()
        {
            return CachedLabels?.Value?.Count > 0;
        }

        public List<LabelOnGround> UpdateLabelComponent()
        {
            long start = Stopwatch.GetTimestamp();
            long allocStart = GC.GetAllocatedBytesForCurrentThread();
            LabelScanAllocationBreakdown breakdown = default;
            try
            {
                long readStart = GC.GetAllocatedBytesForCurrentThread();
                IList<LabelOnGround>? groundLabels = _gameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible;
                long readBytes = GC.GetAllocatedBytesForCurrentThread() - readStart;

                if (groundLabels == null || groundLabels.Count == 0)
                {
                    breakdown = new LabelScanAllocationBreakdown(readBytes, 0, 0, 0, readBytes);
                    return [];
                }

                long listAllocStart = GC.GetAllocatedBytesForCurrentThread();
                List<LabelOnGround> validLabels = new(SystemMath.Min(groundLabels.Count, 1000));
                long listAllocBytes = GC.GetAllocatedBytesForCurrentThread() - listAllocStart;

                long validityStart = GC.GetAllocatedBytesForCurrentThread();
                long now = Environment.TickCount64;
                if (now - _labelDataCacheWindowStartMs >= LabelDataCacheWindowMs)
                {
                    _labelDataCache.Clear();
                    _labelDataCacheWindowStartMs = now;
                }

                for (int i = 0; i < groundLabels.Count && validLabels.Count < 1000; i++)
                {
                    LabelOnGround label = groundLabels[i];
                    long address = label.Address;

                    if (_labelDataCache.TryGetValue(address, out CachedLabelData cached)
                        && now - cached.AtMs < LabelDataCacheWindowMs)
                    {
                        if (cached.Valid)
                            validLabels.Add(label);
                        continue;
                    }

                    bool isValid = ClickableLabelPolicy.IsBasicLabelValid(label);
                    float distance = ResolveLabelDistance(label);
                    _labelDataCache[address] = new CachedLabelData(isValid, distance, now);
                    if (isValid)
                        validLabels.Add(label);
                }
                long validityBytes = GC.GetAllocatedBytesForCurrentThread() - validityStart;

                long sortStart = GC.GetAllocatedBytesForCurrentThread();
                LabelGeometry.SortLabelsByDistance(validLabels, ResolveCachedLabelDistance);
                long sortBytes = GC.GetAllocatedBytesForCurrentThread() - sortStart;

                long totalBytes = readBytes + listAllocBytes + validityBytes + sortBytes;
                breakdown = new LabelScanAllocationBreakdown(readBytes, listAllocBytes, validityBytes, sortBytes, totalBytes);

                return validLabels;
            }
            finally
            {
                _recordProcessingMs?.Invoke((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                _recordAllocationBytes?.Invoke(GC.GetAllocatedBytesForCurrentThread() - allocStart);
                _recordAllocationBreakdown?.Invoke(breakdown);
            }
        }

        private static float ResolveLabelDistance(LabelOnGround label)
        {
            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                || !DynamicAccess.TryReadFloat(rawItem, DynamicAccessProfiles.DistancePlayer, out float distance))
                return float.MaxValue;

            return distance;
        }

        // Every valid label was just cached by the scan loop above, so this is a dictionary hit per
        // label; the fallback keeps the sort safe if a label was added by another path.
        private float ResolveCachedLabelDistance(LabelOnGround label)
        {
            if (_labelDataCache.TryGetValue(label.Address, out CachedLabelData cached))
                return cached.Distance;

            return ResolveLabelDistance(label);
        }
    }
}