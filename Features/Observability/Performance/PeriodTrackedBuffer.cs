namespace ClickIt.Features.Observability.Performance
{
    // Per-value processing buffer: rolling ms sample + inter-run period tracked under a lock (label-scan processing can be recorded from any coroutine that touches CachedClickIt.Features.ClickIt.Features.Labels.Value, unlike render sections which are render-thread only).
    internal sealed class PeriodTrackedBuffer
    {
        private readonly RollingSampleBuffer _samples = new();
        private readonly ExpiringSampleBuffer _periods = new();
        private readonly object _lock = new();
        private long _lastTimestampMs;
        private bool _hasPrevious;

        internal void Record(double ms)
        {
            lock (_lock)
            {
                long now = Environment.TickCount64;
                if (_hasPrevious)
                    _periods.Record(SystemMath.Max(1, now - _lastTimestampMs));
                _lastTimestampMs = now;
                _hasPrevious = true;
                _samples.Record(ms);
            }
        }

        internal (double LastMs, double AverageMs, double MaxMs, long SampleCount, double AveragePeriodMs) Stats
        {
            get
            {
                lock (_lock)
                {
                    (double last, double avg, double max, long count) = _samples.Stats;
                    return (last, avg, max, count, _periods.Stats.Average);
                }
            }
        }
    }
}
