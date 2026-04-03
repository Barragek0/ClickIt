namespace ClickIt.Features.Observability.Performance
{
    internal sealed class ClickActivityTracker
    {
        private readonly Queue<long> _clickIntervals = new(10);
        private readonly object _clickIntervalsLock = new();
        private long _lastClickTime;
        private int _clickCount;

        internal int ClickCount
        {
            get => _clickCount;
            set => _clickCount = value;
        }

        internal void RecordClickInterval(long currentTimeMs)
        {
            _clickCount++;
            if (_lastClickTime != 0 && _clickCount > 3)
            {
                long interval = currentTimeMs - _lastClickTime;
                if (interval > 0 && interval < 10000)
                {
                    lock (_clickIntervalsLock)
                    {
                        _clickIntervals.Enqueue(interval);
                        if (_clickIntervals.Count > 10)
                            _clickIntervals.Dequeue();
                    }
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
            => _clickCount = 0;

        internal void Clear()
        {
            _lastClickTime = 0;
            _clickCount = 0;
            lock (_clickIntervalsLock)
                _clickIntervals.Clear();
        }
    }
}