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
        private readonly Stopwatch _mainTimer = new();
        private readonly Stopwatch _secondTimer = new();

        // Coroutine timing
        private readonly Stopwatch _renderTimer = new();
        private readonly Stopwatch _altarCoroutineTimer = new();
        private readonly Stopwatch _clickCoroutineTimer = new();
        private readonly Stopwatch _flareCoroutineTimer = new();
        private readonly Stopwatch _shrineCoroutineTimer = new();

        // Performance tracking queues with thread-safe access
        private readonly Queue<long> _clickCoroutineTimings = new(10);
        private readonly Queue<long> _altarCoroutineTimings = new(10);
        private readonly Queue<long> _flareCoroutineTimings = new(10);
        private readonly Queue<long> _shrineCoroutineTimings = new(10);
        private readonly Queue<long> _renderTimings = new(60);
        private readonly Queue<long> _clickIntervals = new(10);

        // Thread safety locks
        private readonly object _clickTimingsLock = new();
        private readonly object _altarTimingsLock = new();
        private readonly object _flareTimingsLock = new();
        private readonly object _shrineTimingsLock = new();
        private readonly object _renderTimingsLock = new();
        private readonly object _clickIntervalsLock = new();

        // FPS calculation
        private readonly Stopwatch _fpsTimer = new();
        private int _frameCount = 0;
        private double _currentFps = 0;

        // Last timing values for display
        private long _lastAltarTiming = 0;
        private long _lastClickTiming = 0;
        private long _lastFlareTiming = 0;
        private long _lastShrineTiming = 0;
        private long _lastRenderTiming = 0;

        // Max timing values for display
        private long _maxAltarTiming = 0;
        private long _maxClickTiming = 0;
        private long _maxFlareTiming = 0;
        private long _maxShrineTiming = 0;

        // Input safety timing
        private readonly Stopwatch _lastHotkeyReleaseTimer = new();
        private readonly Stopwatch _lastRenderTimer = new();
        private readonly Stopwatch _lastTickTimer = new();

        // Click interval tracking
        private long _lastClickTime = 0;
        private int _clickCount = 0;

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
                case "flare":
                    return _maxFlareTiming;
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
            _clickCount++;
            long currentTime = _mainTimer.ElapsedMilliseconds;
            if (_lastClickTime != 0 && _clickCount > 3) // Skip the first few clicks to avoid irregular intervals
            {
                long interval = currentTime - _lastClickTime;
                // Only record reasonable intervals (not too large, probably from stale timer state)
                if (interval > 0 && interval < 10000) // Max 10 seconds to avoid stale data
                {
                    EnqueueTiming(_clickIntervals, interval, 10, _clickIntervalsLock);
                }
            }
            _lastClickTime = currentTime;
        }

        public double GetAverageClickInterval()
        {
            lock (_clickIntervalsLock)
            {
                return _clickIntervals.Count > 0 ? _clickIntervals.Average() : 0;
            }
        }

        public void ResetClickCount()
        {
            _clickCount = 0;
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