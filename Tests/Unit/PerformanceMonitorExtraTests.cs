using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt;
using System.Threading;

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
            Thread.Sleep(10);
            pm.StopRenderTiming();

            pm.RenderTimings.Count.Should().BeGreaterThan(0);
            pm.GetLastTiming("render").Should().BeGreaterThan(0);
            pm.GetAverageTiming("render").Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public void CoroutineTiming_StartStop_RecordsLastAndMax()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            pm.StartCoroutineTiming("altar");
            Thread.Sleep(6);
            pm.StopCoroutineTiming("altar");

            pm.GetLastTiming("altar").Should().BeGreaterThan(0);
            pm.GetAverageTiming("altar").Should().BeGreaterOrEqualTo(0);
            pm.GetMaxTiming("altar").Should().BeGreaterOrEqualTo(pm.GetLastTiming("altar"));
        }

        [TestMethod]
        public void UpdateFPS_ComputesPositiveValue_AfterAtLeastOneSecond()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            // Call update once to ensure the internal timer starts, wait long enough for the update to compute
            pm.UpdateFPS();
            Thread.Sleep(1200);
            pm.UpdateFPS();

            pm.CurrentFPS.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void ShouldTriggerSecondTimerAction_TrueAfterInterval()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            Thread.Sleep(220);
            pm.ShouldTriggerSecondTimerAction(200).Should().BeTrue();
        }

        [TestMethod]
        public void RecordClickInterval_AddsIntervals_GetAverageWorks()
        {
            var pm = new PerformanceMonitor(new ClickItSettings());
            pm.Start();

            // call enough times to start recording intervals (skips first few)
            pm.RecordClickInterval();
            Thread.Sleep(5);
            pm.RecordClickInterval();
            Thread.Sleep(5);
            pm.RecordClickInterval();
            Thread.Sleep(5);
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
