using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Tests.Harness;

namespace ClickIt.Tests.Core
{
    [TestClass]
    public class ClickItLogMessageTests
    {
        [TestMethod]
        public void LogMessageBool_DoesNotRecurse_WhenNotRendering()
        {
            var clickIt = new ClickIt();
            ClickItHostHarness.SetSettings(clickIt, new ClickItSettings());

            clickIt.State.Rendering.IsRendering = false;

            var alertService = clickIt.GetAlertService();
            var countBefore = alertService.LastAlertTimes.Count;

            // Use localDebug=false to avoid reading Settings which may be null in some test harness scenarios
            clickIt.LogMessage(false, "test-message", 0);

            alertService.LastAlertTimes.Count.Should().Be(countBefore);
        }

        [TestMethod]
        public void LogMessageBool_Skips_WhenRendering()
        {
            var clickIt = new ClickIt();
            ClickItHostHarness.SetSettings(clickIt, new ClickItSettings());

            clickIt.State.Rendering.IsRendering = true;

            var alertService = clickIt.GetAlertService();
            var countBefore = alertService.LastAlertTimes.Count;

            clickIt.LogMessage(false, "should-not-log", 0);

            alertService.LastAlertTimes.Count.Should().Be(countBefore);
        }
    }
}
