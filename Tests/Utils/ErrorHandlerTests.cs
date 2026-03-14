using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System.Collections.Generic;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class ErrorHandlerTests
    {
        [TestMethod]
        public void LogError_WhenDebugMode_LogsAndTracks()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true) };
            var logged = new List<string>();
            var messages = new List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => messages.Add(s));

            handler.LogError("boom1");
            handler.LogError("boom2");

            logged.Should().HaveCount(2);
            handler.RecentErrors.Should().Contain(new[] { "boom1", "boom2" });
        }

        [TestMethod]
        public void LogError_WhenNotDebug_DoesNotLogOrTrack()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(false) };
            var logged = new List<string>();
            var messages = new List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => messages.Add(s));

            handler.LogError("boom");
            logged.Should().BeEmpty();
            handler.RecentErrors.Should().BeEmpty();
        }

        [TestMethod]
        public void LogMessage_RespectsFlags()
        {
            var settings = new ClickItSettings
            {
                DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true),
                LogMessages = new ExileCore.Shared.Nodes.ToggleNode(true)
            };

            var logged = new List<string>();
            var messages = new List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => messages.Add(s));

            handler.LogMessage(true, false, "m1", 0);
            messages.Should().BeEmpty();

            handler.LogMessage(true, true, "m2", 0);
            messages.Should().Contain("m2");

            handler.LogMessage(false, false, "m3", 0);
            messages.Should().Contain("m3");
        }

        [TestMethod]
        public void RecentErrors_ShouldKeepMaxLimit()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true) };
            var logged = new List<string>();
            var messages = new List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => messages.Add(s));

            for (int i = 0; i < 15; i++) handler.LogError($"err{i}");

            handler.RecentErrors.Should().HaveCountLessOrEqualTo(10);
            handler.RecentErrors.Should().Contain("err14");
            handler.RecentErrors.Should().NotContain("err0");
        }

        [TestMethod]
        public void GlobalExceptionHandlers_RegisterAndUnregister_AreIdempotent()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true) };
            var logged = new List<string>();
            var messages = new List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => messages.Add(s));

            handler.RegisterGlobalExceptionHandlers();
            handler.RegisterGlobalExceptionHandlers();

            handler.UnregisterGlobalExceptionHandlers();
            handler.UnregisterGlobalExceptionHandlers();
        }
    }
}
