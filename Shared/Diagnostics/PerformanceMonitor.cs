using System.Runtime;

namespace ClickIt.Shared.Diagnostics
{
    public enum TimingChannel
    {
        Unknown = 0,
        Click = 1,
        Altar = 2,
        Flare = 3,
        Render = 4,
        Blight = 5,
        Ultimatum = 6,
        LabelOverlay = 7
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
        HarvestOverlay = 9,
        BlightOverlay = 10,
        ClickHotkeyToggle = 11,
        InventoryFullWarning = 12,
        UiRegionRectangle = 13,
        PerformanceOverlay = 14,
        TextFlush = 7,
        FrameFlush = 8
    }

    // Per-feature domain work (label scan, altar scan, blight refresh, path build, ...). Recorded
    // at the processing boundaries independent of rendering or the coroutine framework so the debug
    // table can show "how much does feature X cost each run".
    public enum ProcessingSection
    {
        Unknown = 0,
        Altar = 1,
        Blight = 2,
        Click = 3,
        Flare = 4,
        Harvest = 5,
        Label = 6,
        Pathfinding = 7,
        Strongbox = 8,
        Ultimatum = 9,
        AreaBlockedUi = 10,
        ManualUiHover = 11
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
        private readonly ProcessingSectionMetricsStore _processingMetrics = new();
        private readonly GcAllocationMetricsStore _gcAllocationMetrics = new();
        private readonly LabelScanAllocationStore _labelScanAllocation = new();
        private readonly ClickAllocationStore _clickAllocation = new();
        private readonly TimingChannelMetricsTracker _timingTracker = new();

        private readonly Stopwatch _mainTimer = new();
        private readonly Stopwatch _secondTimer = new();

        // Input safety timing
        private readonly Stopwatch _lastHotkeyReleaseTimer = new();
        private readonly Stopwatch _lastRenderTimer = new();
        private readonly Stopwatch _lastTickTimer = new();

        private long _lastWorkingSetFetchMs;
        private double _cachedWorkingSetMb;

        internal ClickActivityTracker ClickActivity { get; } = new();

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

        public double GetAveragePeriod(TimingChannel channel)
            => _timingTracker.GetAveragePeriod(channel);

        public Queue<double> GetRenderTimingsSnapshot()
            => _timingTracker.GetRenderTimingsSnapshot();

        public (double Current, double Average, double Max) GetFpsStats()
            => _fpsTracker.GetStats();

        internal PerformanceMetricsSnapshot GetDebugSnapshot()
        {
            TimingMetricsSnapshot MapRenderSection(RenderSection section)
            {
                (double LastMs, double AverageMs, double MaxMs, long SampleCount) = GetRenderSectionStats(section);
                return new TimingMetricsSnapshot(LastMs, AverageMs, MaxMs, SampleCount);
            }

            TimingMetricsSnapshot MapTimingChannel(TimingChannel channel)
                => new(GetLastTiming(channel), GetAverageTiming(channel), GetMaxTiming(channel), GetTimingSampleCount(channel), GetAveragePeriod(channel));

            TimingMetricsSnapshot MapProcessingSection(ProcessingSection section)
            {
                (double LastMs, double AverageMs, double MaxMs, long SampleCount, double AveragePeriodMs) = GetProcessingStats(section);
                return new TimingMetricsSnapshot(LastMs, AverageMs, MaxMs, SampleCount, AveragePeriodMs);
            }

            Dictionary<ProcessingSection, GcAllocationSnapshot> MapAllocations()
            {
                Dictionary<ProcessingSection, GcAllocationSnapshot> allocations = [];
                foreach (ProcessingSection section in Enum.GetValues<ProcessingSection>())
                {
                    if (section == ProcessingSection.Unknown)
                        continue;
                    GcAllocationSnapshot stats = GetAllocationStats(section);
                    if (stats.SampleCount > 0)
                        allocations[section] = stats;
                }
                return allocations;
            }

            (double LastMs, double AverageMs, double MaxMs, int SampleCount) renderStats = GetRenderTimingStats();
            (double Current, double Average, double Max) = GetFpsStats();

            return new PerformanceMetricsSnapshot(
                new FpsMetricsSnapshot(Current, Average, Max),
                new TimingMetricsSnapshot(renderStats.LastMs, renderStats.AverageMs, renderStats.MaxMs, renderStats.SampleCount),
                MapRenderSection(RenderSection.LazyMode),
                MapRenderSection(RenderSection.DebugOverlay),
                MapRenderSection(RenderSection.AltarOverlay),
                MapRenderSection(RenderSection.UltimatumOverlay),
                MapRenderSection(RenderSection.StrongboxOverlay),
                MapRenderSection(RenderSection.PathfindingOverlay),
                MapRenderSection(RenderSection.HarvestOverlay),
                MapRenderSection(RenderSection.BlightOverlay),
                MapRenderSection(RenderSection.ClickHotkeyToggle),
                MapRenderSection(RenderSection.InventoryFullWarning),
                MapRenderSection(RenderSection.UiRegionRectangle),
                MapRenderSection(RenderSection.PerformanceOverlay),
                MapRenderSection(RenderSection.TextFlush),
                MapRenderSection(RenderSection.FrameFlush),
                MapTimingChannel(TimingChannel.Altar),
                MapTimingChannel(TimingChannel.Click),
                MapTimingChannel(TimingChannel.Flare),
                MapTimingChannel(TimingChannel.Blight),
                MapTimingChannel(TimingChannel.Ultimatum),
                MapTimingChannel(TimingChannel.LabelOverlay),
                MapProcessingSection(ProcessingSection.Altar),
                MapProcessingSection(ProcessingSection.Blight),
                MapProcessingSection(ProcessingSection.Click),
                MapProcessingSection(ProcessingSection.Flare),
                MapProcessingSection(ProcessingSection.Harvest),
                MapProcessingSection(ProcessingSection.Label),
                MapProcessingSection(ProcessingSection.Pathfinding),
                MapProcessingSection(ProcessingSection.Strongbox),
                MapProcessingSection(ProcessingSection.Ultimatum),
                MapProcessingSection(ProcessingSection.AreaBlockedUi),
                MapProcessingSection(ProcessingSection.ManualUiHover),
                GetClickTargetInterval(),
                GetAverageSuccessfulClickTiming(),
                GetAverageClickInterval(),
                GetAverageClickSleepMs(),
                MapAllocations(),
                GetLabelScanAllocationStats(),
                GetClickAllocationStats(),
                GetMemorySnapshot());
        }

        internal void RecordFpsSample(double fps)
            => _fpsTracker.RecordSample(fps);

        public void RecordRenderSectionTiming(RenderSection section, double ms)
            => _renderSectionMetrics.Record(section, ms);

        public (double LastMs, double AverageMs, double MaxMs, long SampleCount) GetRenderSectionStats(RenderSection section)
            => _renderSectionMetrics.GetStats(section);

        public void RecordProcessingTiming(ProcessingSection section, double ms)
            => _processingMetrics.Record(section, ms);

        public (double LastMs, double AverageMs, double MaxMs, long SampleCount, double AveragePeriodMs) GetProcessingStats(ProcessingSection section)
            => _processingMetrics.GetStats(section);

        public void RecordAllocation(ProcessingSection section, long bytes)
            => _gcAllocationMetrics.Record(section, bytes);

        internal GcAllocationSnapshot GetAllocationStats(ProcessingSection section)
            => _gcAllocationMetrics.GetStats(section);

        internal void RecordLabelScanAllocation(LabelScanAllocationBreakdown breakdown)
            => _labelScanAllocation.Record(breakdown);

        internal LabelScanAllocationStats GetLabelScanAllocationStats()
            => _labelScanAllocation.GetStats();

        internal void RecordClickAllocation(ClickAllocationBreakdown breakdown)
            => _clickAllocation.Record(breakdown);

        internal ClickAllocationStats GetClickAllocationStats()
            => _clickAllocation.GetStats();

        public (double LastMs, double AverageMs, double MaxMs, int SampleCount) GetRenderTimingStats()
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
            => ClickActivity.RecordClickInterval(_mainTimer.ElapsedMilliseconds);

        public double GetAverageClickInterval()
            => ClickActivity.GetAverageClickInterval();

        public void ResetClickCount()
            => ClickActivity.ResetClickCount();

        public void ShutdownForHotReload()
        {
            _mainTimer.Stop();
            _secondTimer.Stop();
            _fpsTracker.Stop();
            _lastHotkeyReleaseTimer.Stop();
            _lastRenderTimer.Stop();
            _lastTickTimer.Stop();
            _timingTracker.Clear();
            ClickActivity.Clear();
        }

        public void RecordSuccessfulClickTiming(long duration)
        {
            _timingTracker.RecordSuccessfulClickTiming(duration);
        }

        public double GetAverageSuccessfulClickTiming()
        {
            return _timingTracker.GetAverageSuccessfulClickTiming();
        }

        public void RecordClickSleepTiming(double ms)
        {
            _timingTracker.RecordClickSleepTiming(ms);
        }

        public double GetAverageClickSleepMs()
        {
            return _timingTracker.GetAverageClickSleepMs();
        }

        internal int GetTimingSampleCount(TimingChannel channel)
            => _timingTracker.GetTimingSampleCount(channel);

        // Working set read is throttled to once per second: Process.GetCurrentProcess() allocates a
        // Process object per call, which would add GC pressure on the per-frame snapshot path.
        private double GetProcessWorkingSetMb()
        {
            long now = Environment.TickCount64;
            if (now - _lastWorkingSetFetchMs < 1000 && _cachedWorkingSetMb > 0)
                return _cachedWorkingSetMb;
            _lastWorkingSetFetchMs = now;
            try
            {
                _cachedWorkingSetMb = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
            }
            catch
            {
                _cachedWorkingSetMb = 0;
            }
            return _cachedWorkingSetMb;
        }

        internal MemoryMetricsSnapshot GetMemorySnapshot()
        {
            GCMemoryInfo info = GC.GetGCMemoryInfo();
            double mb = 1024.0 * 1024.0;
            double loadPercent = info.TotalAvailableMemoryBytes > 0
                ? info.TotalCommittedBytes * 100.0 / info.TotalAvailableMemoryBytes
                : 0;
            return new MemoryMetricsSnapshot(
                GetProcessWorkingSetMb(),
                GC.GetTotalMemory(false) / mb,
                info.GenerationInfo[0].SizeAfterBytes / mb,
                info.GenerationInfo[1].SizeAfterBytes / mb,
                info.GenerationInfo[2].SizeAfterBytes / mb,
                info.GenerationInfo[3].SizeAfterBytes / mb,
                info.FragmentedBytes / mb,
                loadPercent);
        }
    }
}
