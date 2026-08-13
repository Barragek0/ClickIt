namespace ClickIt.Features.Labels.Selection
{
    public sealed class LabelReadModelService
    {
        private readonly GameController _gameController;
        private readonly Action<double>? _recordProcessingMs;
        private readonly Action<long>? _recordAllocationBytes;
        private readonly Action<LabelScanAllocationBreakdown>? _recordAllocationBreakdown;

        public TimeCache<List<LabelOnGround>> CachedLabels { get; }

        // Per-label scan data keyed on the label ADDRESS so validity/sort reads only re-run on a long window.
        private readonly record struct CachedLabelData(bool Valid, float Distance, long AtMs);
        private readonly Dictionary<long, CachedLabelData> _labelDataCache = [];
        private long _labelDataCacheWindowStartMs;
        private const long LabelDataCacheWindowMs = 1000;

        // TimeCache.Value runs the scan on whichever thread first accesses it after expiry, so the shared cache must be serialized.
        private readonly Lock _scanLock = new();

        // Same-instance label set while the visible label addresses are unchanged (activates downstream ReferenceEquals-gated caches).
        private readonly StableLabelSetCache _stableSet = new();
        private readonly List<LabelOnGround> _emptyLabels = [];

        public LabelReadModelService(GameController gameController, Action<double>? recordProcessingMs = null, Action<long>? recordAllocationBytes = null, Action<LabelScanAllocationBreakdown>? recordAllocationBreakdown = null)
        {
            _gameController = gameController;
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
            lock (_scanLock)
            {
                using (new DlrReadScope(ProcessingSection.Label))
                    return UpdateLabelComponentCore();
            }
        }

        private List<LabelOnGround> UpdateLabelComponentCore()
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
                    _stableSet.Reset();
                    return _emptyLabels;
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

                // Same instance while the visible label set is unchanged so downstream caches that key on the list reference only re-run on real label-set changes.
                return _stableSet.Resolve(validLabels);
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

        // Every valid label was just cached by the scan loop above, so this is a dictionary hit per label; the fallback keeps the sort safe if a label was added by another path.
        private float ResolveCachedLabelDistance(LabelOnGround label)
        {
            if (_labelDataCache.TryGetValue(label.Address, out CachedLabelData cached))
                return cached.Distance;

            return ResolveLabelDistance(label);
        }
    }
}
