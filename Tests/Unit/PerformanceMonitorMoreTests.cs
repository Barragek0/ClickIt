using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    [Ignore("Consolidated into PerformanceMonitorTests.cs")]
    public class PerformanceMonitorMoreTests
    {
        [TestMethod]
        public void StartAndTimers_BasicFlows_Work()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.Start();
            // Timers are started; calling these should be safe and return a bool
            pm.ShouldTriggerMainTimerAction(0); // may be true or false depending on timing
            pm.ShouldTriggerSecondTimerAction(0); // may be true or false depending on timing

            pm.StartRenderTiming();
            pm.StopRenderTiming();
            pm.GetLastTiming("render").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("render").Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void CoroutineTiming_AttachAndStop_ForAllTypes()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.StartCoroutineTiming("click");
            pm.StopCoroutineTiming("click");
            pm.GetLastTiming("click").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("click").Should().BeGreaterOrEqualTo(0);
            pm.GetMaxTiming("click").Should().BeGreaterOrEqualTo(0);

            pm.StartCoroutineTiming("altar");
            pm.StopCoroutineTiming("altar");
            pm.GetLastTiming("altar").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("altar").Should().BeGreaterOrEqualTo(0);
            pm.GetMaxTiming("altar").Should().BeGreaterOrEqualTo(0);

            pm.StartCoroutineTiming("flare");
            pm.StopCoroutineTiming("flare");
            pm.GetLastTiming("flare").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("flare").Should().BeGreaterOrEqualTo(0);

            pm.StartCoroutineTiming("shrine");
            pm.StopCoroutineTiming("shrine");
            pm.GetLastTiming("shrine").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("shrine").Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void HotkeyTimers_ResetAndTimeout_Behaviour()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.StartHotkeyReleaseTimer();
            pm.IsHotkeyReleaseTimeoutExceeded(5000).Should().BeFalse();

            pm.StopHotkeyReleaseTimer();
            // after stop, IsHotkeyReleaseTimeoutExceeded should be true (timer not running)
            pm.IsHotkeyReleaseTimeoutExceeded(5000).Should().BeTrue();
        }

        [TestMethod]
        public void ClickInterval_RecordAndAverage_Works()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.Start();
            // call RecordClickInterval several times to trigger queueing
            for (int i = 0; i < 6; i++)
            {
                pm.RecordClickInterval();
            }

            // moving average should be zero or positive (depending on timings) but call should not throw
            pm.GetAverageClickInterval().Should().BeGreaterOrEqualTo(0);

            // Reset and ensure average is zero when no entries
            pm.ResetClickCount();
            // Add a successful click timing and assert the average
            pm.RecordSuccessfulClickTiming(100);
            pm.GetAverageSuccessfulClickTiming().Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void GetClickTargetInterval_ReturnsSettingsValue()
        {
            var settings = new ClickItSettings();
            settings.ClickFrequencyTarget.Value = 123;
            var pm = new PerformanceMonitor(settings);

            pm.GetClickTargetInterval().Should().Be(123);
        }
    }
}
