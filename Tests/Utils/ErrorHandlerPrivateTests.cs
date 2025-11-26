using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class ErrorHandlerPrivateTests
    {
        [TestMethod]
        public void HandleUnhandledException_LogsMessage_And_HandlesTerminatingFlag()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true) };
            var logged = new List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => { });

            var mi = handler.GetType().GetMethod("HandleUnhandledException", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            // non-terminating
            var args = new UnhandledExceptionEventArgs(new Exception("boom"), false);
            mi.Invoke(handler, [null, args]);
            logged.Should().Contain(m => m.Contains("Unhandled exception") && m.Contains("boom"));

            // terminating - should still log a terminating message
            logged.Clear();
            var args2 = new UnhandledExceptionEventArgs(new Exception("crash"), true);
            mi.Invoke(handler, [null, args2]);
            logged.Should().Contain(m => m.Contains("Unhandled exception") && m.Contains("crash"));
            logged.Should().Contain(m => m.Contains("Runtime is terminating"));
        }

        [TestMethod]
        public void HandleUnobservedTaskException_LogsAndSetsObserved()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true) };
            var logged = new List<string>();
            var handler = new ErrorHandler(settings, (s, f) => logged.Add(s), (s, f) => { });

            var mi = handler.GetType().GetMethod("HandleUnobservedTaskException", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var ex = new AggregateException(new Exception("taskboom"));
            var args = new UnobservedTaskExceptionEventArgs(ex);

            mi.Invoke(handler, [null, args]);

            logged.Should().Contain(m => m.Contains("Unobserved task exception") && m.Contains("taskboom"));
        }

        [TestMethod]
        public void LogWithFallback_WhenLogErrorThrows_WritesToCrashLogFile()
        {
            var settings = new ClickItSettings { DebugMode = new ExileCore.Shared.Nodes.ToggleNode(true) };

            // Provide a failing logError implementation to force fallback code path
            Action<string, int> failingLog = (s, f) => throw new InvalidOperationException("logfail");
            var messages = new List<string>();
            var handler = new ErrorHandler(settings, failingLog, (s, f) => messages.Add(s));

            var mi = handler.GetType().GetMethod("LogWithFallback", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var file = Path.Combine(Directory.GetCurrentDirectory(), "ClickIt_Crash.log");
            try
            {
                if (File.Exists(file)) File.Delete(file);

                mi.Invoke(handler, ["fallback-test"]);

                File.Exists(file).Should().BeTrue();
                var contents = File.ReadAllText(file);
                contents.Should().Contain("fallback-test");
            }
            finally
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { }
            }
        }
    }
}
