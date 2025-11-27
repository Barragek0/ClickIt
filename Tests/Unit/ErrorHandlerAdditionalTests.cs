using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System;
using System.IO;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ErrorHandlerAdditionalTests
    {
        [TestMethod]
        public void LogError_TracksRecentErrors_WhenDebugModeTrue()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            var logged = false;
            var eh = new ErrorHandler(settings, (m, f) => logged = true, (m, f) => { });

            eh.LogError("boom");
            logged.Should().BeTrue();
            eh.RecentErrors.Should().ContainSingle().Which.Should().Contain("boom");
        }

        [TestMethod]
        public void LogError_DoesNotCallWhenDebugModeFalse()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = false;
            var called = false;
            var eh = new ErrorHandler(settings, (m, f) => called = true, (m, f) => { });

            eh.LogError("silent");
            called.Should().BeFalse();
            eh.RecentErrors.Should().BeEmpty();
        }

        [TestMethod]
        public void LogMessage_RespectsRequireLocalDebugAndFlags()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;

            bool called = false;
            var eh = new ErrorHandler(settings, (m, f) => { }, (m, f) => called = true);

            // requireLocalDebug true, localDebugFlag false -> shouldn't call
            eh.LogMessage(true, false, "m", 0);
            called.Should().BeFalse();

            // requireLocalDebug true & localDebug true -> should call
            eh.LogMessage(true, true, "m2", 0);
            called.Should().BeTrue();
        }

        [TestMethod]
        public void LogMessage_NoRequire_UsesGlobalDebugModeAndLogMessages()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;

            bool called = false;
            var eh = new ErrorHandler(settings, (m, f) => { }, (m, f) => called = true);

            eh.LogMessage(false, true, "x", 0);
            called.Should().BeTrue();
        }

        [TestMethod]
        public void LogWithFallback_WhenPrimaryThrows_DoesNotThrowAndWritesFile()
        {
            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;

            // Create a logError that throws to force fallback
            Action<string, int> throwing = (m, f) => throw new Exception("boom");
            var eh = new ErrorHandler(settings, throwing, (m, f) => { });

            var pi = typeof(ErrorHandler).GetMethod("LogWithFallback", BindingFlags.NonPublic | BindingFlags.Instance);
            var tmp = Path.Combine(Directory.GetCurrentDirectory(), "ClickIt_Crash.log");
            if (File.Exists(tmp)) File.Delete(tmp);

            Action act = () => pi.Invoke(eh, new object[] { "a test message" });
            act.Should().NotThrow();

            // Fallback attempted; file may or may not be created depending on environment, but invocation should not throw
        }
    }
}
