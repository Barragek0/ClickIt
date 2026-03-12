using System.Diagnostics;

namespace ClickIt.Utils
{
    public enum TimingChannel
    {
        Unknown = 0,
        Click = 1,
        Altar = 2,
        Flare = 3,
        Shrine = 4,
        Render = 5
    }

    /// <summary>
    /// Handles all performance monitoring, timing, and FPS calculations for the ClickIt plugin.
    /// Provides thread-safe access to timing queues and performance metrics.
    /// </summary>
    public partial class PerformanceMonitor(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

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
        private readonly Queue<long> _successfulClickTimings = new(10);

        // Thread safety locks
        private readonly object _clickTimingsLock = new();
        private readonly object _altarTimingsLock = new();
        private readonly object _flareTimingsLock = new();
        private readonly object _shrineTimingsLock = new();
        private readonly object _renderTimingsLock = new();
        private readonly object _clickIntervalsLock = new();
        private readonly object _successfulClickTimingsLock = new();

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

        public Queue<long> GetRenderTimingsSnapshot()
        {
            lock (_renderTimingsLock)
            {
                return new Queue<long>(_renderTimings);
            }
        }

        public void Start()
        {
            _mainTimer.Start();
            _secondTimer.Start();
            _lastRenderTimer.Start();
            _lastTickTimer.Start();
        }

        public double GetClickTargetInterval()
        {
            return _settings.ClickFrequencyTarget.Value;
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

        public void RecordSuccessfulClickTiming(long duration)
        {
            EnqueueTiming(_successfulClickTimings, duration, 10, _successfulClickTimingsLock);
        }

        public double GetAverageSuccessfulClickTiming()
        {
            lock (_successfulClickTimingsLock)
            {
                return _successfulClickTimings.Count > 0 ? _successfulClickTimings.Average() : 0;
            }
        }
    }
}
