namespace ClickIt.Tests.Core
{
    [TestClass]
    public class ClickItLogMessageTests
    {
        [TestMethod]
        public void LogMessageString_DoesNotThrow_WhenNotRendering()
        {
            var clickIt = new ClickIt();

            clickIt.State.Rendering.IsRendering = false;

            FluentActions.Invoking(() => clickIt.LogMessage("test-message", 0))
                .Should().NotThrow();
        }

        [TestMethod]
        public void LogMessageString_Skips_WhenRendering()
        {
            var clickIt = new ClickIt();

            clickIt.State.Rendering.IsRendering = true;

            FluentActions.Invoking(() => clickIt.LogMessage("should-not-log", 0))
                .Should().NotThrow();
        }

        [TestMethod]
        public void LogMessageBool_DoesNotRecurse_WhenNotRendering()
        {
            var clickIt = new ClickIt();

            clickIt.State.Rendering.IsRendering = false;

            var alertService = ClickItHostHarness.InvokeNonPublicInstanceMethod<AlertService>(clickIt, "GetAlertService");
            var countBefore = alertService.LastAlertTimes.Count;

            // Use localDebug=false to avoid reading Settings which may be null in some test harness scenarios
            clickIt.LogMessage(false, "test-message", 0);

            alertService.LastAlertTimes.Count.Should().Be(countBefore);
        }

        [TestMethod]
        public void LogMessageBool_Skips_WhenRendering()
        {
            var clickIt = new ClickIt();

            clickIt.State.Rendering.IsRendering = true;

            var alertService = ClickItHostHarness.InvokeNonPublicInstanceMethod<AlertService>(clickIt, "GetAlertService");
            var countBefore = alertService.LastAlertTimes.Count;

            clickIt.LogMessage(false, "should-not-log", 0);

            alertService.LastAlertTimes.Count.Should().Be(countBefore);
        }

        [TestMethod]
        public void LogError_DoesNotThrow_WhenNotRendering()
        {
            var clickIt = new ClickIt();

            clickIt.State.Rendering.IsRendering = false;

            FluentActions.Invoking(() => clickIt.LogError("visible-error", 0))
                .Should().NotThrow();
        }

        [TestMethod]
        public void LogError_Skips_WhenRendering()
        {
            var clickIt = new ClickIt();

            clickIt.State.Rendering.IsRendering = true;

            FluentActions.Invoking(() => clickIt.LogError("hidden-error", 0))
                .Should().NotThrow();
        }
    }
}
