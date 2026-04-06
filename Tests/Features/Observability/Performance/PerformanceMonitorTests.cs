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