namespace ClickIt.Features.Observability.Performance
{
    internal sealed class ClickActivityTracker
    {
        private readonly Queue<long> _clickIntervals = new(10);
        private readonly Lock _clickIntervalsLock = new();
        private long _lastClickTime;
        private int _clickCount;

        // Recorded from several coroutines (click/manual-hover/ultimatum/movement) while Reset runs on the main thread - volatile-backed so increments are never lost and reads are never torn.
        internal int ClickCount
        {
            get => Volatile.Read(ref _clickCount);
            set => Volatile.Write(ref _clickCount, value);
        }

        internal void RecordClickInterval(long currentTimeMs)
        {
            int count = Interlocked.Increment(ref _clickCount);
            if (_lastClickTime != 0 && count > 3)
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
            => Interlocked.Exchange(ref _clickCount, 0);

        internal void Clear()
        {
            _lastClickTime = 0;
            Interlocked.Exchange(ref _clickCount, 0);
            lock (_clickIntervalsLock)
                _clickIntervals.Clear();
        }
    }
}