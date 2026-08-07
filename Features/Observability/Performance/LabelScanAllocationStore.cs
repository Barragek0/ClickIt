namespace ClickIt.Features.Observability.Performance;

// Rolling per-stage allocation inside the label scan (windowed like GcAllocationMetricsStore so the
// debug tables can show last/avg/max bytes-per-run per stage). The label-scan boundary can run on
// any coroutine that touches CachedLabels.Value, so reads are locked.
internal sealed class LabelScanAllocationStore
{
    private const int MaxWindow = 1000;

    private readonly Lock _lock = new();
    private readonly StageSamples _listRead = new();
    private readonly StageSamples _listAlloc = new();
    private readonly StageSamples _validity = new();
    private readonly StageSamples _sort = new();
    private long _sampleCount;

    internal void Record(LabelScanAllocationBreakdown breakdown)
    {
        lock (_lock)
        {
            _listRead.Record(breakdown.ListReadBytes);
            _listAlloc.Record(breakdown.ListAllocBytes);
            _validity.Record(breakdown.ValidityBytes);
            _sort.Record(breakdown.SortBytes);
            _sampleCount++;
        }
    }

    internal LabelScanAllocationStats GetStats()
    {
        lock (_lock)
        {
            return new LabelScanAllocationStats(
                _listRead.Stats,
                _listAlloc.Stats,
                _validity.Stats,
                _sort.Stats,
                _sampleCount);
        }
    }

    private sealed class StageSamples
    {
        private readonly Queue<long> _bytes = new(MaxWindow);
        private long _last;

        public void Record(long bytes)
        {
            if (bytes < 0)
                bytes = 0;
            _last = bytes;
            _bytes.Enqueue(bytes);
            if (_bytes.Count > MaxWindow)
                _bytes.Dequeue();
        }

        public AllocationStageSnapshot Stats
        {
            get
            {
                if (_bytes.Count == 0)
                    return default;
                long total = 0;
                long max = 0;
                foreach (long b in _bytes)
                {
                    total += b;
                    if (b > max)
                        max = b;
                }
                return new AllocationStageSnapshot(_last, total / (double)_bytes.Count, max);
            }
        }
    }
}
