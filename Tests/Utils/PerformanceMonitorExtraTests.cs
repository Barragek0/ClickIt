using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class PerformanceMonitorExtraTests
    {
        [TestMethod]
        public void StartStopCoroutineTiming_Flare_ProducesTimings()
        {
            var settings = new ClickItSettings();
            var pm = new PerformanceMonitor(settings);

            pm.Start();
            pm.StartCoroutineTiming("flare");
            pm.StopCoroutineTiming("flare");

            pm.GetLastTiming("flare").Should().BeGreaterOrEqualTo(0);

            pm.GetAverageTiming("flare").Should().BeGreaterOrEqualTo(0);
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
