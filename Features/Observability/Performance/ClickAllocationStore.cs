namespace ClickIt.Features.Observability.Performance;

// Rolling per-stage allocation inside the click pipeline (windowed like LabelScanAllocationStore so
// the debug tables can show last/avg/max bytes-per-run per stage). The click pipeline runs on the
// plugin loop thread, but reads are locked for the same safety as the other stores.
internal sealed class ClickAllocationStore
{
    private const int MaxWindow = 1000;

    private readonly Lock _lock = new();
    private readonly StageSamples _context = new();
    private readonly StageSamples _acquire = new();
    private readonly StageSamples _rank = new();
    private readonly StageSamples _execute = new();
    private readonly StageSamples _post = new();
    private readonly StageSamples _other = new();
    private readonly StageTimingSamples _contextTime = new();
    private readonly StageTimingSamples _acquireTime = new();
    private readonly StageTimingSamples _rankTime = new();
    private readonly StageTimingSamples _executeTime = new();
    private readonly StageTimingSamples _postTime = new();
    private long _sampleCount;

    internal void Record(ClickAllocationBreakdown breakdown)
    {
        lock (_lock)
        {
            _context.Record(breakdown.ContextBytes);
            _acquire.Record(breakdown.AcquireBytes);
            _rank.Record(breakdown.RankBytes);
            _execute.Record(breakdown.ExecuteBytes);
            _post.Record(breakdown.PostBytes);
            _other.Record(breakdown.OtherBytes);
            _contextTime.Record(breakdown.ContextMs);
            _acquireTime.Record(breakdown.AcquireMs);
            _rankTime.Record(breakdown.RankMs);
            _executeTime.Record(breakdown.ExecuteMs);
            _postTime.Record(breakdown.PostMs);
            _sampleCount++;
        }
    }

    internal ClickAllocationStats GetStats()
    {
        lock (_lock)
        {
            return new ClickAllocationStats(
                _context.Stats,
                _acquire.Stats,
                _rank.Stats,
                _execute.Stats,
                _post.Stats,
                _other.Stats,
                _sampleCount,
                _contextTime.Stats,
                _acquireTime.Stats,
                _rankTime.Stats,
                _executeTime.Stats,
                _postTime.Stats);
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

    private sealed class StageTimingSamples
    {
        private readonly Queue<double> _millis = new(MaxWindow);
        private double _last;

        public void Record(double ms)
        {
            if (ms < 0)
                ms = 0;
            _last = ms;
            _millis.Enqueue(ms);
            if (_millis.Count > MaxWindow)
                _millis.Dequeue();
        }

        public TimingStageSnapshot Stats
        {
            get
            {
                if (_millis.Count == 0)
                    return default;
                double total = 0;
                double max = 0;
                foreach (double ms in _millis)
                {
                    total += ms;
                    if (ms > max)
                        max = ms;
                }
                return new TimingStageSnapshot(_last, total / _millis.Count, max);
            }
        }
    }
}
