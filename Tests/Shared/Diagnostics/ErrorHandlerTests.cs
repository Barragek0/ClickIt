namespace ClickIt.Tests.Shared.Diagnostics
{
    [TestClass]
    public class ErrorHandlerTests
    {
        [TestMethod]
        public void LogError_WhenDebugModeDisabled_DoesNotLogButTracksRecentErrors()
        {
            var settings = new ClickItSettings();
            var loggedErrors = new List<string>();
            var handler = new ErrorHandler(
                settings,
                (message, _) => loggedErrors.Add(message),
                static (_, _) => { });

            settings.DebugMode.Value = false;

            handler.LogError("background failure", frame: 7);

            loggedErrors.Should().BeEmpty();
            handler.RecentErrors.Should().ContainSingle();
            handler.RecentErrors[0].Should().Contain("background failure");
        }

        [TestMethod]
        public void LogError_WhenDebugModeEnabled_LogsAndTracksRecentErrors()
        {
            var settings = new ClickItSettings();
            var loggedErrors = new List<(string Message, int Frame)>();
            var handler = new ErrorHandler(
                settings,
                (message, frame) => loggedErrors.Add((message, frame)),
                static (_, _) => { });

            settings.DebugMode.Value = true;

            handler.LogError("visible failure", frame: 9);

            loggedErrors.Should().ContainSingle();
            loggedErrors[0].Message.Should().Be("visible failure");
            loggedErrors[0].Frame.Should().Be(9);
            handler.RecentErrors.Should().ContainSingle();
            handler.RecentErrors[0].Should().Contain("visible failure");
        }
    }
}