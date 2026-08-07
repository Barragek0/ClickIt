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
            private readonly Queue<long> _bytes = new(MaxWindow);
            private readonly Queue<long> _periods = new(MaxWindow);
            private long _lastTimestampMs;
            private long _lastBytes;
            private bool _hasPrevious;

            public void Record(long bytes)
            {
                long now = Environment.TickCount64;
                if (_hasPrevious)
                {
                    long period = SystemMath.Max(1, now - _lastTimestampMs);
                    _periods.Enqueue(period);
                    if (_periods.Count > MaxWindow)
                        _periods.Dequeue();
                }
                _lastTimestampMs = now;
                _lastBytes = bytes;
                _hasPrevious = true;

                _bytes.Enqueue(bytes);
                if (_bytes.Count > MaxWindow)
                    _bytes.Dequeue();
            }

            public GcAllocationSnapshot Stats
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
                    double avgBytes = total / (double)_bytes.Count;

                    double avgPeriodMs = 0;
                    if (_periods.Count > 0)
                    {
                        long periodTotal = 0;
                        foreach (long p in _periods)
                            periodTotal += p;
                        avgPeriodMs = periodTotal / (double)_periods.Count;
                    }

                    double allocPerSecond = avgPeriodMs > 0 ? avgBytes * 1000.0 / avgPeriodMs : 0;
                    return new GcAllocationSnapshot(allocPerSecond, avgBytes, max, _bytes.Count, _lastBytes, avgPeriodMs);
                }
            }
        }
    }
}
