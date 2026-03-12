namespace ClickIt.Utils
{
    public partial class PerformanceMonitor
    {
        public void UpdateFPS()
        {
            _frameCount++;
            if (!_fpsTimer.IsRunning)
            {
                _fpsTimer.Start();
            }

            if (_fpsTimer.ElapsedMilliseconds >= 1000)
            {
                _currentFps = _frameCount / (_fpsTimer.ElapsedMilliseconds / 1000.0);
                _fpsSampleSum += _currentFps;
                _fpsSampleCount++;
                _maxFps = Math.Max(_maxFps, _currentFps);
                _frameCount = 0;
                _fpsTimer.Restart();
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
                case TimingChannel.Shrine:
                    _shrineCoroutineTimer.Restart();
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
                    _maxAltarTiming = Math.Max(_maxAltarTiming, altarTiming);
                    EnqueueTiming(_altarCoroutineTimings, altarTiming, 10, _altarTimingsLock);
                    break;
                case TimingChannel.Click:
                    _clickCoroutineTimer.Stop();
                    long clickTiming = _clickCoroutineTimer.ElapsedMilliseconds;
                    _lastClickTiming = clickTiming;
                    _maxClickTiming = Math.Max(_maxClickTiming, clickTiming);
                    EnqueueTiming(_clickCoroutineTimings, clickTiming, 10, _clickTimingsLock);
                    break;
                case TimingChannel.Flare:
                    _flareCoroutineTimer.Stop();
                    long flareTiming = _flareCoroutineTimer.ElapsedMilliseconds;
                    _lastFlareTiming = flareTiming;
                    _maxFlareTiming = Math.Max(_maxFlareTiming, flareTiming);
                    EnqueueTiming(_flareCoroutineTimings, flareTiming, 10, _flareTimingsLock);
                    break;
                case TimingChannel.Shrine:
                    _shrineCoroutineTimer.Stop();
                    long shrineTiming = _shrineCoroutineTimer.ElapsedMilliseconds;
                    _lastShrineTiming = shrineTiming;
                    _maxShrineTiming = Math.Max(_maxShrineTiming, shrineTiming);
                    EnqueueTiming(_shrineCoroutineTimings, shrineTiming, 10, _shrineTimingsLock);
                    break;
            }
        }

        public void StopCoroutineTiming(string coroutineName)
        {
            StopCoroutineTiming(MapTimingChannel(coroutineName));
        }

        public double GetLastTiming(TimingChannel channel)
        {
            switch (channel)
            {
                case TimingChannel.Click:
                    return _lastClickTiming;
                case TimingChannel.Altar:
                    return _lastAltarTiming;
                case TimingChannel.Flare:
                    return _lastFlareTiming;
                case TimingChannel.Shrine:
                    return _lastShrineTiming;
                case TimingChannel.Render:
                    return _lastRenderTiming;
                default:
                    return 0;
            }
        }

        public double GetLastTiming(string timingType)
        {
            return GetLastTiming(MapTimingChannel(timingType));
        }

        public double GetAverageTiming(TimingChannel channel)
        {
            Queue<long> queue;
            object lockObj;

            switch (channel)
            {
                case TimingChannel.Click:
                    queue = _clickCoroutineTimings;
                    lockObj = _clickTimingsLock;
                    break;
                case TimingChannel.Altar:
                    queue = _altarCoroutineTimings;
                    lockObj = _altarTimingsLock;
                    break;
                case TimingChannel.Flare:
                    queue = _flareCoroutineTimings;
                    lockObj = _flareTimingsLock;
                    break;
                case TimingChannel.Shrine:
                    queue = _shrineCoroutineTimings;
                    lockObj = _shrineTimingsLock;
                    break;
                case TimingChannel.Render:
                    queue = _renderTimings;
                    lockObj = _renderTimingsLock;
                    break;
                default:
                    return 0;
            }

            lock (lockObj)
            {
                return queue.Count > 0 ? queue.Average() : 0;
            }
        }

        public double GetAverageTiming(string timingType)
        {
            return GetAverageTiming(MapTimingChannel(timingType));
        }

        public double GetMaxTiming(TimingChannel channel)
        {
            switch (channel)
            {
                case TimingChannel.Click:
                    return _maxClickTiming;
                case TimingChannel.Altar:
                    return _maxAltarTiming;
                case TimingChannel.Flare:
                    return _maxFlareTiming;
                case TimingChannel.Shrine:
                    return _maxShrineTiming;
                default:
                    return 0;
            }
        }

        public double GetMaxTiming(string timingType)
        {
            return GetMaxTiming(MapTimingChannel(timingType));
        }

        private static TimingChannel MapTimingChannel(string? timingType)
        {
            switch (timingType)
            {
                case "click":
                    return TimingChannel.Click;
                case "altar":
                    return TimingChannel.Altar;
                case "flare":
                    return TimingChannel.Flare;
                case "shrine":
                    return TimingChannel.Shrine;
                case "render":
                    return TimingChannel.Render;
                default:
                    return TimingChannel.Unknown;
            }
        }

        // Helper to enqueue timing measurements and keep a fixed-length queue
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
    }
}
