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
            total.MaxMs.Should().BeApproximately(SystemMath.Max(20.0 / 6, 30.0 / 3), 0.001);
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

            // 70 samples of 1.0 fill and then roll the 60-sample window, then one 2.0 sample.
            for (int i = 0; i < 70; i++)
                monitor.RecordRenderSectionTiming(RenderSection.DebugOverlay, 1.0);
            monitor.RecordRenderSectionTiming(RenderSection.DebugOverlay, 2.0);

            (double LastMs, double AverageMs, double MaxMs, long SampleCount) stats = monitor.GetRenderSectionStats(RenderSection.DebugOverlay);
            stats.SampleCount.Should().Be(60);
            stats.LastMs.Should().Be(2.0);
            stats.MaxMs.Should().Be(2.0);
            // Window holds 59×1.0 + 1×2.0.
            stats.AverageMs.Should().BeApproximately(61.0 / 60.0, 0.001);
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