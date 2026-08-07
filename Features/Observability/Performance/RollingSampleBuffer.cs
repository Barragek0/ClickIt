namespace ClickIt.Features.Observability.Performance
{
    // Bounded rolling window shared by the render and processing metric stores: max reflects the
    // full 100-sample window, average the most recent 100 samples, last the most recent sample.
    // Locked because label-scan processing can be recorded from any coroutine that touches
    // CachedLabels.Value, unlike render sections which are render-thread only.
    internal sealed class RollingSampleBuffer
    {
        private const int MaxWindow = 1000;
        private const int AverageWindow = 100;

        private readonly Queue<double> _samples = new(MaxWindow);
        private readonly object _lock = new();
        private double _last;

        public (double LastMs, double AverageMs, double MaxMs, long SampleCount) Stats
        {
            get
            {
                lock (_lock)
                {
                    if (_samples.Count == 0)
                        return (0, 0, 0, 0);
                    double max = double.MinValue;
                    foreach (double value in _samples)
                    {
                        if (value > max)
                            max = value;
                    }
                    return (_last, AverageOfLast(_samples, AverageWindow), max, _samples.Count);
                }
            }
        }

        public void Record(double ms)
        {
            lock (_lock)
            {
                _last = ms;
                _samples.Enqueue(ms);
                if (_samples.Count > MaxWindow)
                    _samples.Dequeue();
            }
        }

        private static double AverageOfLast(Queue<double> samples, int recentWindow)
        {
            int count = samples.Count;
            int take = SystemMath.Min(count, recentWindow);
            int skip = count - take;
            double sum = 0;
            int i = 0;
            foreach (double value in samples)
            {
                if (i >= skip)
                    sum += value;
                i++;
            }
            return sum / take;
        }
    }
}
