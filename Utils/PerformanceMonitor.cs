using System.Diagnostics;
using ClickIt.Services.Observability;

namespace ClickIt.Utils
{
    public enum TimingChannel
    {
        Unknown = 0,
        Click = 1,
        Altar = 2,
        Flare = 3,
        Render = 4
    }

    public enum RenderSection
    {
        Unknown = 0,
        LazyMode = 1,
        DebugOverlay = 2,
        AltarOverlay = 3,
        UltimatumOverlay = 4,
        StrongboxOverlay = 5,
        PathfindingOverlay = 6,
        TextFlush = 7,
        FrameFlush = 8
    }

    /// <summary>
    /// Handles all performance monitoring, timing, and FPS calculations for the ClickIt plugin.
    /// Provides thread-safe access to timing queues and performance metrics.
    /// </summary>
    public partial class PerformanceMonitor(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        private readonly Stopwatch _mainTimer = new();
        private readonly Stopwatch _secondTimer = new();

        private readonly Stopwatch _renderTimer = new();
        private readonly Stopwatch _altarCoroutineTimer = new();
        private readonly Stopwatch _clickCoroutineTimer = new();
        private readonly Stopwatch _flareCoroutineTimer = new();

        private readonly Queue<long> _clickCoroutineTimings = new(10);
        private readonly Queue<long> _altarCoroutineTimings = new(10);
        private readonly Queue<long> _flareCoroutineTimings = new(10);
        private readonly Queue<long> _renderTimings = new(60);
        private readonly Queue<long> _clickIntervals = new(10);
        private readonly Queue<long> _successfulClickTimings = new(10);

        // Thread safety locks
        private readonly object _clickTimingsLock = new();
        private readonly object _altarTimingsLock = new();
        private readonly object _flareTimingsLock = new();
        private readonly object _renderTimingsLock = new();
        private readonly object _clickIntervalsLock = new();
        private readonly object _successfulClickTimingsLock = new();

        private readonly Stopwatch _fpsTimer = new();
        private int _frameCount = 0;
        private double _currentFps = 0;
        private double _maxFps = 0;
        private double _fpsSampleSum = 0;
        private int _fpsSampleCount = 0;

        private long _lastAltarTiming = 0;
        private long _lastClickTiming = 0;
        private long _lastFlareTiming = 0;
        private long _lastRenderTiming = 0;

        private long _maxAltarTiming = 0;
        private long _maxClickTiming = 0;
        private long _maxFlareTiming = 0;

        // Input safety timing
        private readonly Stopwatch _lastHotkeyReleaseTimer = new();
        private readonly Stopwatch _lastRenderTimer = new();
        private readonly Stopwatch _lastTickTimer = new();

        private long _lastClickTime = 0;
        private int _clickCount = 0;

        private double _lastLazyModeMs = 0;
        private double _avgLazyModeMs = 0;
        private double _maxLazyModeMs = 0;
        private long _lazyModeSamples = 0;

        private double _lastDebugOverlayMs = 0;
        private double _avgDebugOverlayMs = 0;
        private double _maxDebugOverlayMs = 0;
        private long _debugOverlaySamples = 0;

        private double _lastAltarOverlayMs = 0;
        private double _avgAltarOverlayMs = 0;
        private double _maxAltarOverlayMs = 0;
        private long _altarOverlaySamples = 0;

        private double _lastUltimatumOverlayMs = 0;
        private double _avgUltimatumOverlayMs = 0;
        private double _maxUltimatumOverlayMs = 0;
        private long _ultimatumOverlaySamples = 0;

        private double _lastStrongboxOverlayMs = 0;
        private double _avgStrongboxOverlayMs = 0;
        private double _maxStrongboxOverlayMs = 0;
        private long _strongboxOverlaySamples = 0;

        private double _lastTextFlushMs = 0;
        private double _avgTextFlushMs = 0;
        private double _maxTextFlushMs = 0;
        private long _textFlushSamples = 0;

        private double _lastPathfindingOverlayMs = 0;
        private double _avgPathfindingOverlayMs = 0;
        private double _maxPathfindingOverlayMs = 0;
        private long _pathfindingOverlaySamples = 0;

        private double _lastFrameFlushMs = 0;
        private double _avgFrameFlushMs = 0;
        private double _maxFrameFlushMs = 0;
        private long _frameFlushSamples = 0;

        public double CurrentFPS => _currentFps;

        public Queue<long> GetRenderTimingsSnapshot()
        {
            lock (_renderTimingsLock)
            {
                return new Queue<long>(_renderTimings);
            }
        }

        public (double Current, double Average, double Max) GetFpsStats()
        {
            double averageFps = _fpsSampleCount > 0 ? _fpsSampleSum / _fpsSampleCount : 0;
            return (_currentFps, averageFps, _maxFps);
        }

        internal PerformanceMetricsSnapshot GetDebugSnapshot()
        {
            TimingMetricsSnapshot MapRenderSection(RenderSection section)
            {
                var stats = GetRenderSectionStats(section);
                return new TimingMetricsSnapshot(stats.LastMs, stats.AverageMs, stats.MaxMs, stats.SampleCount);
            }

            TimingMetricsSnapshot MapTimingChannel(TimingChannel channel)
                => new(GetLastTiming(channel), GetAverageTiming(channel), GetMaxTiming(channel), GetTimingSampleCount(channel));

            var renderStats = GetRenderTimingStats();
            var fpsStats = GetFpsStats();

            return new PerformanceMetricsSnapshot(
                new FpsMetricsSnapshot(fpsStats.Current, fpsStats.Average, fpsStats.Max),
                new TimingMetricsSnapshot(renderStats.LastMs, renderStats.AverageMs, renderStats.MaxMs, renderStats.SampleCount),
                MapRenderSection(RenderSection.LazyMode),
                MapRenderSection(RenderSection.DebugOverlay),
                MapRenderSection(RenderSection.AltarOverlay),
                MapRenderSection(RenderSection.UltimatumOverlay),
                MapRenderSection(RenderSection.StrongboxOverlay),
                MapRenderSection(RenderSection.PathfindingOverlay),
                MapRenderSection(RenderSection.TextFlush),
                MapRenderSection(RenderSection.FrameFlush),
                MapTimingChannel(TimingChannel.Altar),
                MapTimingChannel(TimingChannel.Click),
                MapTimingChannel(TimingChannel.Flare),
                GetClickTargetInterval(),
                GetAverageSuccessfulClickTiming(),
                GetAverageClickInterval());
        }

        internal void RecordFpsSampleForTests(double fps)
        {
            _currentFps = fps;
            _fpsSampleSum += fps;
            _fpsSampleCount++;
            _maxFps = Math.Max(_maxFps, fps);
        }

        private static void RecordSample(ref double last, ref double avg, ref double max, ref long samples, double ms)
        {
            last = ms;
            samples++;
            avg += (ms - avg) / samples;
            if (ms > max)
                max = ms;
        }

        public void RecordRenderSectionTiming(RenderSection section, double ms)
        {
            switch (section)
            {
                case RenderSection.LazyMode:
                    RecordSample(ref _lastLazyModeMs, ref _avgLazyModeMs, ref _maxLazyModeMs, ref _lazyModeSamples, ms);
                    break;
                case RenderSection.DebugOverlay:
                    RecordSample(ref _lastDebugOverlayMs, ref _avgDebugOverlayMs, ref _maxDebugOverlayMs, ref _debugOverlaySamples, ms);
                    break;
                case RenderSection.AltarOverlay:
                    RecordSample(ref _lastAltarOverlayMs, ref _avgAltarOverlayMs, ref _maxAltarOverlayMs, ref _altarOverlaySamples, ms);
                    break;
                case RenderSection.UltimatumOverlay:
                    RecordSample(ref _lastUltimatumOverlayMs, ref _avgUltimatumOverlayMs, ref _maxUltimatumOverlayMs, ref _ultimatumOverlaySamples, ms);
                    break;
                case RenderSection.StrongboxOverlay:
                    RecordSample(ref _lastStrongboxOverlayMs, ref _avgStrongboxOverlayMs, ref _maxStrongboxOverlayMs, ref _strongboxOverlaySamples, ms);
                    break;
                case RenderSection.TextFlush:
                    RecordSample(ref _lastTextFlushMs, ref _avgTextFlushMs, ref _maxTextFlushMs, ref _textFlushSamples, ms);
                    break;
                case RenderSection.PathfindingOverlay:
                    RecordSample(ref _lastPathfindingOverlayMs, ref _avgPathfindingOverlayMs, ref _maxPathfindingOverlayMs, ref _pathfindingOverlaySamples, ms);
                    break;
                case RenderSection.FrameFlush:
                    RecordSample(ref _lastFrameFlushMs, ref _avgFrameFlushMs, ref _maxFrameFlushMs, ref _frameFlushSamples, ms);
                    break;
            }
        }

        public (double LastMs, double AverageMs, double MaxMs, long SampleCount) GetRenderSectionStats(RenderSection section)
        {
            return section switch
            {
                RenderSection.LazyMode => (_lastLazyModeMs, _avgLazyModeMs, _maxLazyModeMs, _lazyModeSamples),
                RenderSection.DebugOverlay => (_lastDebugOverlayMs, _avgDebugOverlayMs, _maxDebugOverlayMs, _debugOverlaySamples),
                RenderSection.AltarOverlay => (_lastAltarOverlayMs, _avgAltarOverlayMs, _maxAltarOverlayMs, _altarOverlaySamples),
                RenderSection.UltimatumOverlay => (_lastUltimatumOverlayMs, _avgUltimatumOverlayMs, _maxUltimatumOverlayMs, _ultimatumOverlaySamples),
                RenderSection.StrongboxOverlay => (_lastStrongboxOverlayMs, _avgStrongboxOverlayMs, _maxStrongboxOverlayMs, _strongboxOverlaySamples),
                RenderSection.PathfindingOverlay => (_lastPathfindingOverlayMs, _avgPathfindingOverlayMs, _maxPathfindingOverlayMs, _pathfindingOverlaySamples),
                RenderSection.TextFlush => (_lastTextFlushMs, _avgTextFlushMs, _maxTextFlushMs, _textFlushSamples),
                RenderSection.FrameFlush => (_lastFrameFlushMs, _avgFrameFlushMs, _maxFrameFlushMs, _frameFlushSamples),
                _ => (0, 0, 0, 0)
            };
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
                return CalculateAverage(_clickIntervals);
            }
        }

        public void ResetClickCount()
        {
            _clickCount = 0;
        }

        public void ShutdownForHotReload()
        {
            _mainTimer.Stop();
            _secondTimer.Stop();
            _renderTimer.Stop();
            _altarCoroutineTimer.Stop();
            _clickCoroutineTimer.Stop();
            _flareCoroutineTimer.Stop();
            _fpsTimer.Stop();
            _lastHotkeyReleaseTimer.Stop();
            _lastRenderTimer.Stop();
            _lastTickTimer.Stop();

            lock (_clickTimingsLock)
                _clickCoroutineTimings.Clear();
            lock (_altarTimingsLock)
                _altarCoroutineTimings.Clear();
            lock (_flareTimingsLock)
                _flareCoroutineTimings.Clear();
            lock (_renderTimingsLock)
                _renderTimings.Clear();
            lock (_clickIntervalsLock)
                _clickIntervals.Clear();
            lock (_successfulClickTimingsLock)
                _successfulClickTimings.Clear();
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
    }
}
