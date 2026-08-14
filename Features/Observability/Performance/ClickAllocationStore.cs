namespace ClickIt.Features.Observability.Performance;

// Rolling per-stage allocation inside the click pipeline, windowed for last/avg/max bytes-per-run per stage.
internal sealed class ClickAllocationStore
{
    private readonly Lock _lock = new();
    private readonly AllocationSampleWindow _context = new();
    private readonly AllocationSampleWindow _acquire = new();
    private readonly AllocationSampleWindow _rank = new();
    private readonly AllocationSampleWindow _execute = new();
    private readonly AllocationSampleWindow _post = new();
    private readonly AllocationSampleWindow _other = new();
    private readonly AllocationSampleWindow _altar = new();
    private readonly StageTimingSamples _contextTime = new();
    private readonly StageTimingSamples _acquireTime = new();
    private readonly StageTimingSamples _rankTime = new();
    private readonly StageTimingSamples _executeTime = new();
    private readonly StageTimingSamples _postTime = new();
    private readonly StageTimingSamples _altarTime = new();
    private readonly StageTimingSamples _otherTime = new();
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
            _altar.Record(breakdown.AltarBytes);
            _contextTime.Record(breakdown.ContextMs);
            _acquireTime.Record(breakdown.AcquireMs);
            _rankTime.Record(breakdown.RankMs);
            _executeTime.Record(breakdown.ExecuteMs);
            _postTime.Record(breakdown.PostMs);
            _altarTime.Record(breakdown.AltarMs);
            _otherTime.Record(breakdown.OtherMs);
            _sampleCount++;
        }
    }

    internal ClickAllocationStats GetStats()
    {
        lock (_lock)
        {
            return new ClickAllocationStats(
                ToStage(_context.Stats),
                ToStage(_acquire.Stats),
                ToStage(_rank.Stats),
                ToStage(_execute.Stats),
                ToStage(_post.Stats),
                ToStage(_other.Stats),
                _sampleCount,
                _contextTime.Stats,
                _acquireTime.Stats,
                _rankTime.Stats,
                _executeTime.Stats,
                _postTime.Stats,
                ToStage(_altar.Stats),
                _altarTime.Stats,
                _otherTime.Stats);
        }
    }

    private static AllocationStageSnapshot ToStage(AllocationSampleStats s)
        => new(s.LastBytesPerRun, s.AvgBytesPerRun, s.MaxBytesPerRun, s.MaxAllocPerSecond);
}
