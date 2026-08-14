namespace ClickIt.Features.Observability.Performance;

// Rolling deltas between consecutive marks of the same periodic event (click run, blight refresh, label scan, blocked-UI refresh, ultimatum poll, flare poll), so the perf tables show the ACTUAL observed cadence of background work instead of the configured target. Deltas expire on the same 10s window as the other perf buffers and average over the recent samples. Uses a high-resolution clock so TickCount64's ~15ms resolution cannot inflate short cadences. A gap above the stale floor (or 5x the kind's recent average, whichever is larger) means the event STOPPED (hotkey released, feature disabled), so the next mark starts a fresh session and is not counted as a cadence sample.
internal sealed class IntervalTimingStore
{
    private const long StaleIntervalFloorMs = 1000;
    private const double StaleIntervalRelativeMultiplier = 5.0;

    private readonly object _lock = new();
    private readonly Dictionary<IntervalKind, ExpiringSampleBuffer> _buffers = [];
    private readonly Dictionary<IntervalKind, long> _lastMarkMs = [];
    private readonly Func<long> _now;

    internal IntervalTimingStore(Func<long>? nowProvider = null)
        => _now = nowProvider ?? (() => (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency));

    internal void Mark(IntervalKind kind)
    {
        long now = _now();
        lock (_lock)
        {
            if (_lastMarkMs.TryGetValue(kind, out long last) && last > 0)
            {
                long delta = now - last;
                if (delta >= 0)
                {
                    ExpiringSampleBuffer buffer = GetBuffer(kind);
                    (_, double avg, _, _) = buffer.Stats;
                    if (delta <= SystemMath.Max(StaleIntervalFloorMs, avg * StaleIntervalRelativeMultiplier))
                        buffer.Record(delta);
                }
            }
            _lastMarkMs[kind] = now;
        }
    }

    internal IReadOnlyDictionary<IntervalKind, IntervalTimingSnapshot> GetSnapshots()
    {
        lock (_lock)
        {
            Dictionary<IntervalKind, IntervalTimingSnapshot> result = [];
            foreach ((IntervalKind kind, ExpiringSampleBuffer buffer) in _buffers)
            {
                (double last, double avg, double max, long count) = buffer.Stats;
                if (count > 0)
                    result[kind] = new IntervalTimingSnapshot(last, avg, max, count);
            }
            return result;
        }
    }

    private ExpiringSampleBuffer GetBuffer(IntervalKind kind)
    {
        if (!_buffers.TryGetValue(kind, out ExpiringSampleBuffer? buffer))
        {
            buffer = new ExpiringSampleBuffer(expiryMs: 10_000, maxSamples: 200, averageSamples: 50);
            _buffers[kind] = buffer;
        }
        return buffer;
    }
}
