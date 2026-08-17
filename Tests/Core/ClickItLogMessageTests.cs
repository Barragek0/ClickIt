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
