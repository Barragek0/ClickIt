namespace ClickIt.Features.Observability.Performance
{
    internal sealed class ClickActivityTracker
    {
        private readonly Queue<long> _clickIntervals = new(10);
        private readonly Lock _clickIntervalsLock = new();
        private long _lastClickTime;

        internal int ClickCount { get; set; }

        internal void RecordClickInterval(long currentTimeMs)
        {
            ClickCount++;
            if (_lastClickTime != 0 && ClickCount > 3)
            {
                long interval = currentTimeMs - _lastClickTime;
                if (interval is > 0 and < 10000)
                    lock (_clickIntervalsLock)
                    {
                        _clickIntervals.Enqueue(interval);
                        if (_clickIntervals.Count > 10)
                            _clickIntervals.Dequeue();
                    }

            }

            _lastClickTime = currentTimeMs;
        }

        internal double GetAverageClickInterval()
        {
            lock (_clickIntervalsLock)
            {
                if (_clickIntervals.Count == 0)
                    return 0;

                long sum = 0;
                foreach (long value in _clickIntervals)
                    sum += value;

                return (double)sum / _clickIntervals.Count;
            }
        }

        internal void ResetClickCount()
            => ClickCount = 0;

        internal void Clear()
        {
            _lastClickTime = 0;
            ClickCount = 0;
            lock (_clickIntervalsLock)
                _clickIntervals.Clear();
        }
    }
}