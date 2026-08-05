namespace ClickIt.Features.Observability.Performance
{
    internal sealed class TimingChannelMetricsTracker
    {
        private readonly Stopwatch _renderTimer = new();
        private readonly Stopwatch _altarCoroutineTimer = new();
        private readonly Stopwatch _clickCoroutineTimer = new();
        private readonly Stopwatch _flareCoroutineTimer = new();
        private readonly Stopwatch _blightCoroutineTimer = new();
        private readonly Stopwatch _ultimatumCoroutineTimer = new();
        private readonly Stopwatch _labelOverlayCoroutineTimer = new();

        private readonly Queue<long> _clickCoroutineTimings = new(10);
        private readonly Queue<long> _altarCoroutineTimings = new(10);
        private readonly Queue<long> _flareCoroutineTimings = new(10);
        private readonly Queue<long> _blightCoroutineTimings = new(10);
        private readonly Queue<long> _ultimatumCoroutineTimings = new(10);
        private readonly Queue<long> _labelOverlayCoroutineTimings = new(10);
        private readonly Queue<long> _renderTimings = new(60);
        private readonly Queue<long> _successfulClickTimings = new(10);

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
        private long _lastRenderTiming;

        private long _maxAltarTiming;
        private long _maxClickTiming;
        private long _maxFlareTiming;
        private long _maxBlightTiming;
        private long _maxUltimatumTiming;
        private long _maxLabelOverlayTiming;

        // Run period per channel: wall-clock interval between consecutive Stop calls, used to
        // normalize coroutine cost to duty-cycle % and per-frame ms (coroutines run at different
        // cadences, so raw per-run averages are not comparable to the render table).
        private readonly Queue<long> _altarPeriods = new(10);
        private readonly Queue<long> _clickPeriods = new(10);
        private readonly Queue<long> _flarePeriods = new(10);
        private readonly Queue<long> _blightPeriods = new(10);
        private readonly Queue<long> _ultimatumPeriods = new(10);
        private readonly Queue<long> _labelOverlayPeriods = new(10);

        private long _lastAltarStopTimestampMs;
        private long _lastClickStopTimestampMs;
        private long _lastFlareStopTimestampMs;
        private long _lastBlightStopTimestampMs;
        private long _lastUltimatumStopTimestampMs;
        private long _lastLabelOverlayStopTimestampMs;

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
                    _maxAltarTiming = SystemMath.Max(_maxAltarTiming, altarTiming);
                    EnqueueTiming(_altarCoroutineTimings, altarTiming, 10, _altarTimingsLock);
                    RecordPeriod(ref _lastAltarStopTimestampMs, _altarPeriods, _altarTimingsLock);
                    break;
                case TimingChannel.Click:
                    _clickCoroutineTimer.Stop();
                    long clickTiming = _clickCoroutineTimer.ElapsedMilliseconds;
                    _lastClickTiming = clickTiming;
                    _maxClickTiming = SystemMath.Max(_maxClickTiming, clickTiming);
                    EnqueueTiming(_clickCoroutineTimings, clickTiming, 10, _clickTimingsLock);
                    RecordPeriod(ref _lastClickStopTimestampMs, _clickPeriods, _clickTimingsLock);
                    break;
                case TimingChannel.Flare:
                    _flareCoroutineTimer.Stop();
                    long flareTiming = _flareCoroutineTimer.ElapsedMilliseconds;
                    _lastFlareTiming = flareTiming;
                    _maxFlareTiming = SystemMath.Max(_maxFlareTiming, flareTiming);
                    EnqueueTiming(_flareCoroutineTimings, flareTiming, 10, _flareTimingsLock);
                    RecordPeriod(ref _lastFlareStopTimestampMs, _flarePeriods, _flareTimingsLock);
                    break;
                case TimingChannel.Blight:
                    _blightCoroutineTimer.Stop();
                    long blightTiming = _blightCoroutineTimer.ElapsedMilliseconds;
                    _lastBlightTiming = blightTiming;
                    _maxBlightTiming = SystemMath.Max(_maxBlightTiming, blightTiming);
                    EnqueueTiming(_blightCoroutineTimings, blightTiming, 10, _blightTimingsLock);
                    RecordPeriod(ref _lastBlightStopTimestampMs, _blightPeriods, _blightTimingsLock);
                    break;
                case TimingChannel.Ultimatum:
                    _ultimatumCoroutineTimer.Stop();
                    long ultimatumTiming = _ultimatumCoroutineTimer.ElapsedMilliseconds;
                    _lastUltimatumTiming = ultimatumTiming;
                    _maxUltimatumTiming = SystemMath.Max(_maxUltimatumTiming, ultimatumTiming);
                    EnqueueTiming(_ultimatumCoroutineTimings, ultimatumTiming, 10, _ultimatumTimingsLock);
                    RecordPeriod(ref _lastUltimatumStopTimestampMs, _ultimatumPeriods, _ultimatumTimingsLock);
                    break;
                case TimingChannel.LabelOverlay:
                    _labelOverlayCoroutineTimer.Stop();
                    long labelOverlayTiming = _labelOverlayCoroutineTimer.ElapsedMilliseconds;
                    _lastLabelOverlayTiming = labelOverlayTiming;
                    _maxLabelOverlayTiming = SystemMath.Max(_maxLabelOverlayTiming, labelOverlayTiming);
                    EnqueueTiming(_labelOverlayCoroutineTimings, labelOverlayTiming, 10, _labelOverlayTimingsLock);
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
                TimingChannel.Blight => _maxBlightTiming,
                TimingChannel.Ultimatum => _maxUltimatumTiming,
                TimingChannel.LabelOverlay => _maxLabelOverlayTiming,
                TimingChannel.Unknown => 0,
                TimingChannel.Render => 0,
                _ => 0,
            };
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
            _maxAltarTiming = 0;
            _maxClickTiming = 0;
            _maxFlareTiming = 0;
            _maxBlightTiming = 0;
            _maxUltimatumTiming = 0;
            _maxLabelOverlayTiming = 0;

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
                EnqueueTiming(periods, now - lastStopTimestampMs, 10, lockObject);
            lastStopTimestampMs = now;
        }

        private static double GetQueueAverage(Queue<long> queue, object lockObject)
        {
            lock (lockObject)
            {
                return CalculateAverage(queue);
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