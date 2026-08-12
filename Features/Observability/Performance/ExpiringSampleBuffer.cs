namespace ClickIt.Features.Observability.Performance
{
    // Time-window sample buffer: samples expire individually after a fixed window so a section that stops recording drains to zero. The average covers only the most recent samples (so the table reacts quickly); max keeps the full live window.
    internal sealed class ExpiringSampleBuffer
    {
        internal const int DefaultMaxSamples = 1000;
        internal const long DefaultExpiryMs = 10_000;
        internal const int DefaultAverageSamples = 50;

        private readonly Queue<(long TimestampMs, double Value)> _samples = new(DefaultMaxSamples);
        private readonly object _lock = new();
        private readonly Func<long> _now;
        private readonly long _expiryMs;
        private readonly int _maxSamples;
        private readonly int _averageSamples;

        private double _last;

        internal ExpiringSampleBuffer(long expiryMs = DefaultExpiryMs, int maxSamples = DefaultMaxSamples, Func<long>? nowProvider = null, int averageSamples = DefaultAverageSamples)
        {
            _expiryMs = expiryMs;
            _maxSamples = maxSamples;
            _now = nowProvider ?? (static () => Environment.TickCount64);
            _averageSamples = averageSamples;
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
                    long take = SystemMath.Min(_samples.Count, _averageSamples);
                    double sum = 0;
                    double max = double.MinValue;
                    long index = 0;
                    foreach ((_, double value) in _samples)
                    {
                        if (value > max)
                            max = value;
                        if (index >= _samples.Count - take)
                            sum += value;
                        index++;
                    }
                    return (_last, take > 0 ? sum / take : 0, max, _samples.Count);
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
