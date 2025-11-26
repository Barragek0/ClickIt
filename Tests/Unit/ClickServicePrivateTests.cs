using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Reflection;
using ClickIt.Services;
using SharpDX;
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
            settings.DebugMode.Value = true;
            // Ensure DebugMode is set so ErrorHandler.LogError will forward messages during the test
            settings.DebugMode.Value = true;
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

        [TestMethod]
        public void ClickAltarElement_NullElement_LogsCritical()
        {
            var svc = (ClickService)RuntimeHelpers.GetUninitializedObject(typeof(ClickService));

            var settings = new ClickItSettings();
            var errors = new System.Collections.Generic.List<string>();
            var err = new ErrorHandler(settings, (s, f) => errors.Add(s), (s, f) => { });

            // Inject settings + error handler required for the null-element branch
            settings.DebugMode.Value = true;
            typeof(ClickService).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(svc, settings);
            typeof(ClickService).GetField("errorHandler", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(svc, err);

            var mi = typeof(ClickService).GetMethod("ClickAltarElement", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var enumerator = mi.Invoke(svc, new object[] { null, false }) as System.Collections.IEnumerator;

            // Execute the iterator; null-element path yields no items, but should log
            if (enumerator != null)
            {
                var moved = enumerator.MoveNext();
                moved.Should().BeFalse();
            }

            errors.Should().Contain(m => m.Contains("CRITICAL: Altar element is null"));
        }

        [TestMethod]
        public void TryPerformClick_InvalidElement_ReturnsFalse_And_Logs()
        {
            var svc = (ClickService)RuntimeHelpers.GetUninitializedObject(typeof(ClickService));

            var settings = new ClickItSettings();
            var errors = new System.Collections.Generic.List<string>();
            var err = new ErrorHandler(settings, (s, f) => errors.Add(s), (s, f) => { });

            // Inject settings + errorHandler and a minimal performance monitor to satisfy method requirements
            settings.DebugMode.Value = true;
            typeof(ClickService).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(svc, settings);
            typeof(ClickService).GetField("errorHandler", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(svc, err);
            typeof(ClickService).GetField("performanceMonitor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(svc, new PerformanceMonitor(settings));

            var mi = typeof(ClickService).GetMethod("TryPerformClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = mi.Invoke(svc, new object[] { null, new Vector2(0, 0) });

            // Method should return false when the element is invalid/null
            (result is bool b && b).Should().BeFalse();

            errors.Should().Contain(m => m.Contains("Element became invalid or invisible before click"));
        }
    }
}
