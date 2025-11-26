using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Reflection;
using ClickIt.Services;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServicePrivateTests
    {
        // Note: IsAltarLabel inspects ExileCore types which vary across test environments.
        // More robust integration-style tests of ProcessRegularClick / altar-detection will cover
        // IsAltarLabel behaviour in later phases; keeping this file focused on DebugLog (quick-wins).

        [TestMethod]
        public void DebugLog_OnlyLogs_WhenDebugModeEnabled()
        {
            // Create an uninitialized ClickService instance and set only the fields we need for DebugLog
            var svc = (ClickService)RuntimeHelpers.GetUninitializedObject(typeof(ClickService));

            var settings = new ClickItSettings();
            // default DebugMode is false
            var messages = new System.Collections.Generic.List<string>();
            var err = new ErrorHandler(settings, (s, f) => { }, (s, f) => messages.Add(s));

            // Inject private fields
            typeof(ClickService).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(svc, settings);
            typeof(ClickService).GetField("errorHandler", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(svc, err);

            // Invoke DebugLog (should not call into error handler because DebugMode is false)
            var mi = typeof(ClickService).GetMethod("DebugLog", BindingFlags.Instance | BindingFlags.NonPublic)!;
            mi.Invoke(svc, new object[] { new System.Func<string>(() => "hello") });
            messages.Should().BeEmpty();

            // Enable DebugMode and ensure DebugLog logs
            settings.DebugMode.Value = true;
            // ErrorHandler.LogMessage only forwards messages when LogMessages is enabled
            settings.LogMessages.Value = true;
            mi.Invoke(svc, new object[] { new System.Func<string>(() => "hello2") });
            messages.Should().Contain("hello2");
        }
    }
}
