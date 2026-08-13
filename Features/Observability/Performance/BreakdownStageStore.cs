namespace ClickIt.Features.Observability.Performance;

// Records one per-run sample for every stage of a feature's processing (allocation bytes + wall time) so the debug tables can show exactly which sub-step of an area costs time/allocation. Stages are recorded together per run so each stage's window period matches the parent section.
internal sealed class BreakdownStageStore
{
    private readonly string[] _stageNames;
    private readonly AllocationSampleWindow[] _bytes;
    private readonly StageTimingSamples[]? _times;
    private readonly Lock _lock = new();
    private long _sampleCount;

    internal BreakdownStageStore(bool trackTiming, params string[] stageNames)
    {
        _stageNames = stageNames;
        _bytes = new AllocationSampleWindow[stageNames.Length];
        for (int i = 0; i < _bytes.Length; i++)
            _bytes[i] = new AllocationSampleWindow();
        if (trackTiming)
        {
            _times = new StageTimingSamples[stageNames.Length];
            for (int i = 0; i < _times.Length; i++)
                _times[i] = new StageTimingSamples();
        }
    }

    internal void Record(ReadOnlySpan<long> stageBytes, ReadOnlySpan<double> stageMs)
    {
        lock (_lock)
        {
            for (int i = 0; i < _bytes.Length && i < stageBytes.Length; i++)
                _bytes[i].Record(stageBytes[i]);
            if (_times != null)
            {
                for (int i = 0; i < _times.Length && i < stageMs.Length; i++)
                    _times[i].Record(stageMs[i]);
            }
            _sampleCount++;
        }
    }

    // Records only one named stage (e.g. the Blight executor, which runs on a different thread and cadence than the refresh stages it belongs with) without zero-filling the sibling stages.
    internal void RecordStage(int stageIndex, long bytes, double ms)
    {
        lock (_lock)
        {
            if (stageIndex < 0 || stageIndex >= _bytes.Length)
                return;
            _bytes[stageIndex].Record(bytes);
            if (_times != null)
                _times[stageIndex].Record(ms);
            _sampleCount++;
        }
    }

    internal BreakdownStats GetStats()
    {
        lock (_lock)
        {
            BreakdownStageSnapshot[] stages = new BreakdownStageSnapshot[_stageNames.Length];
            for (int i = 0; i < _stageNames.Length; i++)
            {
                AllocationSampleStats a = _bytes[i].Stats;
                stages[i] = new BreakdownStageSnapshot(
                    _stageNames[i],
                    new AllocationStageSnapshot(a.LastBytesPerRun, a.AvgBytesPerRun, a.MaxBytesPerRun, a.MaxAllocPerSecond),
                    _times != null ? _times[i].Stats : default);
            }
            return new BreakdownStats(stages, _sampleCount);
        }
    }
}

// Synchronous only (no async/iterator capture): ref-struct spans cannot cross those boundaries, and the stage buffers are either stackalloc (plain methods) or a reusable per-thread array (iterators).
public delegate void BreakdownRecorder(ReadOnlySpan<long> stageBytes, ReadOnlySpan<double> stageMs);

// Rolling per-stage wall-clock window (last/avg/max ms) with the same 10-second expiry and 50-sample average as the parent timing stores, so stage maxes never outlive the parent's window.
internal sealed class StageTimingSamples
{
    private readonly ExpiringSampleBuffer _samples = new();

    internal void Record(double ms)
        => _samples.Record(ms);

    internal TimingStageSnapshot Stats
    {
        get
        {
            (double last, double average, double max, long _) = _samples.Stats;
            return new TimingStageSnapshot(last, average, max);
        }
    }
}
