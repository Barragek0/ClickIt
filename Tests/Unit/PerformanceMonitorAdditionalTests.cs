using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class PerformanceMonitorAdditionalTests
    {
        [TestMethod]
        public void ShouldTriggerSecondTimerAction_RestartsAndReturnsTrue_WhenIntervalZero()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.Start();
            // Use -1 so any elapsedMilliseconds (>= 0) will be greater than the interval
            bool ok = pm.ShouldTriggerSecondTimerAction(-1);
            ok.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldTriggerMainTimerAction_WithSmallInterval_BehavesConsistently()
        {
            var settings = new ClickItSettings();

            var pm = new PerformanceMonitor(settings);
            pm.Start();
            // using -1 ensures a true result without blocking/sleeping
            pm.ShouldTriggerMainTimerAction(-1).Should().BeTrue();
        }

        [TestMethod]
        public void HotkeyReleaseTimer_StartStop_TimeoutBehavior()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.StartHotkeyReleaseTimer();
            pm.IsHotkeyReleaseTimeoutExceeded(5000).Should().BeFalse();

            pm.StopHotkeyReleaseTimer();
            pm.IsHotkeyReleaseTimeoutExceeded(5000).Should().BeTrue();
        }

        [TestMethod]
        public void RecordSuccessfulClickTiming_AffectsAverage()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.RecordSuccessfulClickTiming(50);
            pm.RecordSuccessfulClickTiming(150);

            pm.GetAverageSuccessfulClickTiming().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void GetClickTargetInterval_ReturnsSettingValue()
        {
            var settings = new ClickItSettings();
            settings.ClickFrequencyTarget.Value = 123;
            var pm = new PerformanceMonitor(settings);

            pm.GetClickTargetInterval().Should().BeApproximately(123.0, 0.001);
        }
    }
}
