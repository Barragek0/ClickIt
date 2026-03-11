using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt;
using System.Threading;
using System.Diagnostics;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class PerformanceMonitorExtraTests
    {
        [TestMethod]
        public void StartRenderTiming_StopRenderTiming_UpdatesQueuesAndLastTiming()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            pm.StartRenderTiming();
            pm.StopRenderTiming();

            pm.RenderTimings.Count.Should().BeGreaterThan(0);
            pm.GetLastTiming("render").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("render").Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public void CoroutineTiming_StartStop_RecordsLastAndMax()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            pm.StartCoroutineTiming("altar");
            pm.StopCoroutineTiming("altar");

            pm.GetLastTiming("altar").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("altar").Should().BeGreaterOrEqualTo(0);
            pm.GetMaxTiming("altar").Should().BeGreaterOrEqualTo(pm.GetLastTiming("altar"));
        }

        [TestMethod]
        public void UpdateFPS_ComputesPositiveValue_AfterAtLeastOneSecond()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            // Poll until the internal 1-second FPS window elapses to avoid fixed sleep flakiness.
            pm.UpdateFPS();
            Stopwatch timeout = Stopwatch.StartNew();
            while (pm.CurrentFPS <= 0 && timeout.ElapsedMilliseconds < 3000)
            {
                SpinWait.SpinUntil(static () => false, 25);
                pm.UpdateFPS();
            }

            pm.CurrentFPS.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void ShouldTriggerSecondTimerAction_TrueAfterInterval()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            pm.ShouldTriggerSecondTimerAction(int.MaxValue).Should().BeFalse();
            pm.ShouldTriggerSecondTimerAction(-1).Should().BeTrue();
        }

        [TestMethod]
        public void RecordClickInterval_AddsIntervals_GetAverageWorks()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            // call enough times to start recording intervals (skips first few)
            pm.RecordClickInterval();
            pm.RecordClickInterval();
            pm.RecordClickInterval();
            pm.RecordClickInterval();

            pm.GetAverageClickInterval().Should().BeGreaterThanOrEqualTo(0);

            pm.ResetClickCount();
            pm.RecordClickInterval();
            // After reset the counter is 1 so still no interval recorded
            pm.GetAverageClickInterval().Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public void SuccessfulClickTiming_RecordsAndAverages()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.RecordSuccessfulClickTiming(100);
            pm.RecordSuccessfulClickTiming(200);

            pm.GetAverageSuccessfulClickTiming().Should().BeInRange(100, 200);
        }
    }
}
