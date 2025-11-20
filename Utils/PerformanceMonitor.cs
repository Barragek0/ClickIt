using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClickIt.Utils
{
    /// <summary>
    /// Handles all performance monitoring, timing, and FPS calculations for the ClickIt plugin.
    /// Provides thread-safe access to timing queues and performance metrics.
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly ClickItSettings _settings;

        public PerformanceMonitor(ClickItSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        // Primary timing controls
        private readonly Stopwatch _mainTimer = new Stopwatch();
        private readonly Stopwatch _secondTimer = new Stopwatch();

        // Dedicated click interval timer (resets between clicks)
        private readonly Stopwatch _clickIntervalTimer = new Stopwatch();

        // Coroutine timing
        private readonly Stopwatch _renderTimer = new Stopwatch();
        private readonly Stopwatch _altarCoroutineTimer = new Stopwatch();
        private readonly Stopwatch _clickCoroutineTimer = new Stopwatch();
        private readonly Stopwatch _delveFlareCoroutineTimer = new Stopwatch();
        private readonly Stopwatch _shrineCoroutineTimer = new Stopwatch();

        // Performance tracking queues with thread-safe access
        private readonly Queue<long> _clickCoroutineTimings = new Queue<long>(10);
        private readonly Queue<long> _altarCoroutineTimings = new Queue<long>(10);
        private readonly Queue<long> _delveFlareCoroutineTimings = new Queue<long>(10);
        private readonly Queue<long> _shrineCoroutineTimings = new Queue<long>(10);
        private readonly Queue<long> _renderTimings = new Queue<long>(60);
        private readonly Queue<long> _clickIntervals = new Queue<long>(10);

        // Thread safety locks
        private readonly object _clickTimingsLock = new object();
        private readonly object _altarTimingsLock = new object();
        private readonly object _delveFlareTimingsLock = new object();
        private readonly object _shrineTimingsLock = new object();
        private readonly object _renderTimingsLock = new object();
        private readonly object _clickIntervalsLock = new object();

        // FPS calculation
        private readonly Stopwatch _fpsTimer = new Stopwatch();
        private int _frameCount = 0;
        private double _currentFps = 0;

        // Last timing values for display
        private long _lastAltarTiming = 0;
        private long _lastClickTiming = 0;
        private long _lastDelveFlareTiming = 0;
        private long _lastShrineTiming = 0;
        private long _lastRenderTiming = 0;

        // Max timing values for display
        private long _maxAltarTiming = 0;
        private long _maxClickTiming = 0;
        private long _maxDelveFlareTiming = 0;
        private long _maxShrineTiming = 0;

        // Input safety timing
        private readonly Stopwatch _lastHotkeyReleaseTimer = new Stopwatch();
        private readonly Stopwatch _lastRenderTimer = new Stopwatch();
        private readonly Stopwatch _lastTickTimer = new Stopwatch();

        public double CurrentFPS => _currentFps;
        public Queue<long> RenderTimings => _renderTimings;

        public void Start()
        {
            _mainTimer.Start();
            _secondTimer.Start();
            _lastRenderTimer.Start();
            _lastTickTimer.Start();
        }

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
                case "delveFlare":
                    _delveFlareCoroutineTimer.Restart();
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
                case "delveFlare":
                    _delveFlareCoroutineTimer.Stop();
                    long delveFlareTiming = _delveFlareCoroutineTimer.ElapsedMilliseconds;
                    _lastDelveFlareTiming = delveFlareTiming;
                    _maxDelveFlareTiming = Math.Max(_maxDelveFlareTiming, delveFlareTiming);
                    EnqueueTiming(_delveFlareCoroutineTimings, delveFlareTiming, 10, _delveFlareTimingsLock);
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
                case "delveFlare":
                    return _lastDelveFlareTiming;
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
                case "delveFlare":
                    queue = _delveFlareCoroutineTimings;
                    lockObj = _delveFlareTimingsLock;
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

        public double GetClickTargetInterval()
        {
            return _settings.ClickFrequencyTarget.Value;
        }

        public double GetMaxTiming(string timingType)
        {
            switch (timingType)
            {
                case "click":
                    return _maxClickTiming;
                case "altar":
                    return _maxAltarTiming;
                case "delveFlare":
                    return _maxDelveFlareTiming;
                case "shrine":
                    return _maxShrineTiming;
                default:
                    return 0;
            }
        }

        public bool ShouldTriggerSecondTimerAction(int intervalMs = 200)
        {
            if (_secondTimer.ElapsedMilliseconds > intervalMs)
            {
                _secondTimer.Restart();
                return true;
            }
            return false;
        }

        public bool ShouldTriggerMainTimerAction(int intervalMs)
        {
            return _mainTimer.ElapsedMilliseconds > intervalMs;
        }

        public void ResetMainTimer()
        {
            _mainTimer.Restart();
        }

        public void StartHotkeyReleaseTimer()
        {
            _lastHotkeyReleaseTimer.Restart();
        }

        public void StopHotkeyReleaseTimer()
        {
            _lastHotkeyReleaseTimer.Stop();
        }

        public bool IsHotkeyReleaseTimeoutExceeded(int timeoutMs = 5000)
        {
            return !_lastHotkeyReleaseTimer.IsRunning ||
                   _lastHotkeyReleaseTimer.ElapsedMilliseconds > timeoutMs;
        }

        public void RecordClickInterval()
        {
            // Use dedicated click interval timer that only runs between clicks
            if (_clickIntervalTimer.IsRunning)
            {
                long interval = _clickIntervalTimer.ElapsedMilliseconds;
                EnqueueTiming(_clickIntervals, interval, 10, _clickIntervalsLock);
            }
            // Restart the timer for the next interval measurement
            _clickIntervalTimer.Restart();
        }

        public double GetAverageClickInterval()
        {
            lock (_clickIntervalsLock)
            {
                return _clickIntervals.Count > 0 ? _clickIntervals.Average() : 0;
            }
        }

        // Helper to enqueue timing measurements and keep a fixed-length queue
        private void EnqueueTiming(Queue<long> queue, long value, int maxLength, object lockObject)
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