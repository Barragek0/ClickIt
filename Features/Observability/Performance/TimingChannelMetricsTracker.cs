namespace ClickIt.Features.Observability.Performance
{
    internal sealed class TimingChannelMetricsTracker
    {
        private readonly Stopwatch _renderTimer = new();
        private readonly Stopwatch _altarCoroutineTimer = new();
        private readonly Stopwatch _clickCoroutineTimer = new();
        private readonly Stopwatch _flareCoroutineTimer = new();

        private readonly Queue<long> _clickCoroutineTimings = new(10);
        private readonly Queue<long> _altarCoroutineTimings = new(10);
        private readonly Queue<long> _flareCoroutineTimings = new(10);
        private readonly Queue<long> _renderTimings = new(60);
        private readonly Queue<long> _successfulClickTimings = new(10);

        private readonly object _clickTimingsLock = new();
        private readonly object _altarTimingsLock = new();
        private readonly object _flareTimingsLock = new();
        private readonly object _renderTimingsLock = new();
        private readonly object _successfulClickTimingsLock = new();

        private long _lastAltarTiming;
        private long _lastClickTiming;
        private long _lastFlareTiming;
        private long _lastRenderTiming;

        private long _maxAltarTiming;
        private long _maxClickTiming;
        private long _maxFlareTiming;

        public Queue<long> GetRenderTimingsSnapshot()
        {
            lock (_renderTimingsLock)
            {
                return new Queue<long>(_renderTimings);
            }
        }

        public (long LastMs, double AverageMs, long MaxMs, int SampleCount) GetRenderTimingStats()
        {
            lock (_renderTimingsLock)
            {
                if (_renderTimings.Count == 0)
                {
                    return (0, 0, 0, 0);
                }

                long last = 0;
                long sum = 0;
                long max = long.MinValue;
                int count = 0;

                foreach (long timing in _renderTimings)
                {
                    last = timing;
                    sum += timing;
                    if (timing > max)
                    {
                        max = timing;
                    }
                    count++;
                }

                double average = count > 0 ? (double)sum / count : 0;
                return (last, average, max, count);
            }
        }

        public void StartRenderTiming()
        {
            _renderTimer.Restart();
        }

        public void StopRenderTiming()
        {
            _renderTimer.Stop();
            long timing = _renderTimer.ElapsedMilliseconds;
            _lastRenderTiming = timing;
            EnqueueTiming(_renderTimings, timing, 60, _renderTimingsLock);
        }

        public void StartCoroutineTiming(TimingChannel channel)
        {
            switch (channel)
            {
                case TimingChannel.Altar:
                    _altarCoroutineTimer.Restart();
                    break;
                case TimingChannel.Click:
                    _clickCoroutineTimer.Restart();
                    break;
                case TimingChannel.Flare:
                    _flareCoroutineTimer.Restart();
                    break;
                case TimingChannel.Unknown:
                case TimingChannel.Render:
                default:
                    break;
            }
        }

        public void StartCoroutineTiming(string coroutineName)
        {
            StartCoroutineTiming(MapTimingChannel(coroutineName));
        }

        public void StopCoroutineTiming(TimingChannel channel)
        {
            switch (channel)
            {
                case TimingChannel.Altar:
                    _altarCoroutineTimer.Stop();
                    long altarTiming = _altarCoroutineTimer.ElapsedMilliseconds;
                    _lastAltarTiming = altarTiming;
                    _maxAltarTiming = SystemMath.Max(_maxAltarTiming, altarTiming);
                    EnqueueTiming(_altarCoroutineTimings, altarTiming, 10, _altarTimingsLock);
                    break;
                case TimingChannel.Click:
                    _clickCoroutineTimer.Stop();
                    long clickTiming = _clickCoroutineTimer.ElapsedMilliseconds;
                    _lastClickTiming = clickTiming;
                    _maxClickTiming = SystemMath.Max(_maxClickTiming, clickTiming);
                    EnqueueTiming(_clickCoroutineTimings, clickTiming, 10, _clickTimingsLock);
                    break;
                case TimingChannel.Flare:
                    _flareCoroutineTimer.Stop();
                    long flareTiming = _flareCoroutineTimer.ElapsedMilliseconds;
                    _lastFlareTiming = flareTiming;
                    _maxFlareTiming = SystemMath.Max(_maxFlareTiming, flareTiming);
                    EnqueueTiming(_flareCoroutineTimings, flareTiming, 10, _flareTimingsLock);
                    break;
                case TimingChannel.Unknown:
                case TimingChannel.Render:
                default:
                    break;
            }
        }

        public void StopCoroutineTiming(string coroutineName)
        {
            StopCoroutineTiming(MapTimingChannel(coroutineName));
        }

        public double GetLastTiming(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Click => _lastClickTiming,
                TimingChannel.Altar => _lastAltarTiming,
                TimingChannel.Flare => _lastFlareTiming,
                TimingChannel.Render => _lastRenderTiming,
                TimingChannel.Unknown => 0,
                _ => 0,
            };
        }

        public double GetLastTiming(string timingType)
        {
            return GetLastTiming(MapTimingChannel(timingType));
        }

        public double GetAverageTiming(TimingChannel channel)
        {
            Queue<long> queue;
            object lockObject;

            switch (channel)
            {
                case TimingChannel.Click:
                    queue = _clickCoroutineTimings;
                    lockObject = _clickTimingsLock;
                    break;
                case TimingChannel.Altar:
                    queue = _altarCoroutineTimings;
                    lockObject = _altarTimingsLock;
                    break;
                case TimingChannel.Flare:
                    queue = _flareCoroutineTimings;
                    lockObject = _flareTimingsLock;
                    break;
                case TimingChannel.Render:
                    queue = _renderTimings;
                    lockObject = _renderTimingsLock;
                    break;
                case TimingChannel.Unknown:
                default:
                    return 0;
            }

            lock (lockObject)
            {
                return CalculateAverage(queue);
            }
        }

        public double GetAverageTiming(string timingType)
        {
            return GetAverageTiming(MapTimingChannel(timingType));
        }

        public void RecordSuccessfulClickTiming(long duration)
        {
            EnqueueTiming(_successfulClickTimings, duration, 10, _successfulClickTimingsLock);
        }

        public double GetAverageSuccessfulClickTiming()
        {
            lock (_successfulClickTimingsLock)
            {
                return CalculateAverage(_successfulClickTimings);
            }
        }

        public double GetMaxTiming(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Click => _maxClickTiming,
                TimingChannel.Altar => _maxAltarTiming,
                TimingChannel.Flare => _maxFlareTiming,
                TimingChannel.Unknown => 0,
                TimingChannel.Render => 0,
                _ => 0,
            };
        }

        public double GetMaxTiming(string timingType)
        {
            return GetMaxTiming(MapTimingChannel(timingType));
        }

        public int GetTimingSampleCount(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Click => GetQueueCount(_clickCoroutineTimings, _clickTimingsLock),
                TimingChannel.Altar => GetQueueCount(_altarCoroutineTimings, _altarTimingsLock),
                TimingChannel.Flare => GetQueueCount(_flareCoroutineTimings, _flareTimingsLock),
                TimingChannel.Render => GetQueueCount(_renderTimings, _renderTimingsLock),
                TimingChannel.Unknown => 0,
                _ => 0,
            };
        }

        public void Clear()
        {
            _renderTimer.Stop();
            _altarCoroutineTimer.Stop();
            _clickCoroutineTimer.Stop();
            _flareCoroutineTimer.Stop();

            lock (_clickTimingsLock)
                _clickCoroutineTimings.Clear();
            lock (_altarTimingsLock)
                _altarCoroutineTimings.Clear();
            lock (_flareTimingsLock)
                _flareCoroutineTimings.Clear();
            lock (_renderTimingsLock)
                _renderTimings.Clear();
            lock (_successfulClickTimingsLock)
                _successfulClickTimings.Clear();

            _lastAltarTiming = 0;
            _lastClickTiming = 0;
            _lastFlareTiming = 0;
            _lastRenderTiming = 0;
            _maxAltarTiming = 0;
            _maxClickTiming = 0;
            _maxFlareTiming = 0;
        }

        private static TimingChannel MapTimingChannel(string? timingType)
        {
            return timingType switch
            {
                "click" => TimingChannel.Click,
                "altar" => TimingChannel.Altar,
                "flare" => TimingChannel.Flare,
                "render" => TimingChannel.Render,
                _ => TimingChannel.Unknown,
            };
        }

        private static void EnqueueTiming(Queue<long> queue, long value, int maxLength, object lockObject)
        {
            lock (lockObject)
            {
                queue.Enqueue(value);
                if (queue.Count > maxLength)
                {
                    queue.Dequeue();
                }
            }
        }

        private static double CalculateAverage(Queue<long> queue)
        {
            int count = queue.Count;
            if (count == 0)
                return 0;

            long sum = 0;
            foreach (long value in queue)
            {
                sum += value;
            }

            return (double)sum / count;
        }

        private static int GetQueueCount(Queue<long> queue, object lockObject)
        {
            lock (lockObject)
            {
                return queue.Count;
            }
        }
    }
}