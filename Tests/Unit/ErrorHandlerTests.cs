using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ErrorHandlerTests
    {
        [TestMethod]
        public void LogError_AddsToRecentErrors_WhenDebugModeEnabled()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            var called = false;
            var eh = new ErrorHandler(settings, (m, f) => { called = true; }, (m, f) => { });

            eh.LogError("whoops", 0);
            called.Should().BeTrue();
            eh.RecentErrors.Should().ContainSingle().Which.Should().Be("whoops");
        }

        [TestMethod]
        public void LogMessage_RespectsDebugAndLogMessagesFlags()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;

            var called = false;
            var eh = new ErrorHandler(settings, (m, f) => { }, (m, f) => { called = true; });

            eh.LogMessage("hello", 1);
            called.Should().BeTrue();
        }
    }
}
