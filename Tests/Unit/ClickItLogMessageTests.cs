using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItLogMessageTests
    {
        [TestMethod]
        public void LogMessageBool_DoesNotRecurse_WhenNotRendering()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            // Previously this path recursed - ensure it completes (no exception).
            clickIt.State.IsRendering = false;

            // No exception should be thrown, this verifies the method forwards correctly.
            var alertService = clickIt.__Test_GetAlertService();
            var countBefore = alertService.LastAlertTimes.Count;

            // Use localDebug=false to avoid reading Settings which may be null in some test harness scenarios
            clickIt.LogMessage(false, "test-message", 0);

            alertService.LastAlertTimes.Count.Should().Be(countBefore);
        }

        [TestMethod]
        public void LogMessageBool_Skips_WhenRendering()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            clickIt.State.IsRendering = true;

            // Should return quickly and not throw — assert nothing changed in last-alert timestamps.
            var alertService = clickIt.__Test_GetAlertService();
            var countBefore = alertService.LastAlertTimes.Count;

            clickIt.LogMessage(false, "should-not-log", 0);

            alertService.LastAlertTimes.Count.Should().Be(countBefore);
        }
    }
}
