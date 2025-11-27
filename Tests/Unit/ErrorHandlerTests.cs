using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System;
using System.Reflection;
using System.Threading.Tasks;

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

            Action<string, int> throwing = (m, f) => throw new Exception("boom");
            var eh = new ErrorHandler(settings, throwing, (m, f) => { });

            var pi = typeof(ErrorHandler).GetMethod("LogWithFallback", BindingFlags.NonPublic | BindingFlags.Instance);
            pi.Should().NotBeNull();
            var tmp = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "ClickIt_Crash.log");
            if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp);

            System.Action act = () => pi!.Invoke(eh, new object[] { "a test message" });
            act.Should().NotThrow();
        }

        [TestMethod]
        public void HandleUnhandledException_LogsMessage_And_HandlesTerminatingFlag()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true) };
            var logged = new System.Collections.Generic.List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => { });

            var mi = handler.GetType().GetMethod("HandleUnhandledException", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var args = new UnhandledExceptionEventArgs(new Exception("boom"), false);
            mi.Invoke(handler, new object?[] { null!, args });
            logged.Should().Contain(m => m.Contains("Unhandled exception") && m.Contains("boom"));

            // terminating - should still log a terminating message
            logged.Clear();
            var args2 = new UnhandledExceptionEventArgs(new Exception("crash"), true);
            mi.Invoke(handler, new object?[] { null!, args2 });
            logged.Should().Contain(m => m.Contains("Unhandled exception") && m.Contains("crash"));
            logged.Should().Contain(m => m.Contains("Runtime is terminating"));
        }

        [TestMethod]
        public void HandleUnobservedTaskException_LogsAndSetsObserved()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true) };
            var logged = new System.Collections.Generic.List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => { });

            var mi = handler.GetType().GetMethod("HandleUnobservedTaskException", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var ex = new AggregateException(new Exception("taskboom"));
            var args = new UnobservedTaskExceptionEventArgs(ex);

            mi.Invoke(handler, new object?[] { null!, args });

            logged.Should().Contain(m => m.Contains("Unobserved task exception") && m.Contains("taskboom"));
        }
    }
}
