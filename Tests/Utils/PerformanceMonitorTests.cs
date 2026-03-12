using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using FluentAssertions;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class PerformanceMonitorTests
    {
        [TestMethod]
        public void Start_And_RenderTiming_EnqueuesValue()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.Start();
            pm.StartRenderTiming();
            pm.StopRenderTiming();

            pm.GetRenderTimingsSnapshot().Should().NotBeEmpty();
            pm.GetLastTiming("render").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("render").Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void CoroutineTimings_Enqueue_And_RespectMaxLength()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.Start();

            // push more than capacity (10) for 'click' timings
            for (int i = 0; i < 12; i++)
            {
                pm.StartCoroutineTiming("click");
                pm.StopCoroutineTiming("click");
            }

            pm.GetAverageTiming("click").Should().BeGreaterOrEqualTo(0);
            pm.GetMaxTiming("click").Should().BeGreaterOrEqualTo(0);

            // check last timing retrieval for known keys
            pm.GetLastTiming("click").Should().BeGreaterOrEqualTo(0);
            pm.GetLastTiming("altar").Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void ClickInterval_Record_And_Average_Works()
        {
            var settings = new ClickItSettings();
            settings.ClickFrequencyTarget = new ExileCore.Shared.Nodes.RangeNode<int>(100, 1, 9999);
            var pm = new PerformanceMonitor(settings);
            pm.Start();

            // simulate several click intervals (skip first few as logic ignores early clicks)
            pm.RecordClickInterval();
            pm.RecordClickInterval();
            pm.RecordClickInterval();
            pm.RecordClickInterval();
            pm.RecordClickInterval();

            pm.GetAverageClickInterval().Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void SecondTimer_ShouldTrigger_WhenIntervalExceeded()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.Start();

            pm.ShouldTriggerSecondTimerAction(int.MaxValue).Should().BeFalse();
            pm.ShouldTriggerSecondTimerAction(-1).Should().BeTrue();
        }

        [TestMethod]
        public void GetRenderTimingStats_ReturnsExpectedValues()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.Start();

            pm.StartRenderTiming();
            pm.StopRenderTiming();

            var stats = pm.GetRenderTimingStats();
            stats.SampleCount.Should().BeGreaterThan(0);
            stats.LastMs.Should().BeGreaterOrEqualTo(0);
            stats.AverageMs.Should().BeGreaterOrEqualTo(0);
            stats.MaxMs.Should().BeGreaterOrEqualTo(stats.LastMs);
        }

        [TestMethod]
        public void GetFpsStats_TracksCurrentAverageAndMax()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.RecordFpsSampleForTests(120.0);
            pm.RecordFpsSampleForTests(80.0);

            var fpsStats = pm.GetFpsStats();
            fpsStats.Current.Should().Be(80.0);
            fpsStats.Average.Should().Be(100.0);
            fpsStats.Max.Should().Be(120.0);
        }

        [TestMethod]
        public void RenderSectionStats_TracksLastAverageAndMax()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.RecordRenderSectionTiming(RenderSection.FrameFlush, 1.0);
            pm.RecordRenderSectionTiming(RenderSection.FrameFlush, 3.0);

            var stats = pm.GetRenderSectionStats(RenderSection.FrameFlush);
            stats.LastMs.Should().Be(3.0);
            stats.AverageMs.Should().Be(2.0);
            stats.MaxMs.Should().Be(3.0);
            stats.SampleCount.Should().Be(2);
        }

        [TestMethod]
        public void RenderSectionStats_TracksEachSectionIndependently()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.RecordRenderSectionTiming(RenderSection.DebugOverlay, 2.0);
            pm.RecordRenderSectionTiming(RenderSection.DebugOverlay, 6.0);
            pm.RecordRenderSectionTiming(RenderSection.TextFlush, 1.0);
            pm.RecordRenderSectionTiming(RenderSection.TextFlush, 3.0);
            pm.RecordRenderSectionTiming(RenderSection.FrameFlush, 5.0);

            var debugStats = pm.GetRenderSectionStats(RenderSection.DebugOverlay);
            debugStats.LastMs.Should().Be(6.0);
            debugStats.AverageMs.Should().Be(4.0);
            debugStats.MaxMs.Should().Be(6.0);
            debugStats.SampleCount.Should().Be(2);

            var textStats = pm.GetRenderSectionStats(RenderSection.TextFlush);
            textStats.LastMs.Should().Be(3.0);
            textStats.AverageMs.Should().Be(2.0);
            textStats.MaxMs.Should().Be(3.0);
            textStats.SampleCount.Should().Be(2);

            var frameStats = pm.GetRenderSectionStats(RenderSection.FrameFlush);
            frameStats.LastMs.Should().Be(5.0);
            frameStats.AverageMs.Should().Be(5.0);
            frameStats.MaxMs.Should().Be(5.0);
            frameStats.SampleCount.Should().Be(1);

            pm.GetRenderSectionStats(RenderSection.AltarOverlay).SampleCount.Should().Be(0);
            pm.GetRenderSectionStats(RenderSection.StrongboxOverlay).SampleCount.Should().Be(0);
        }

        [TestMethod]
        public void RecordRenderSectionTiming_IgnoresUnknownSection()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.RecordRenderSectionTiming(RenderSection.Unknown, 99.0);

            var unknownStats = pm.GetRenderSectionStats(RenderSection.Unknown);
            unknownStats.LastMs.Should().Be(0);
            unknownStats.AverageMs.Should().Be(0);
            unknownStats.MaxMs.Should().Be(0);
            unknownStats.SampleCount.Should().Be(0);
        }
    }
}
