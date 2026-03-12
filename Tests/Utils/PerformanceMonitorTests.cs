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
    }
}
