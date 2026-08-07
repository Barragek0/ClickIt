namespace ClickIt.Tests.Features.Observability.Performance
{
    [TestClass]
    public class PerformanceMonitorTests
    {
        [TestMethod]
        public void TimerActions_RespectMainAndSecondIntervals_AndResetWhenTriggered()
        {
            var settings = new ClickItSettings();
            var monitor = new PerformanceMonitor(settings);

            monitor.Start();

            monitor.ShouldTriggerMainTimerAction(50).Should().BeFalse();
            monitor.ShouldTriggerSecondTimerAction(50).Should().BeFalse();

            Thread.Sleep(20);

            monitor.ShouldTriggerMainTimerAction(1).Should().BeTrue();
            monitor.ShouldTriggerSecondTimerAction(1).Should().BeTrue();
            monitor.ShouldTriggerSecondTimerAction(50).Should().BeFalse();

            monitor.ResetMainTimer();

            monitor.ShouldTriggerMainTimerAction(50).Should().BeFalse();
        }

        [TestMethod]
        public void HotkeyReleaseTimer_TracksTimeoutState_BeforeStartDuringRunAndAfterStop()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.IsHotkeyReleaseTimeoutExceeded().Should().BeTrue();

            monitor.StartHotkeyReleaseTimer();

            monitor.IsHotkeyReleaseTimeoutExceeded(5000).Should().BeFalse();

            Thread.Sleep(20);

            monitor.IsHotkeyReleaseTimeoutExceeded(1).Should().BeTrue();

            monitor.StopHotkeyReleaseTimer();

            monitor.IsHotkeyReleaseTimeoutExceeded(5000).Should().BeTrue();
        }

        [TestMethod]
        public void ClickAndSuccessfulClickTiming_AggregateIntoExpectedAverages()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.Start();

            for (int index = 0; index < 4; index++)
            {
                Thread.Sleep(5);
                monitor.RecordClickInterval();
            }

            monitor.RecordSuccessfulClickTiming(10);
            monitor.RecordSuccessfulClickTiming(20);
            monitor.RecordSuccessfulClickTiming(30);

            monitor.GetAverageClickInterval().Should().BeGreaterThan(0);
            monitor.GetAverageSuccessfulClickTiming().Should().Be(20);
        }

        [TestMethod]
        public void RenderAndFpsRecording_FeedSectionStatsTimingStatsAndDebugSnapshot()
        {
            var settings = new ClickItSettings();
            settings.ClickFrequencyTarget.Value = 123;

            var monitor = new PerformanceMonitor(settings);

            monitor.RecordFpsSample(144);
            monitor.RecordRenderSectionTiming(RenderSection.LazyMode, 7.5);
            monitor.RecordRenderSectionTiming(RenderSection.DebugOverlay, 4.5);

            monitor.StartRenderTiming();
            Thread.Sleep(5);
            monitor.StopRenderTiming();

            monitor.StartCoroutineTiming(TimingChannel.Click);
            Thread.Sleep(5);
            monitor.StopCoroutineTiming(TimingChannel.Click);

            monitor.RecordSuccessfulClickTiming(18);
            monitor.Start();

            for (int index = 0; index < 4; index++)
            {
                Thread.Sleep(3);
                monitor.RecordClickInterval();
            }

            var lazyModeStats = monitor.GetRenderSectionStats(RenderSection.LazyMode);
            var renderStats = monitor.GetRenderTimingStats();
            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();

            lazyModeStats.LastMs.Should().Be(7.5);
            lazyModeStats.MaxMs.Should().Be(7.5);
            lazyModeStats.SampleCount.Should().Be(1);
            renderStats.SampleCount.Should().Be(1);
            renderStats.LastMs.Should().BeGreaterThanOrEqualTo(0);

            snapshot.Fps.Current.Should().Be(144);
            snapshot.ClickTargetIntervalMs.Should().Be(123);
            snapshot.GetRenderSection(RenderSection.LazyMode).LastMs.Should().Be(7.5);
            snapshot.GetRenderSection(RenderSection.DebugOverlay).LastMs.Should().Be(4.5);
            snapshot.GetCoroutineTiming(TimingChannel.Click).SampleCount.Should().Be(1);
            snapshot.Render.SampleCount.Should().Be(1);
            snapshot.AverageSuccessfulClickTimingMs.Should().Be(18);
            snapshot.AverageClickIntervalMs.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void RenderSection_AverageCoversWholeWindow_MaxOverWholeWindow()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            for (int i = 0; i < 10; i++)
                monitor.RecordRenderSectionTiming(RenderSection.BlightOverlay, 10.0);
            for (int i = 0; i < 20; i++)
                monitor.RecordRenderSectionTiming(RenderSection.BlightOverlay, 1.0);

            (double LastMs, double AverageMs, double MaxMs, long SampleCount) stats = monitor.GetRenderSectionStats(RenderSection.BlightOverlay);

            stats.LastMs.Should().Be(1.0);
            stats.AverageMs.Should().BeApproximately(4.0, 0.001, "the average covers the last 100 samples");
            stats.MaxMs.Should().Be(10.0, "the spike is still inside the 1000-sample max window");
            stats.SampleCount.Should().Be(30);
        }

        [TestMethod]
        public void RenderSection_MaxRollsOutAfter1000Samples()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            for (int i = 0; i < 10; i++)
                monitor.RecordRenderSectionTiming(RenderSection.BlightOverlay, 10.0);
            for (int i = 0; i < 1000; i++)
                monitor.RecordRenderSectionTiming(RenderSection.BlightOverlay, 1.0);

            (double LastMs, double AverageMs, double MaxMs, long SampleCount) stats = monitor.GetRenderSectionStats(RenderSection.BlightOverlay);

            stats.MaxMs.Should().Be(1.0, "the 10ms spike rolled out of the 1000-sample window");
            stats.AverageMs.Should().Be(1.0);
            stats.SampleCount.Should().Be(1000);
        }

        [TestMethod]
        public void RenderSectionTiming_HarvestAndBlight_FlowThroughStoreAndSnapshot()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.RecordRenderSectionTiming(RenderSection.HarvestOverlay, 3.2);
            monitor.RecordRenderSectionTiming(RenderSection.BlightOverlay, 5.4);

            (double LastMs, double AverageMs, double MaxMs, long SampleCount) harvestStats = monitor.GetRenderSectionStats(RenderSection.HarvestOverlay);
            (double LastMs, double AverageMs, double MaxMs, long SampleCount) blightStats = monitor.GetRenderSectionStats(RenderSection.BlightOverlay);
            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();

            harvestStats.LastMs.Should().Be(3.2);
            harvestStats.SampleCount.Should().Be(1);
            blightStats.LastMs.Should().Be(5.4);
            blightStats.SampleCount.Should().Be(1);

            snapshot.GetRenderSection(RenderSection.HarvestOverlay).LastMs.Should().Be(3.2);
            snapshot.GetRenderSection(RenderSection.HarvestOverlay).SampleCount.Should().Be(1);
            snapshot.GetRenderSection(RenderSection.BlightOverlay).LastMs.Should().Be(5.4);
            snapshot.GetRenderSection(RenderSection.BlightOverlay).SampleCount.Should().Be(1);
        }

        [TestMethod]
        public void ProcessingTiming_FlowsThroughStoreAndSnapshot()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.RecordProcessingTiming(ProcessingSection.Label, 4.0);
            monitor.RecordProcessingTiming(ProcessingSection.Label, 6.0);
            monitor.RecordProcessingTiming(ProcessingSection.Pathfinding, 20.0);

            (double lastMs, double avgMs, double maxMs, long count, double _) = monitor.GetProcessingStats(ProcessingSection.Label);
            lastMs.Should().Be(6.0);
            avgMs.Should().Be(5.0);
            maxMs.Should().Be(6.0);
            count.Should().Be(2);

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();
            snapshot.GetProcessingSection(ProcessingSection.Label).LastMs.Should().Be(6.0);
            snapshot.GetProcessingSection(ProcessingSection.Label).SampleCount.Should().Be(2);
            snapshot.GetProcessingSection(ProcessingSection.Pathfinding).LastMs.Should().Be(20.0);
            snapshot.GetProcessingSection(ProcessingSection.Altar).SampleCount.Should().Be(0);
        }

        [TestMethod]
        public void ProcessingTotal_AggregatesLastAndAverageSumsAndMaxOfMaxes()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.RecordProcessingTiming(ProcessingSection.Blight, 10.0);
            monitor.RecordProcessingTiming(ProcessingSection.Click, 5.0);
            monitor.RecordProcessingTiming(ProcessingSection.Ultimatum, 15.0);

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();

            snapshot.ProcessingTotal.SampleCount.Should().Be(3);
            snapshot.ProcessingTotal.LastMs.Should().BeApproximately(
                snapshot.GetProcessingSection(ProcessingSection.Blight).LastMs
                + snapshot.GetProcessingSection(ProcessingSection.Click).LastMs
                + snapshot.GetProcessingSection(ProcessingSection.Ultimatum).LastMs, 0.01);
            snapshot.ProcessingTotal.AverageMs.Should().BeApproximately(
                snapshot.GetProcessingSection(ProcessingSection.Blight).AverageMs
                + snapshot.GetProcessingSection(ProcessingSection.Click).AverageMs
                + snapshot.GetProcessingSection(ProcessingSection.Ultimatum).AverageMs, 0.01);
            snapshot.ProcessingTotal.MaxMs.Should().Be(15.0);
        }

        [TestMethod]
        public void ProcessingTotal_IsZero_WhenNoSectionHasSamples()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();

            snapshot.ProcessingTotal.SampleCount.Should().Be(0);
            snapshot.ProcessingTotal.LastMs.Should().Be(0);
            snapshot.ProcessingTotal.AverageMs.Should().Be(0);
            snapshot.ProcessingTotal.MaxMs.Should().Be(0);
        }

        [TestMethod]
        public void NewRenderSections_FlowThroughStoreAndSnapshot()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.RecordRenderSectionTiming(RenderSection.ClickHotkeyToggle, 1.1);
            monitor.RecordRenderSectionTiming(RenderSection.InventoryFullWarning, 2.2);
            monitor.RecordRenderSectionTiming(RenderSection.UiRegionRectangle, 3.3);
            monitor.RecordRenderSectionTiming(RenderSection.PerformanceOverlay, 4.4);

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();

            snapshot.GetRenderSection(RenderSection.ClickHotkeyToggle).LastMs.Should().Be(1.1);
            snapshot.GetRenderSection(RenderSection.ClickHotkeyToggle).SampleCount.Should().Be(1);
            snapshot.GetRenderSection(RenderSection.InventoryFullWarning).LastMs.Should().Be(2.2);
            snapshot.GetRenderSection(RenderSection.UiRegionRectangle).LastMs.Should().Be(3.3);
            snapshot.GetRenderSection(RenderSection.PerformanceOverlay).LastMs.Should().Be(4.4);
        }

        [TestMethod]
        public void Allocations_FlowThroughStoreAndSnapshot()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.RecordAllocation(ProcessingSection.Label, 2048);
            monitor.RecordAllocation(ProcessingSection.Pathfinding, 8192);

            GcAllocationSnapshot labelStats = monitor.GetAllocationStats(ProcessingSection.Label);
            labelStats.SampleCount.Should().Be(1);
            labelStats.AvgBytesPerRun.Should().Be(2048);

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();
            snapshot.GetAllocationSection(ProcessingSection.Label).AvgBytesPerRun.Should().Be(2048);
            snapshot.GetAllocationSection(ProcessingSection.Pathfinding).AvgBytesPerRun.Should().Be(8192);
            snapshot.GetAllocationSection(ProcessingSection.Blight).SampleCount.Should().Be(0);
        }

        [TestMethod]
        public void LabelScanAllocation_FlowsThroughStoreAndSnapshot()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.RecordLabelScanAllocation(new LabelScanAllocationBreakdown(
                ListReadBytes: 1024 * 1024, ListAllocBytes: 4096, ValidityBytes: 32768, SortBytes: 2048, TotalBytes: 1024 * 1024 + 4096 + 32768 + 2048));
            monitor.RecordLabelScanAllocation(new LabelScanAllocationBreakdown(
                ListReadBytes: 2 * 1024 * 1024, ListAllocBytes: 8192, ValidityBytes: 65536, SortBytes: 1024, TotalBytes: 2 * 1024 * 1024 + 8192 + 65536 + 1024));

            LabelScanAllocationStats stats = monitor.GetLabelScanAllocationStats();
            stats.SampleCount.Should().Be(2);
            stats.ListRead.AvgBytesPerRun.Should().Be((1024 * 1024 + 2 * 1024 * 1024) / 2.0);
            stats.ListRead.MaxBytesPerRun.Should().Be(2 * 1024 * 1024);
            stats.ListRead.LastBytesPerRun.Should().Be(2 * 1024 * 1024);
            stats.Validity.AvgBytesPerRun.Should().Be((32768 + 65536) / 2.0);

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();
            snapshot.LabelScanAllocation.SampleCount.Should().Be(2);
            snapshot.LabelScanAllocation.ListRead.AvgBytesPerRun.Should().Be((1024 * 1024 + 2 * 1024 * 1024) / 2.0);
            snapshot.LabelScanAllocation.Sort.MaxBytesPerRun.Should().Be(2048);
        }

        [TestMethod]
        public void ClickAllocation_FlowsThroughStoreAndSnapshot()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.RecordClickAllocation(new ClickAllocationBreakdown(
                ContextBytes: 2048, AcquireBytes: 1024 * 1024, RankBytes: 32768, ExecuteBytes: 65536, PostBytes: 1024, OtherBytes: 8192, TotalBytes: 2048 + 1024 * 1024 + 32768 + 65536 + 1024 + 8192));
            monitor.RecordClickAllocation(new ClickAllocationBreakdown(
                ContextBytes: 4096, AcquireBytes: 2 * 1024 * 1024, RankBytes: 16384, ExecuteBytes: 131072, PostBytes: 0, OtherBytes: 16384, TotalBytes: 4096 + 2 * 1024 * 1024 + 16384 + 131072 + 16384));

            ClickAllocationStats stats = monitor.GetClickAllocationStats();
            stats.SampleCount.Should().Be(2);
            stats.Acquire.AvgBytesPerRun.Should().Be((1024 * 1024 + 2 * 1024 * 1024) / 2.0);
            stats.Acquire.LastBytesPerRun.Should().Be(2 * 1024 * 1024);
            stats.Other.AvgBytesPerRun.Should().Be((8192 + 16384) / 2.0);

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();
            snapshot.ClickAllocation.SampleCount.Should().Be(2);
            snapshot.ClickAllocation.Execute.AvgBytesPerRun.Should().Be((65536 + 131072) / 2.0);
            snapshot.ClickAllocation.Rank.MaxBytesPerRun.Should().Be(32768);
            snapshot.ClickAllocation.Other.LastBytesPerRun.Should().Be(16384);
        }

        [TestMethod]
        public void MemorySnapshot_ReportsHeapAndProcessMetrics()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();

            snapshot.Memory.ProcessWorkingSetMb.Should().BeGreaterThan(0);
            snapshot.Memory.ManagedHeapMb.Should().BeGreaterThanOrEqualTo(0);
            snapshot.Memory.Gen0Mb.Should().BeGreaterThanOrEqualTo(0);
            snapshot.Memory.Gen2Mb.Should().BeGreaterThanOrEqualTo(0);
            snapshot.Memory.FragmentedMb.Should().BeGreaterThanOrEqualTo(0);
            snapshot.Memory.MemoryLoadPercent.Should().BeInRange(0, 100);
        }

        [TestMethod]
        public void CoroutinesTotal_AggregatesLastAndAverageSumsAndMaxOfMaxes_AcrossChannels()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.StartCoroutineTiming(TimingChannel.Altar);
            monitor.StopCoroutineTiming(TimingChannel.Altar);
            monitor.StartCoroutineTiming(TimingChannel.Click);
            monitor.StopCoroutineTiming(TimingChannel.Click);
            monitor.StartCoroutineTiming(TimingChannel.Ultimatum);
            monitor.StopCoroutineTiming(TimingChannel.Ultimatum);

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();

            snapshot.CoroutinesTotal.SampleCount.Should().Be(3);
            snapshot.CoroutinesTotal.LastMs.Should().BeApproximately(
                snapshot.AltarCoroutine.LastMs + snapshot.ClickCoroutine.LastMs + snapshot.UltimatumCoroutine.LastMs, 0.01);
            snapshot.CoroutinesTotal.AverageMs.Should().BeApproximately(
                snapshot.AltarCoroutine.AverageMs + snapshot.ClickCoroutine.AverageMs + snapshot.UltimatumCoroutine.AverageMs, 0.01);
            snapshot.CoroutinesTotal.MaxMs.Should().Be(
                SystemMath.Max(snapshot.AltarCoroutine.MaxMs, SystemMath.Max(snapshot.ClickCoroutine.MaxMs, snapshot.UltimatumCoroutine.MaxMs)));
            snapshot.CoroutinesTotal.SampleCount.Should().Be(
                snapshot.AltarCoroutine.SampleCount + snapshot.ClickCoroutine.SampleCount + snapshot.UltimatumCoroutine.SampleCount);
        }

        [TestMethod]
        public void CoroutinesTotal_IsZero_WhenNoCoroutineHasSamples()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            PerformanceMetricsSnapshot snapshot = monitor.GetDebugSnapshot();

            snapshot.CoroutinesTotal.SampleCount.Should().Be(0);
            snapshot.CoroutinesTotal.LastMs.Should().Be(0);
            snapshot.CoroutinesTotal.AverageMs.Should().Be(0);
            snapshot.CoroutinesTotal.MaxMs.Should().Be(0);
        }

        [TestMethod]
        public void CoroutinesTotalPerFrameSnapshot_ScalesEachChannelByItsOwnPeriod()
        {
            var fps = new FpsMetricsSnapshot(Current: 60, Average: 60, Max: 60);
            var altar = new TimingMetricsSnapshot(LastMs: 10, AverageMs: 5, MaxMs: 20, SampleCount: 10, AveragePeriodMs: 100);
            var click = new TimingMetricsSnapshot(LastMs: 20, AverageMs: 10, MaxMs: 30, SampleCount: 10, AveragePeriodMs: 50);
            PerformanceMetricsSnapshot snapshot = new(
                Fps: fps, Render: default, LazyMode: default, DebugOverlay: default,
                AltarOverlay: default, UltimatumOverlay: default, StrongboxOverlay: default,
                PathfindingOverlay: default, HarvestOverlay: default, BlightOverlay: default,
                TextFlush: default, FrameFlush: default,
                AltarCoroutine: altar, ClickCoroutine: click,
                FlareCoroutine: default, BlightCoroutine: default,
                UltimatumCoroutine: default, LabelOverlayCoroutine: default,
                ClickTargetIntervalMs: 0, AverageSuccessfulClickTimingMs: 0, AverageClickIntervalMs: 0);

            // altar scale = 1000/100/60 = 1/6; click scale = 1000/50/60 = 1/3.
            TimingMetricsSnapshot total = snapshot.CoroutinesTotalPerFrameSnapshot;
            total.SampleCount.Should().Be(2);
            total.LastMs.Should().BeApproximately(10.0 / 6 + 20.0 / 3, 0.001);
            total.AverageMs.Should().BeApproximately(5.0 / 6 + 10.0 / 3, 0.001);
            total.MaxMs.Should().BeApproximately(20.0 / 6 + 30.0 / 3, 0.001);
            snapshot.CoroutinesTotalPerFrame.Should().BeApproximately(5.0 / 6 + 10.0 / 3, 0.001);
        }

        [TestMethod]
        public void CoroutinesTotalPerFrameSnapshot_IsZero_WithoutRunPeriods()
        {
            var fps = new FpsMetricsSnapshot(Current: 60, Average: 60, Max: 60);
            var altar = new TimingMetricsSnapshot(LastMs: 10, AverageMs: 5, MaxMs: 20, SampleCount: 10);
            var click = new TimingMetricsSnapshot(LastMs: 20, AverageMs: 10, MaxMs: 30, SampleCount: 10);
            PerformanceMetricsSnapshot snapshot = new(
                Fps: fps, Render: default, LazyMode: default, DebugOverlay: default,
                AltarOverlay: default, UltimatumOverlay: default, StrongboxOverlay: default,
                PathfindingOverlay: default, HarvestOverlay: default, BlightOverlay: default,
                TextFlush: default, FrameFlush: default,
                AltarCoroutine: altar, ClickCoroutine: click,
                FlareCoroutine: default, BlightCoroutine: default,
                UltimatumCoroutine: default, LabelOverlayCoroutine: default,
                ClickTargetIntervalMs: 0, AverageSuccessfulClickTimingMs: 0, AverageClickIntervalMs: 0);

            TimingMetricsSnapshot total = snapshot.CoroutinesTotalPerFrameSnapshot;
            total.SampleCount.Should().Be(0);
            total.LastMs.Should().Be(0);
            total.AverageMs.Should().Be(0);
            total.MaxMs.Should().Be(0);
            snapshot.CoroutinesTotalPerFrame.Should().Be(0);
        }

        [TestMethod]
        public void TimingMetricsSnapshot_DutyCycleAndPerFrame_NormalizeByRunPeriod()
        {
            var snap = new TimingMetricsSnapshot(LastMs: 5, AverageMs: 2, MaxMs: 10, SampleCount: 10, AveragePeriodMs: 50);

            snap.DutyCyclePercent.Should().Be(4.0);
            snap.PerFrameScale(60).Should().BeApproximately(1000.0 / 50 / 60, 0.001);
            snap.PerFrameMs(60).Should().BeApproximately(2 * (1000.0 / 50) / 60, 0.001);
            snap.PerFrameMs(0).Should().Be(0);
        }

        [TestMethod]
        public void TimingMetricsSnapshot_DutyCycleAndPerFrame_AreZero_WithoutRunPeriod()
        {
            var snap = new TimingMetricsSnapshot(LastMs: 5, AverageMs: 2, MaxMs: 10, SampleCount: 10);

            snap.DutyCyclePercent.Should().Be(0);
            snap.PerFrameMs(60).Should().Be(0);
        }

        [TestMethod]
        public void RenderSection_Stats_UseRollingWindow_NotAllTimeCumulative()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            // 1000 samples of 1.0 fill and then roll the 1000-sample window, then one 2.0 sample.
            for (int i = 0; i < 1000; i++)
                monitor.RecordRenderSectionTiming(RenderSection.DebugOverlay, 1.0);
            monitor.RecordRenderSectionTiming(RenderSection.DebugOverlay, 2.0);

            (double LastMs, double AverageMs, double MaxMs, long SampleCount) stats = monitor.GetRenderSectionStats(RenderSection.DebugOverlay);
            stats.SampleCount.Should().Be(1000);
            stats.LastMs.Should().Be(2.0);
            stats.MaxMs.Should().Be(2.0);
            // Max window holds 999×1.0 + 1×2.0; average covers the last 100 samples.
            stats.AverageMs.Should().BeApproximately(1.01, 0.001);
        }

        [TestMethod]
        public void ShutdownForHotReload_ClearsRecordedMetrics()
        {
            var monitor = new PerformanceMonitor(new ClickItSettings());

            monitor.RecordFpsSample(120);
            monitor.RecordRenderSectionTiming(RenderSection.FrameFlush, 9);
            monitor.StartRenderTiming();
            Thread.Sleep(5);
            monitor.StopRenderTiming();
            monitor.RecordSuccessfulClickTiming(25);
            monitor.Start();
            monitor.StartHotkeyReleaseTimer();

            for (int index = 0; index < 4; index++)
            {
                Thread.Sleep(3);
                monitor.RecordClickInterval();
            }

            monitor.ShutdownForHotReload();

            monitor.GetRenderTimingStats().SampleCount.Should().Be(0);
            monitor.GetRenderSectionStats(RenderSection.FrameFlush).SampleCount.Should().Be(1);
            monitor.GetAverageSuccessfulClickTiming().Should().Be(0);
            monitor.GetAverageClickInterval().Should().Be(0);
            monitor.IsHotkeyReleaseTimeoutExceeded().Should().BeTrue();
            monitor.GetDebugSnapshot().Render.SampleCount.Should().Be(0);
        }
    }
}