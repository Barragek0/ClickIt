using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class PerformanceMonitorTests
    {
        [TestMethod]
        public void StopRenderTiming_EnqueuesTimingAndReportsLastAndAverage()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.StartRenderTiming();
            pm.StopRenderTiming();

            pm.RenderTimings.Count.Should().BeGreaterOrEqualTo(1);
            pm.GetLastTiming("render").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("render").Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void StartStopCoroutineTiming_Click_RecordsTiming()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.StartCoroutineTiming("click");
            pm.StopCoroutineTiming("click");

            pm.GetLastTiming("click").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("click").Should().BeGreaterOrEqualTo(0);
        }
    }
}
