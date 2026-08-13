namespace ClickIt.Features.Observability.Performance;

// Rolling per-stage allocation inside the label scan (windowed like GcAllocationMetricsStore so the debug tables can show last/avg/max bytes-per-run per stage). The label-scan boundary can run on any coroutine that touches CachedLabels.Value, so reads are locked.
internal sealed class LabelScanAllocationStore
{
    private readonly Lock _lock = new();
    private readonly AllocationSampleWindow _listRead = new();
    private readonly AllocationSampleWindow _listAlloc = new();
    private readonly AllocationSampleWindow _validity = new();
    private readonly AllocationSampleWindow _sort = new();
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
                ToStage(_listRead.Stats),
                ToStage(_listAlloc.Stats),
                ToStage(_validity.Stats),
                ToStage(_sort.Stats),
                _sampleCount);
        }
    }

    private static AllocationStageSnapshot ToStage(AllocationSampleStats s)
        => new(s.LastBytesPerRun, s.AvgBytesPerRun, s.MaxBytesPerRun, s.MaxAllocPerSecond);
}
