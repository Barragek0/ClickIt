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
        // Primary timing controls
        private readonly Stopwatch _mainTimer = new Stopwatch();
        private readonly Stopwatch _secondTimer = new Stopwatch();

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

        // Thread safety locks
        private readonly object _clickTimingsLock = new object();
        private readonly object _altarTimingsLock = new object();
        private readonly object _delveFlareTimingsLock = new object();
        private readonly object _shrineTimingsLock = new object();
        private readonly object _renderTimingsLock = new object();

        // FPS calculation
        private readonly Stopwatch _fpsTimer = new Stopwatch();
        private int _frameCount = 0;
        private double _currentFps = 0;

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
            EnqueueTiming(_renderTimings, _renderTimer.ElapsedMilliseconds, 60, _renderTimingsLock);
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
                    EnqueueTiming(_altarCoroutineTimings, _altarCoroutineTimer.ElapsedMilliseconds, 10, _altarTimingsLock);
                    break;
                case "click":
                    _clickCoroutineTimer.Stop();
                    EnqueueTiming(_clickCoroutineTimings, _clickCoroutineTimer.ElapsedMilliseconds, 10, _clickTimingsLock);
                    break;
                case "delveFlare":
                    _delveFlareCoroutineTimer.Stop();
                    EnqueueTiming(_delveFlareCoroutineTimings, _delveFlareCoroutineTimer.ElapsedMilliseconds, 10, _delveFlareTimingsLock);
                    break;
                case "shrine":
                    _shrineCoroutineTimer.Stop();
                    EnqueueTiming(_shrineCoroutineTimings, _shrineCoroutineTimer.ElapsedMilliseconds, 10, _shrineTimingsLock);
                    break;
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