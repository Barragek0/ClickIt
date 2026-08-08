namespace ClickIt.Features.Observability.Performance
{
    // Rolling per-section allocation pressure: bytes allocated per run (GC.GetAllocatedBytesForCurrentThread
    // delta across a processing boundary) plus the wall-clock period between runs, so the debug table can
    // show a steady-state alloc/s rate alongside the per-run bytes. Locked because the label-scan boundary
    // can run on any coroutine that touches CachedLabels.Value.
    internal sealed class GcAllocationMetricsStore
    {
        private const int MaxWindow = 1000;

        private readonly Lock _lock = new();
        private readonly Dictionary<ProcessingSection, SectionSamples> _samples = [];

        internal void Record(ProcessingSection section, long bytes)
        {
            if (section == ProcessingSection.Unknown || bytes < 0)
                return;

            lock (_lock)
            {
                if (!_samples.TryGetValue(section, out SectionSamples? samples))
                {
                    samples = new SectionSamples();
                    _samples[section] = samples;
                }
                samples.Record(bytes);
            }
        }

        internal GcAllocationSnapshot GetStats(ProcessingSection section)
        {
            lock (_lock)
            {
                return _samples.TryGetValue(section, out SectionSamples? samples)
                    ? samples.Stats
                    : default;
            }
        }

        private sealed class SectionSamples
        {
            private const long ExpiryMs = 30_000;

            private readonly Queue<(long TimestampMs, long Bytes)> _samples = new(MaxWindow);
            private readonly Func<long> _now = static () => Environment.TickCount64;

            public void Record(long bytes)
            {
                long now = _now();
                Expire(now);
                _samples.Enqueue((now, bytes));
                if (_samples.Count > MaxWindow)
                    _samples.Dequeue();
            }

            public GcAllocationSnapshot Stats
            {
                get
                {
                    long now = _now();
                    Expire(now);
                    if (_samples.Count == 0)
                        return default;

                    long total = 0;
                    long max = 0;
                    long last = 0;
                    long periodTotal = 0;
                    int periodCount = 0;
                    long? previousTimestamp = null;
                    foreach ((long timestampMs, long bytes) in _samples)
                    {
                        total += bytes;
                        if (bytes > max)
                            max = bytes;
                        last = bytes;
                        if (previousTimestamp is long previous)
                        {
                            periodTotal += SystemMath.Max(1, timestampMs - previous);
                            periodCount++;
                        }
                        previousTimestamp = timestampMs;
                    }
                    double avgBytes = total / (double)_samples.Count;
                    double avgPeriodMs = periodCount > 0 ? periodTotal / (double)periodCount : 0;
                    double allocPerSecond = avgPeriodMs > 0 ? avgBytes * 1000.0 / avgPeriodMs : 0;
                    return new GcAllocationSnapshot(allocPerSecond, avgBytes, max, _samples.Count, last, avgPeriodMs);
                }
            }

            private void Expire(long now)
            {
                while (_samples.Count > 0 && now - _samples.Peek().TimestampMs > ExpiryMs)
                    _samples.Dequeue();
            }
        }
    }
}
