using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System.Threading;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class PerformanceMonitorMoreTests
    {
        [TestMethod]
        public void Timings_AllKeys_AreTrackedAndMaxReported()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.Start();

            // test multiple coroutine keys
            var keys = new[] { "click", "altar", "flare", "shrine" };
            foreach (var k in keys)
            {
                pm.StartCoroutineTiming(k);
                Thread.Sleep(1);
                pm.StopCoroutineTiming(k);
                pm.GetLastTiming(k).Should().BeGreaterOrEqualTo(0);
                pm.GetAverageTiming(k).Should().BeGreaterOrEqualTo(0);
                pm.GetMaxTiming(k).Should().BeGreaterOrEqualTo(0);
            }
        }

        [TestMethod]
        public void HotkeyReleaseTimer_Behaviour_StartStopAndTimeout()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.StartHotkeyReleaseTimer();
            pm.IsHotkeyReleaseTimeoutExceeded(5000).Should().BeFalse();
            pm.StopHotkeyReleaseTimer();
            pm.IsHotkeyReleaseTimeoutExceeded(1).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldTriggerMainTimerAction_Works_AfterReset()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.ResetMainTimer();
            // Immediately after reset should usually be false for a small interval
            var triggered = pm.ShouldTriggerMainTimerAction(10000);
            triggered.Should().BeFalse();
        }
    }
}
