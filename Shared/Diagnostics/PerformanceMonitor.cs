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
        ManualUiHover = 11,
        GameStateDump = 12
    }

    /// <summary>
    /// Handles all performance monitoring, timing, and FPS calculations for the ClickIt plugin.
    /// Provides thread-safe access to timing queues and performance metrics.
    /// </summary>
    public class PerformanceMonitor(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        // Stage index of the Blight breakdown's "Executor" stage (the click-pipeline building work),
        // recorded separately from the refresh stages because it runs on the click thread.
        internal const int BlightExecutorStageIndex = 3;
        // Stage index of the Blight breakdown's "Events" stage (the entity-event background work:
        // retained-set tracking + DLR path reads per streamed entity), recorded separately because
        // it runs on the game's entity-event path at a different cadence than the refresh stages.
        internal const int BlightEventsStageIndex = 4;
        private readonly FpsTracker _fpsTracker = new();
        private readonly RenderSectionMetricsStore _renderSectionMetrics = new();
        private readonly ProcessingSectionMetricsStore _processingMetrics = new();
        private readonly GcAllocationMetricsStore _gcAllocationMetrics = new();
        private readonly LabelScanAllocationStore _labelScanAllocation = new();
        private readonly ClickAllocationStore _clickAllocation = new();
        private readonly TimingChannelMetricsTracker _timingTracker = new();

        // Per-frame render-section accumulators (render-thread only): OverlayRenderHost accumulates
        // each overlay's enqueue cost, flush attribution accumulates the actual draw cost, and
        // CompleteRenderSectionFrame records ONE combined sample per section per frame so the section
        // average is enqueue + flush (separate samples would dilute the average by 2x and hide the
        // flush attribution from the tables).
        private readonly Dictionary<RenderSection, double> _pendingRenderSectionMs = [];
        private readonly Dictionary<RenderSection, double> _pendingRenderSectionFlushMs = [];

        // Per-area sub-stage breakdowns (allocation + time) for the heavier processing sections.
        // Populated once in the constructor and read-only afterwards, so concurrent Record calls are
        // safe; only sections with a registered breakdown can be recorded (unregistered = no-op).
        private readonly Dictionary<ProcessingSection, BreakdownStageStore> _breakdownStores = new()
        {
            [ProcessingSection.Pathfinding] = new BreakdownStageStore(trackTiming: true, "Terrain", "Goal", "AStar", "Projection"),
            [ProcessingSection.Blight] = new BreakdownStageStore(trackTiming: true, "Entities", "Foundations", "Coverage", "Executor", "Events"),
            [ProcessingSection.Flare] = new BreakdownStageStore(trackTiming: true, "Buffs", "Decision", "Input"),
            [ProcessingSection.Altar] = new BreakdownStageStore(trackTiming: true, "Labels", "Build"),
            [ProcessingSection.Strongbox] = new BreakdownStageStore(trackTiming: true, "Metadata", "Resolve", "Scan"),
        };

        private readonly Stopwatch _mainTimer = new();
        private readonly Stopwatch _secondTimer = new();

        // Input safety timing
        private readonly Stopwatch _lastHotkeyReleaseTimer = new();
        private readonly Stopwatch _lastRenderTimer = new();
        private readonly Stopwatch _lastTickTimer = new();

        private long _lastWorkingSetFetchMs;
        private double _cachedWorkingSetMb;

        // Rolling DLR-read rate (reads/sec + ms/sec) sampled on a 500ms cadence from the
        // DynamicAccess counters, plus the share of reads in the last window that failed. The ms/sec
        // is the actual wall-clock time spent inside dynamic reads (the freeze-relevant question —
        // how much main-thread time do the reads actually cost), mirroring the GC Pause row.
        private readonly ExpiringSampleBuffer _dlrReadRateBuffer = new(expiryMs: 10_000, maxSamples: 60, averageSamples: 10);
        private readonly ExpiringSampleBuffer _dlrReadMsBuffer = new(expiryMs: 10_000, maxSamples: 60, averageSamples: 10);
        private long _lastDlrSampleMs;
        private DynamicAccessStats _lastDlrStats;
        private double _dlrFailPercent;

        // Per-feature DLR-read attribution: one reads/sec + ms/sec buffer per ProcessingSection (the
        // same sections the DLR table rows use), sampled on the same 500ms cadence as the total.
        private readonly ExpiringSampleBuffer[] _dlrSectionReadsBuffers = CreateSectionBuffers();
        private readonly ExpiringSampleBuffer[] _dlrSectionMsBuffers = CreateSectionBuffers();
        private readonly long[] _lastDlrSectionCalls = new long[DynamicAccess.DlrSectionCount];
        private readonly long[] _lastDlrSectionTicks = new long[DynamicAccess.DlrSectionCount];

        // Per-feature GC allocation-rate attribution: one bytes/sec buffer per ProcessingSection,
        // sampled on the same 500ms cadence as the timing tables so the GC table's Last/Avg/Max
        // matches the other tables exactly (10s expiry, 10-sample average). RecordAllocation
        // accumulates per-section bytes; SampleGcAllocationRates converts the delta over the window
        // into a bytes/sec rate.
        private readonly ExpiringSampleBuffer[] _gcSectionByteBuffers = CreateSectionBuffers();
        private readonly long[] _gcSectionCumulativeBytes = new long[DynamicAccess.DlrSectionCount];
        private readonly long[] _lastGcSectionCumulativeBytes = new long[DynamicAccess.DlrSectionCount];
        private long _lastGcSampleMs;

        private static ExpiringSampleBuffer[] CreateSectionBuffers()
        {
            ExpiringSampleBuffer[] buffers = new ExpiringSampleBuffer[DynamicAccess.DlrSectionCount];
            for (int i = 0; i < buffers.Length; i++)
                buffers[i] = new ExpiringSampleBuffer(expiryMs: 10_000, maxSamples: 60, averageSamples: 10);
            return buffers;
        }

        internal ClickActivityTracker ClickActivity { get; } = new();

        public double CurrentFPS => _fpsTracker.CurrentFps;

        public void UpdateFPS()
        {
            _fpsTracker.RecordFrame();
            SampleDlrReadRate();
            SampleGcAllocationRates();
        }

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
                MapProcessingSection(ProcessingSection.GameStateDump),
                GetClickTargetInterval(),
                GetAverageSuccessfulClickTiming(),
                GetAverageClickInterval(),
                GetAverageClickSleepMs(),
                MapAllocations(),
                GetLabelScanAllocationStats(),
                GetClickAllocationStats(),
                GetMemorySnapshot(),
                GetBreakdownStats());
        }

        internal void RecordFpsSample(double fps)
            => _fpsTracker.RecordSample(fps);

        public void RecordRenderSectionTiming(RenderSection section, double ms)
            => _renderSectionMetrics.Record(section, ms);

        // Enqueue side of the per-frame render section accumulator (see the fields above).
        public void AccumulateRenderSectionTiming(RenderSection section, double ms)
        {
            if (section == RenderSection.Unknown)
                return;
            _pendingRenderSectionMs[section] = _pendingRenderSectionMs.TryGetValue(section, out double existing) ? existing + ms : ms;
        }

        // Flush side of the per-frame render section accumulator (see the fields above).
        public void AccumulateRenderSectionFlush(RenderSection section, double ms)
        {
            if (section == RenderSection.Unknown)
                return;
            _pendingRenderSectionFlushMs[section] = _pendingRenderSectionFlushMs.TryGetValue(section, out double existing) ? existing + ms : ms;
        }

        // Records one combined (enqueue + flush) sample per render section that drew this frame.
        public void CompleteRenderSectionFrame()
        {
            foreach (KeyValuePair<RenderSection, double> entry in _pendingRenderSectionMs)
            {
                double flush = _pendingRenderSectionFlushMs.TryGetValue(entry.Key, out double value) ? value : 0;
                _renderSectionMetrics.Record(entry.Key, entry.Value + flush);
            }
            foreach (KeyValuePair<RenderSection, double> entry in _pendingRenderSectionFlushMs)
            {
                if (!_pendingRenderSectionMs.ContainsKey(entry.Key))
                    _renderSectionMetrics.Record(entry.Key, entry.Value);
            }
            _pendingRenderSectionMs.Clear();
            _pendingRenderSectionFlushMs.Clear();
        }

        public (double LastMs, double AverageMs, double MaxMs, long SampleCount) GetRenderSectionStats(RenderSection section)
            => _renderSectionMetrics.GetStats(section);

        public void RecordProcessingTiming(ProcessingSection section, double ms)
            => _processingMetrics.Record(section, ms);

        public (double LastMs, double AverageMs, double MaxMs, long SampleCount, double AveragePeriodMs) GetProcessingStats(ProcessingSection section)
            => _processingMetrics.GetStats(section);

        public void RecordAllocation(ProcessingSection section, long bytes)
        {
            _gcAllocationMetrics.Record(section, bytes);
            if (section != ProcessingSection.Unknown && bytes > 0)
                _ = Interlocked.Add(ref _gcSectionCumulativeBytes[(int)section], bytes);
        }

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

        public void RecordBreakdown(ProcessingSection section, ReadOnlySpan<long> stageBytes, ReadOnlySpan<double> stageMs)
        {
            if (_breakdownStores.TryGetValue(section, out BreakdownStageStore? store))
                store.Record(stageBytes, stageMs);
        }

        // Records a single named stage of a section's breakdown (see BreakdownStageStore.RecordStage).
        public void RecordBreakdownStage(ProcessingSection section, int stageIndex, long bytes, double ms)
        {
            if (_breakdownStores.TryGetValue(section, out BreakdownStageStore? store))
                store.RecordStage(stageIndex, bytes, ms);
        }

        internal IReadOnlyDictionary<ProcessingSection, BreakdownStats> GetBreakdownStats()
        {
            Dictionary<ProcessingSection, BreakdownStats> result = [];
            foreach (KeyValuePair<ProcessingSection, BreakdownStageStore> kvp in _breakdownStores)
            {
                BreakdownStats stats = kvp.Value.GetStats();
                if (stats.SampleCount > 0)
                    result[kvp.Key] = stats;
            }
            return result;
        }

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

        // Samples the DynamicAccess read counters on a 500ms cadence (called every render frame) so
        // the debug tables show a rolling reads/sec AND ms/sec rate for the freeze-relevant DLR-read
        // cost. The percent is the share of reads in the window that failed (wasted reads on invalid
        // entities). nowMs is injectable for tests; production uses Environment.TickCount64.
        internal void SampleDlrReadRate(long? nowMs = null)
        {
            long now = nowMs ?? Environment.TickCount64;
            long elapsed = now - _lastDlrSampleMs;
            if (elapsed < 500)
                return;
            _lastDlrSampleMs = now;

            DynamicAccessStats stats = DynamicAccess.GetStats();
            long callsDelta = stats.TryGetCalls - _lastDlrStats.TryGetCalls;
            long ticksDelta = stats.TryGetTicks - _lastDlrStats.TryGetTicks;
            long failDelta = stats.NullSourceFailures - _lastDlrStats.NullSourceFailures
                + stats.RuntimeBinderFailures - _lastDlrStats.RuntimeBinderFailures
                + stats.OtherFailures - _lastDlrStats.OtherFailures
                + stats.BoolConversionFailures - _lastDlrStats.BoolConversionFailures
                + stats.FloatConversionFailures - _lastDlrStats.FloatConversionFailures
                + stats.IntConversionFailures - _lastDlrStats.IntConversionFailures
                + stats.EmptyStringFailures - _lastDlrStats.EmptyStringFailures;
            _lastDlrStats = stats;

            // Per-feature attribution: each section's reads/sec + ms/sec over this window.
            DlrSectionStats[] sectionStats = DynamicAccess.GetSectionStats();
            for (int s = 1; s < DynamicAccess.DlrSectionCount; s++)
            {
                long sectionCalls = sectionStats[s].Calls - _lastDlrSectionCalls[s];
                long sectionTicks = sectionStats[s].Ticks - _lastDlrSectionTicks[s];
                _lastDlrSectionCalls[s] = sectionStats[s].Calls;
                _lastDlrSectionTicks[s] = sectionStats[s].Ticks;
                if (sectionCalls <= 0)
                {
                    _dlrSectionReadsBuffers[s].Record(0);
                    _dlrSectionMsBuffers[s].Record(0);
                    continue;
                }
                _dlrSectionReadsBuffers[s].Record(sectionCalls * 1000.0 / elapsed);
                _dlrSectionMsBuffers[s].Record(sectionTicks * 1000.0 / Stopwatch.Frequency * 1000.0 / elapsed);
            }

            if (callsDelta <= 0)
            {
                _dlrReadRateBuffer.Record(0);
                _dlrReadMsBuffer.Record(0);
                _dlrFailPercent = 0;
                return;
            }

            _dlrReadRateBuffer.Record(callsDelta * 1000.0 / elapsed);
            double msPerSec = ticksDelta * 1000.0 / Stopwatch.Frequency * 1000.0 / elapsed;
            _dlrReadMsBuffer.Record(msPerSec);
            _dlrFailPercent = failDelta * 100.0 / callsDelta;
        }

        // Per-feature GC allocation-rate sampling on the same 500ms cadence as the DLR reads: the
        // bytes recorded since the last sample, divided by the window, become that section's
        // bytes/sec for the buffer (10s expiry, 10-sample average, max over the live window). nowMs
        // is injectable for tests; production uses Environment.TickCount64.
        internal void SampleGcAllocationRates(long? nowMs = null)
        {
            long now = nowMs ?? Environment.TickCount64;
            long elapsed = now - _lastGcSampleMs;
            if (elapsed < 500)
                return;
            _lastGcSampleMs = now;

            for (int s = 1; s < DynamicAccess.DlrSectionCount; s++)
            {
                long cumulative = Interlocked.Read(ref _gcSectionCumulativeBytes[s]);
                long delta = cumulative - _lastGcSectionCumulativeBytes[s];
                _lastGcSectionCumulativeBytes[s] = cumulative;
                _gcSectionByteBuffers[s].Record(delta <= 0 ? 0 : delta * 1000.0 / elapsed);
            }
        }

        // Snapshot of the per-feature GC allocation rates (bytes/sec, indexed by ProcessingSection
        // value; the GC table renders rows 1..DlrSectionCount-1, 0 is the un-attributed Other bucket).
        internal GcSectionSnapshot[] GetGcSectionSnapshot()
        {
            GcSectionSnapshot[] result = new GcSectionSnapshot[DynamicAccess.DlrSectionCount];
            for (int s = 1; s < DynamicAccess.DlrSectionCount; s++)
            {
                (double bLast, double bAvg, double bMax, _) = _gcSectionByteBuffers[s].Stats;
                result[s] = new GcSectionSnapshot(bLast, bAvg, bMax);
            }
            return result;
        }

        // Snapshot of the per-feature DLR reads/sec + ms/sec (indexed by ProcessingSection value; the
        // DLR table renders rows 1..DlrSectionCount-1, 0 is the un-attributed Other bucket).
        internal DlrSectionSnapshot[] GetDlrSectionSnapshot()
        {
            DlrSectionSnapshot[] result = new DlrSectionSnapshot[DynamicAccess.DlrSectionCount];
            for (int s = 1; s < DynamicAccess.DlrSectionCount; s++)
            {
                (double rLast, double rAvg, double rMax, _) = _dlrSectionReadsBuffers[s].Stats;
                (double mLast, double mAvg, double mMax, _) = _dlrSectionMsBuffers[s].Stats;
                result[s] = new DlrSectionSnapshot(rLast, rAvg, rMax, mLast, mAvg, mMax);
            }
            return result;
        }

        internal MemoryMetricsSnapshot GetMemorySnapshot()
        {
            GCMemoryInfo info = GC.GetGCMemoryInfo();
            double mb = 1024.0 * 1024.0;
            double loadPercent = info.TotalAvailableMemoryBytes > 0
                ? info.TotalCommittedBytes * 100.0 / info.TotalAvailableMemoryBytes
                : 0;

            // Recent blocking-GC pause picture (the actual cause of whole-process hitches). The
            // pause durations span only the GCs since the last GetGCMemoryInfo() call, so they are
            // a rolling recent window, not process-lifetime.
            double pauseLastMs = 0, pauseAvgMs = 0, pauseMaxMs = 0;
            ReadOnlySpan<TimeSpan> pauses = info.PauseDurations;
            if (pauses.Length > 0)
            {
                double sum = 0;
                for (int i = 0; i < pauses.Length; i++)
                {
                    double ms = pauses[i].TotalMilliseconds;
                    sum += ms;
                    if (i == pauses.Length - 1)
                        pauseLastMs = ms;
                    if (ms > pauseMaxMs)
                        pauseMaxMs = ms;
                }
                pauseAvgMs = sum / pauses.Length;
            }

            (double dlrLast, double dlrAvg, double dlrMax, _) = _dlrReadRateBuffer.Stats;
            (double dlrMsLast, double dlrMsAvg, double dlrMsMax, _) = _dlrReadMsBuffer.Stats;
            return new MemoryMetricsSnapshot(
                GetProcessWorkingSetMb(),
                GC.GetTotalMemory(false) / mb,
                info.GenerationInfo[0].SizeAfterBytes / mb,
                info.GenerationInfo[1].SizeAfterBytes / mb,
                info.GenerationInfo[2].SizeAfterBytes / mb,
                info.GenerationInfo[3].SizeAfterBytes / mb,
                info.FragmentedBytes / mb,
                loadPercent,
                pauseLastMs,
                pauseAvgMs,
                pauseMaxMs,
                info.PauseTimePercentage,
                dlrLast,
                dlrAvg,
                dlrMax,
                _dlrFailPercent,
                dlrMsLast,
                dlrMsAvg,
                dlrMsMax,
                GetDlrSectionSnapshot(),
                GetGcSectionSnapshot());
        }
    }
}
