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

        public void StartCoroutineTiming(string coroutineName)
        {
            switch (coroutineName)
            {
                case "altar":
                    _altarCoroutineTimer.Restart();
                    break;
                case "click":
                    _clickCoroutineTimer.Restart();
                    break;
                case "flare":
                    _flareCoroutineTimer.Restart();
                    break;
                case "shrine":
                    _shrineCoroutineTimer.Restart();
                    break;
            }
        }

        public void StopCoroutineTiming(string coroutineName)
        {
            switch (coroutineName)
            {
                case "altar":
                    _altarCoroutineTimer.Stop();
                    long altarTiming = _altarCoroutineTimer.ElapsedMilliseconds;
                    _lastAltarTiming = altarTiming;
                    _maxAltarTiming = Math.Max(_maxAltarTiming, altarTiming);
                    EnqueueTiming(_altarCoroutineTimings, altarTiming, 10, _altarTimingsLock);
                    break;
                case "click":
                    _clickCoroutineTimer.Stop();
                    long clickTiming = _clickCoroutineTimer.ElapsedMilliseconds;
                    _lastClickTiming = clickTiming;
                    _maxClickTiming = Math.Max(_maxClickTiming, clickTiming);
                    EnqueueTiming(_clickCoroutineTimings, clickTiming, 10, _clickTimingsLock);
                    break;
                case "flare":
                    _flareCoroutineTimer.Stop();
                    long flareTiming = _flareCoroutineTimer.ElapsedMilliseconds;
                    _lastFlareTiming = flareTiming;
                    _maxFlareTiming = Math.Max(_maxFlareTiming, flareTiming);
                    EnqueueTiming(_flareCoroutineTimings, flareTiming, 10, _flareTimingsLock);
                    break;
                case "shrine":
                    _shrineCoroutineTimer.Stop();
                    long shrineTiming = _shrineCoroutineTimer.ElapsedMilliseconds;
                    _lastShrineTiming = shrineTiming;
                    _maxShrineTiming = Math.Max(_maxShrineTiming, shrineTiming);
                    EnqueueTiming(_shrineCoroutineTimings, shrineTiming, 10, _shrineTimingsLock);
                    break;
            }
        }

        public double GetLastTiming(string timingType)
        {
            switch (timingType)
            {
                case "click":
                    return _lastClickTiming;
                case "altar":
                    return _lastAltarTiming;
                case "flare":
                    return _lastFlareTiming;
                case "shrine":
                    return _lastShrineTiming;
                case "render":
                    return _lastRenderTiming;
                default:
                    return 0;
            }
        }

        public double GetAverageTiming(string timingType)
        {
            Queue<long> queue;
            object lockObj;

            switch (timingType)
            {
                case "click":
                    queue = _clickCoroutineTimings;
                    lockObj = _clickTimingsLock;
                    break;
                case "altar":
                    queue = _altarCoroutineTimings;
                    lockObj = _altarTimingsLock;
                    break;
                case "flare":
                    queue = _flareCoroutineTimings;
                    lockObj = _flareTimingsLock;
                    break;
                case "shrine":
                    queue = _shrineCoroutineTimings;
                    lockObj = _shrineTimingsLock;
                    break;
                case "render":
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

        public double GetMaxTiming(string timingType)
        {
            switch (timingType)
            {
                case "click":
                    return _maxClickTiming;
                case "altar":
                    return _maxAltarTiming;
                case "flare":
                    return _maxFlareTiming;
                case "shrine":
                    return _maxShrineTiming;
                default:
                    return 0;
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
