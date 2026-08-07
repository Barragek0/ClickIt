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
                for (int i = 0; i < groundLabels.Count && validLabels.Count < 1000; i++)
                {
                    LabelOnGround label = groundLabels[i];

                    if (ClickableLabelPolicy.IsBasicLabelValid(label))
                    {
                        validLabels.Add(label);
                    }
                }
                long validityBytes = GC.GetAllocatedBytesForCurrentThread() - validityStart;

                long sortStart = GC.GetAllocatedBytesForCurrentThread();
                LabelGeometry.SortLabelsByDistance(validLabels);
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
    }
}