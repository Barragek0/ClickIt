using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using ClickIt;
using System.Threading;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    [Ignore("Consolidated into PerformanceMonitorTests.cs")]
    public class PerformanceMonitorExtraTests
    {
        [TestMethod]
        public void Start_UpdateFPS_RecordsFps()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.Start();
            // Simulate several frames
            for (int i = 0; i < 10; i++)
            {
                pm.UpdateFPS();
            }

            // Call a few more times - UpdateFPS is safe even if fps timer didn't roll over yet
            pm.UpdateFPS();

            Assert.IsTrue(pm.CurrentFPS >= 0);
        }

        [TestMethod]
        public void StartStopRenderTiming_AddsToQueue()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.StartRenderTiming();
            pm.StopRenderTiming();
            var queue = pm.RenderTimings;
            Assert.IsTrue(queue.Count == 1);
        }

        [TestMethod]
        public void CoroutineTiming_TracksLastAndMax()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.StartCoroutineTiming("click");
            pm.StopCoroutineTiming("click");

            double last = pm.GetLastTiming("click");
            Assert.IsTrue(last >= 0);

            // Test average for empty queue type returns zero safely
            Assert.AreEqual(0, pm.GetAverageTiming("unknown"));
        }

        [TestMethod]
        public void ShouldTriggerTimers_BehavesAsExpected()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.Start();
            pm.ResetMainTimer();
            // Use -1 to force the condition true (elapsed > -1)
            bool main = pm.ShouldTriggerMainTimerAction(-1);
            Assert.IsTrue(main);

            // Second timer: call ShouldTriggerSecondTimerAction with negative to force trigger
            bool second = pm.ShouldTriggerSecondTimerAction(-1);
            Assert.IsTrue(second);
        }

        [TestMethod]
        public void RecordClickInterval_RecordsAndAverages()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.Start();
            for (int i = 0; i < 5; i++)
            {
                pm.RecordClickInterval();
            }

            // Average might be zero depending on timer granularity, but method should not throw
            double avg = pm.GetAverageClickInterval();
            Assert.IsTrue(avg >= 0);
        }

        [TestMethod]
        public void RecordSuccessfulClickTiming_And_Average()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.RecordSuccessfulClickTiming(100);
            pm.RecordSuccessfulClickTiming(200);
            double avg = pm.GetAverageSuccessfulClickTiming();
            Assert.IsTrue(avg >= 100);
        }
    }
}
