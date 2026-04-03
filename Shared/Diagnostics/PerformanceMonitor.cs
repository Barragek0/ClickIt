namespace ClickIt.Shared.Diagnostics
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
    public class PerformanceMonitor(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly FpsTracker _fpsTracker = new();
        private readonly RenderSectionMetricsStore _renderSectionMetrics = new();
        private readonly ClickActivityTracker _clickActivity = new();
        private readonly TimingChannelMetricsTracker _timingTracker = new();

        private readonly Stopwatch _mainTimer = new();
        private readonly Stopwatch _secondTimer = new();
        private readonly Queue<long> _successfulClickTimings = new(10);
        private readonly object _successfulClickTimingsLock = new();

        // Input safety timing
        private readonly Stopwatch _lastHotkeyReleaseTimer = new();
        private readonly Stopwatch _lastRenderTimer = new();
        private readonly Stopwatch _lastTickTimer = new();

        internal ClickActivityTracker ClickActivity => _clickActivity;

        public double CurrentFPS => _fpsTracker.CurrentFps;

        public void UpdateFPS()
            => _fpsTracker.RecordFrame();

        public void StartRenderTiming()
            => _timingTracker.StartRenderTiming();

        public void StopRenderTiming()
            => _timingTracker.StopRenderTiming();

        public void StartCoroutineTiming(TimingChannel channel)
            => _timingTracker.StartCoroutineTiming(channel);

        public void StartCoroutineTiming(string coroutineName)
            => _timingTracker.StartCoroutineTiming(coroutineName);

        public void StopCoroutineTiming(TimingChannel channel)
            => _timingTracker.StopCoroutineTiming(channel);

        public void StopCoroutineTiming(string coroutineName)
            => _timingTracker.StopCoroutineTiming(coroutineName);

        public double GetLastTiming(TimingChannel channel)
            => _timingTracker.GetLastTiming(channel);

        public double GetLastTiming(string timingType)
            => _timingTracker.GetLastTiming(timingType);

        public double GetAverageTiming(TimingChannel channel)
            => _timingTracker.GetAverageTiming(channel);

        public double GetAverageTiming(string timingType)
            => _timingTracker.GetAverageTiming(timingType);

        public double GetMaxTiming(TimingChannel channel)
            => _timingTracker.GetMaxTiming(channel);

        public double GetMaxTiming(string timingType)
            => _timingTracker.GetMaxTiming(timingType);

        public Queue<long> GetRenderTimingsSnapshot()
            => _timingTracker.GetRenderTimingsSnapshot();

        public (double Current, double Average, double Max) GetFpsStats()
            => _fpsTracker.GetStats();

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

        internal void RecordFpsSample(double fps)
            => _fpsTracker.RecordSample(fps);

        public void RecordRenderSectionTiming(RenderSection section, double ms)
            => _renderSectionMetrics.Record(section, ms);

        public (double LastMs, double AverageMs, double MaxMs, long SampleCount) GetRenderSectionStats(RenderSection section)
            => _renderSectionMetrics.GetStats(section);

        public (long LastMs, double AverageMs, long MaxMs, int SampleCount) GetRenderTimingStats()
            => _timingTracker.GetRenderTimingStats();

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
            => _clickActivity.RecordClickInterval(_mainTimer.ElapsedMilliseconds);

        public double GetAverageClickInterval()
            => _clickActivity.GetAverageClickInterval();

        public void ResetClickCount()
            => _clickActivity.ResetClickCount();

        public void ShutdownForHotReload()
        {
            _mainTimer.Stop();
            _secondTimer.Stop();
            _fpsTracker.Stop();
            _lastHotkeyReleaseTimer.Stop();
            _lastRenderTimer.Stop();
            _lastTickTimer.Stop();
            _timingTracker.Clear();
            _clickActivity.Clear();
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

        internal int GetTimingSampleCount(TimingChannel channel)
            => _timingTracker.GetTimingSampleCount(channel);

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
    }
}
