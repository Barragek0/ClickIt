using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System.Threading;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class PerformanceMonitorExtraTests
    {
        [TestMethod]
        public void StartStopCoroutineTiming_FlareAndShrine_ProduceTimings()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.Start();
            pm.StartCoroutineTiming("flare");
            Thread.Sleep(1);
            pm.StopCoroutineTiming("flare");

            pm.StartCoroutineTiming("shrine");
            Thread.Sleep(1);
            pm.StopCoroutineTiming("shrine");

            pm.GetLastTiming("flare").Should().BeGreaterOrEqualTo(0);
            pm.GetLastTiming("shrine").Should().BeGreaterOrEqualTo(0);

            pm.GetAverageTiming("flare").Should().BeGreaterOrEqualTo(0);
            pm.GetAverageTiming("shrine").Should().BeGreaterOrEqualTo(0);
        }

        [TestMethod]
        public void GetAverageTiming_ReturnsZero_ForUnknownKey()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);
            pm.GetAverageTiming("unknown").Should().Be(0);
        }
    }
}
