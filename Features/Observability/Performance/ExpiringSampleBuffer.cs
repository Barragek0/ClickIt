namespace ClickIt.Features.Observability.Performance
{
    // Time-window sample buffer: every sample carries a timestamp and expires 30 seconds after it
    // was recorded (a sliding window — each result is removed individually, oldest first, so a
    // section that stops recording drains to zero instead of showing a stale all-time value).
    // Last/Average/Max are computed over the live (non-expired) samples only. The count cap is a
    // safety bound against pathological recording rates. Locked because the label-scan boundary can
    // be recorded from any coroutine.
    internal sealed class ExpiringSampleBuffer
    {
        internal const int DefaultMaxSamples = 1000;
        internal const long DefaultExpiryMs = 30_000;

        private readonly Queue<(long TimestampMs, double Value)> _samples = new(DefaultMaxSamples);
        private readonly object _lock = new();
        private readonly Func<long> _now;
        private readonly long _expiryMs;
        private readonly int _maxSamples;

        private double _last;

        internal ExpiringSampleBuffer(long expiryMs = DefaultExpiryMs, int maxSamples = DefaultMaxSamples, Func<long>? nowProvider = null)
        {
            _expiryMs = expiryMs;
            _maxSamples = maxSamples;
            _now = nowProvider ?? (static () => Environment.TickCount64);
        }

        internal void Record(double value)
        {
            long now = _now();
            lock (_lock)
            {
                Expire(now);
                _last = value;
                _samples.Enqueue((now, value));
                if (_samples.Count > _maxSamples)
                    _samples.Dequeue();
            }
        }

        internal (double Last, double Average, double Max, long SampleCount) Stats
        {
            get
            {
                long now = _now();
                lock (_lock)
                {
                    Expire(now);
                    if (_samples.Count == 0)
                        return (0, 0, 0, 0);
                    double sum = 0;
                    double max = double.MinValue;
                    foreach ((_, double value) in _samples)
                    {
                        sum += value;
                        if (value > max)
                            max = value;
                    }
                    return (_last, sum / _samples.Count, max, _samples.Count);
                }
            }
        }

        internal long LiveSampleCount()
        {
            long now = _now();
            lock (_lock)
            {
                Expire(now);
                return _samples.Count;
            }
        }

        internal double[] ValuesSnapshot()
        {
            long now = _now();
            lock (_lock)
            {
                Expire(now);
                double[] values = new double[_samples.Count];
                int i = 0;
                foreach ((_, double value) in _samples)
                    values[i++] = value;
                return values;
            }
        }

        internal void Clear()
        {
            lock (_lock)
            {
                _samples.Clear();
                _last = 0;
            }
        }

        private void Expire(long now)
        {
            while (_samples.Count > 0 && now - _samples.Peek().TimestampMs > _expiryMs)
                _samples.Dequeue();
            if (_samples.Count == 0)
                _last = 0;
        }
    }
}
