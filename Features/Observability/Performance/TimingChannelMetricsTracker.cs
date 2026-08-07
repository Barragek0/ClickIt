namespace ClickIt.Features.Observability.Performance
{
    internal sealed class TimingChannelMetricsTracker
    {
        // One rolling window per channel: max is computed over the full 100-sample window, average
        // over the most recent 100 samples, last is simply the most recent sample.
        private const int MaxWindow = 1000;
        private const int AverageWindow = 100;

        private readonly Stopwatch _renderTimer = new();
        private readonly Stopwatch _altarCoroutineTimer = new();
        private readonly Stopwatch _clickCoroutineTimer = new();
        private readonly Stopwatch _flareCoroutineTimer = new();
        private readonly Stopwatch _blightCoroutineTimer = new();
        private readonly Stopwatch _ultimatumCoroutineTimer = new();
        private readonly Stopwatch _labelOverlayCoroutineTimer = new();

        private readonly Queue<long> _clickCoroutineTimings = new(MaxWindow);
        private readonly Queue<long> _altarCoroutineTimings = new(MaxWindow);
        private readonly Queue<long> _flareCoroutineTimings = new(MaxWindow);
        private readonly Queue<long> _blightCoroutineTimings = new(MaxWindow);
        private readonly Queue<long> _ultimatumCoroutineTimings = new(MaxWindow);
        private readonly Queue<long> _labelOverlayCoroutineTimings = new(MaxWindow);
        private readonly Queue<double> _renderTimings = new(MaxWindow);
        private readonly Queue<long> _successfulClickTimings = new(AverageWindow);

        private readonly object _clickTimingsLock = new();
        private readonly object _altarTimingsLock = new();
        private readonly object _flareTimingsLock = new();
        private readonly object _blightTimingsLock = new();
        private readonly object _ultimatumTimingsLock = new();
        private readonly object _labelOverlayTimingsLock = new();
        private readonly object _renderTimingsLock = new();
        private readonly object _successfulClickTimingsLock = new();

        private long _lastAltarTiming;
        private long _lastClickTiming;
        private long _lastFlareTiming;
        private long _lastBlightTiming;
        private long _lastUltimatumTiming;
        private long _lastLabelOverlayTiming;
        private double _lastRenderTiming;

        // Run period per channel: wall-clock interval between consecutive Stop calls, used to
        // normalize coroutine cost to duty-cycle % and per-frame ms (coroutines run at different
        // cadences, so raw per-run averages are not comparable to the render table).
        private readonly Queue<long> _altarPeriods = new(AverageWindow);
        private readonly Queue<long> _clickPeriods = new(AverageWindow);
        private readonly Queue<long> _flarePeriods = new(AverageWindow);
        private readonly Queue<long> _blightPeriods = new(AverageWindow);
        private readonly Queue<long> _ultimatumPeriods = new(AverageWindow);
        private readonly Queue<long> _labelOverlayPeriods = new(AverageWindow);

        private long _lastAltarStopTimestampMs;
        private long _lastClickStopTimestampMs;
        private long _lastFlareStopTimestampMs;
        private long _lastBlightStopTimestampMs;
        private long _lastUltimatumStopTimestampMs;
        private long _lastLabelOverlayStopTimestampMs;

        public Queue<double> GetRenderTimingsSnapshot()
        {
            lock (_renderTimingsLock)
            {
                return new Queue<double>(_renderTimings);
            }
        }

        public (double LastMs, double AverageMs, double MaxMs, int SampleCount) GetRenderTimingStats()
        {
            lock (_renderTimingsLock)
            {
                if (_renderTimings.Count == 0)
                {
                    return (0, 0, 0, 0);
                }

                double last = 0;
                double max = double.MinValue;
                int count = 0;

                foreach (double timing in _renderTimings)
                {
                    last = timing;
                    if (timing > max)
                    {
                        max = timing;
                    }
                    count++;
                }

                return (last, CalculateAverage(_renderTimings, AverageWindow), max, count);
            }
        }

        public void StartRenderTiming()
        {
            _renderTimer.Restart();
        }

        public void StopRenderTiming()
        {
            _renderTimer.Stop();
            double timing = _renderTimer.Elapsed.TotalMilliseconds;
            _lastRenderTiming = timing;
            EnqueueTiming(_renderTimings, timing, MaxWindow, _renderTimingsLock);
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
                case TimingChannel.Blight:
                    _blightCoroutineTimer.Restart();
                    break;
                case TimingChannel.Ultimatum:
                    _ultimatumCoroutineTimer.Restart();
                    break;
                case TimingChannel.LabelOverlay:
                    _labelOverlayCoroutineTimer.Restart();
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
                    EnqueueTiming(_altarCoroutineTimings, altarTiming, MaxWindow, _altarTimingsLock);
                    RecordPeriod(ref _lastAltarStopTimestampMs, _altarPeriods, _altarTimingsLock);
                    break;
                case TimingChannel.Click:
                    _clickCoroutineTimer.Stop();
                    long clickTiming = _clickCoroutineTimer.ElapsedMilliseconds;
                    _lastClickTiming = clickTiming;
                    EnqueueTiming(_clickCoroutineTimings, clickTiming, MaxWindow, _clickTimingsLock);
                    RecordPeriod(ref _lastClickStopTimestampMs, _clickPeriods, _clickTimingsLock);
                    break;
                case TimingChannel.Flare:
                    _flareCoroutineTimer.Stop();
                    long flareTiming = _flareCoroutineTimer.ElapsedMilliseconds;
                    _lastFlareTiming = flareTiming;
                    EnqueueTiming(_flareCoroutineTimings, flareTiming, MaxWindow, _flareTimingsLock);
                    RecordPeriod(ref _lastFlareStopTimestampMs, _flarePeriods, _flareTimingsLock);
                    break;
                case TimingChannel.Blight:
                    _blightCoroutineTimer.Stop();
                    long blightTiming = _blightCoroutineTimer.ElapsedMilliseconds;
                    _lastBlightTiming = blightTiming;
                    EnqueueTiming(_blightCoroutineTimings, blightTiming, MaxWindow, _blightTimingsLock);
                    RecordPeriod(ref _lastBlightStopTimestampMs, _blightPeriods, _blightTimingsLock);
                    break;
                case TimingChannel.Ultimatum:
                    _ultimatumCoroutineTimer.Stop();
                    long ultimatumTiming = _ultimatumCoroutineTimer.ElapsedMilliseconds;
                    _lastUltimatumTiming = ultimatumTiming;
                    EnqueueTiming(_ultimatumCoroutineTimings, ultimatumTiming, MaxWindow, _ultimatumTimingsLock);
                    RecordPeriod(ref _lastUltimatumStopTimestampMs, _ultimatumPeriods, _ultimatumTimingsLock);
                    break;
                case TimingChannel.LabelOverlay:
                    _labelOverlayCoroutineTimer.Stop();
                    long labelOverlayTiming = _labelOverlayCoroutineTimer.ElapsedMilliseconds;
                    _lastLabelOverlayTiming = labelOverlayTiming;
                    EnqueueTiming(_labelOverlayCoroutineTimings, labelOverlayTiming, MaxWindow, _labelOverlayTimingsLock);
                    RecordPeriod(ref _lastLabelOverlayStopTimestampMs, _labelOverlayPeriods, _labelOverlayTimingsLock);
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
                TimingChannel.Blight => _lastBlightTiming,
                TimingChannel.Ultimatum => _lastUltimatumTiming,
                TimingChannel.LabelOverlay => _lastLabelOverlayTiming,
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
            if (channel == TimingChannel.Render)
            {
                lock (_renderTimingsLock)
                {
                    return CalculateAverage(_renderTimings, AverageWindow);
                }
            }

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
                case TimingChannel.Blight:
                    queue = _blightCoroutineTimings;
                    lockObject = _blightTimingsLock;
                    break;
                case TimingChannel.Ultimatum:
                    queue = _ultimatumCoroutineTimings;
                    lockObject = _ultimatumTimingsLock;
                    break;
                case TimingChannel.LabelOverlay:
                    queue = _labelOverlayCoroutineTimings;
                    lockObject = _labelOverlayTimingsLock;
                    break;
                case TimingChannel.Unknown:
                default:
                    return 0;
            }

            lock (lockObject)
            {
                return CalculateAverage(queue, AverageWindow);
            }
        }

        public double GetAverageTiming(string timingType)
        {
            return GetAverageTiming(MapTimingChannel(timingType));
        }

        public void RecordSuccessfulClickTiming(long duration)
        {
            EnqueueTiming(_successfulClickTimings, duration, AverageWindow, _successfulClickTimingsLock);
        }

        public double GetAverageSuccessfulClickTiming()
        {
            lock (_successfulClickTimingsLock)
            {
                return CalculateAverage(_successfulClickTimings, AverageWindow);
            }
        }

        public double GetMaxTiming(TimingChannel channel)
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
                case TimingChannel.Blight:
                    queue = _blightCoroutineTimings;
                    lockObject = _blightTimingsLock;
                    break;
                case TimingChannel.Ultimatum:
                    queue = _ultimatumCoroutineTimings;
                    lockObject = _ultimatumTimingsLock;
                    break;
                case TimingChannel.LabelOverlay:
                    queue = _labelOverlayCoroutineTimings;
                    lockObject = _labelOverlayTimingsLock;
                    break;
                case TimingChannel.Unknown:
                case TimingChannel.Render:
                default:
                    return 0;
            }

            lock (lockObject)
            {
                return CalculateMax(queue);
            }
        }

        public double GetMaxTiming(string timingType)
        {
            return GetMaxTiming(MapTimingChannel(timingType));
        }

        public double GetAveragePeriod(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Altar => GetQueueAverage(_altarPeriods, _altarTimingsLock),
                TimingChannel.Click => GetQueueAverage(_clickPeriods, _clickTimingsLock),
                TimingChannel.Flare => GetQueueAverage(_flarePeriods, _flareTimingsLock),
                TimingChannel.Blight => GetQueueAverage(_blightPeriods, _blightTimingsLock),
                TimingChannel.Ultimatum => GetQueueAverage(_ultimatumPeriods, _ultimatumTimingsLock),
                TimingChannel.LabelOverlay => GetQueueAverage(_labelOverlayPeriods, _labelOverlayTimingsLock),
                TimingChannel.Render => 0,
                TimingChannel.Unknown => 0,
                _ => 0,
            };
        }

        public double GetAveragePeriod(string timingType)
        {
            return GetAveragePeriod(MapTimingChannel(timingType));
        }

        public int GetTimingSampleCount(TimingChannel channel)
        {
            return channel switch
            {
                TimingChannel.Click => GetQueueCount(_clickCoroutineTimings, _clickTimingsLock),
                TimingChannel.Altar => GetQueueCount(_altarCoroutineTimings, _altarTimingsLock),
                TimingChannel.Flare => GetQueueCount(_flareCoroutineTimings, _flareTimingsLock),
                TimingChannel.Blight => GetQueueCount(_blightCoroutineTimings, _blightTimingsLock),
                TimingChannel.Ultimatum => GetQueueCount(_ultimatumCoroutineTimings, _ultimatumTimingsLock),
                TimingChannel.LabelOverlay => GetQueueCount(_labelOverlayCoroutineTimings, _labelOverlayTimingsLock),
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
            _blightCoroutineTimer.Stop();
            _ultimatumCoroutineTimer.Stop();
            _labelOverlayCoroutineTimer.Stop();

            lock (_clickTimingsLock)
                _clickCoroutineTimings.Clear();
            lock (_altarTimingsLock)
                _altarCoroutineTimings.Clear();
            lock (_flareTimingsLock)
                _flareCoroutineTimings.Clear();
            lock (_blightTimingsLock)
                _blightCoroutineTimings.Clear();
            lock (_ultimatumTimingsLock)
                _ultimatumCoroutineTimings.Clear();
            lock (_labelOverlayTimingsLock)
                _labelOverlayCoroutineTimings.Clear();
            lock (_renderTimingsLock)
                _renderTimings.Clear();
            lock (_successfulClickTimingsLock)
                _successfulClickTimings.Clear();

            _lastAltarTiming = 0;
            _lastClickTiming = 0;
            _lastFlareTiming = 0;
            _lastBlightTiming = 0;
            _lastUltimatumTiming = 0;
            _lastLabelOverlayTiming = 0;
            _lastRenderTiming = 0;

            lock (_altarTimingsLock)
                _altarPeriods.Clear();
            lock (_clickTimingsLock)
                _clickPeriods.Clear();
            lock (_flareTimingsLock)
                _flarePeriods.Clear();
            lock (_blightTimingsLock)
                _blightPeriods.Clear();
            lock (_ultimatumTimingsLock)
                _ultimatumPeriods.Clear();
            lock (_labelOverlayTimingsLock)
                _labelOverlayPeriods.Clear();
            _lastAltarStopTimestampMs = 0;
            _lastClickStopTimestampMs = 0;
            _lastFlareStopTimestampMs = 0;
            _lastBlightStopTimestampMs = 0;
            _lastUltimatumStopTimestampMs = 0;
            _lastLabelOverlayStopTimestampMs = 0;
        }

        private static void RecordPeriod(ref long lastStopTimestampMs, Queue<long> periods, object lockObject)
        {
            long now = Environment.TickCount64;
            if (lastStopTimestampMs != 0)
                EnqueueTiming(periods, now - lastStopTimestampMs, AverageWindow, lockObject);
            lastStopTimestampMs = now;
        }

        private static double GetQueueAverage(Queue<long> queue, object lockObject)
        {
            lock (lockObject)
            {
                return CalculateAverage(queue, AverageWindow);
            }
        }

        private static TimingChannel MapTimingChannel(string? timingType)
        {
            return timingType switch
            {
                "click" => TimingChannel.Click,
                "altar" => TimingChannel.Altar,
                "flare" => TimingChannel.Flare,
                "blight" => TimingChannel.Blight,
                "ultimatum" => TimingChannel.Ultimatum,
                "labeloverlay" => TimingChannel.LabelOverlay,
                "render" => TimingChannel.Render,
                _ => TimingChannel.Unknown,
            };
        }

        private static void EnqueueTiming<T>(Queue<T> queue, T value, int maxLength, object lockObject)
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

        internal static long CalculateMax(Queue<long> queue)
        {
            long max = 0;
            foreach (long value in queue)
            {
                if (value > max)
                    max = value;
            }
            return max;
        }

        private static double CalculateAverage(Queue<long> queue, int recentWindow)
        {
            int count = queue.Count;
            if (count == 0)
                return 0;

            int take = SystemMath.Min(count, recentWindow);
            int skip = count - take;
            long sum = 0;
            int i = 0;
            foreach (long value in queue)
            {
                if (i >= skip)
                    sum += value;
                i++;
            }

            return (double)sum / take;
        }

        private static double CalculateAverage(Queue<double> queue, int recentWindow)
        {
            int count = queue.Count;
            if (count == 0)
                return 0;

            int take = SystemMath.Min(count, recentWindow);
            int skip = count - take;
            double sum = 0;
            int i = 0;
            foreach (double value in queue)
            {
                if (i >= skip)
                    sum += value;
                i++;
            }

            return sum / take;
        }

        private static int GetQueueCount<T>(Queue<T> queue, object lockObject)
        {
            lock (lockObject)
            {
                return queue.Count;
            }
        }
    }
}