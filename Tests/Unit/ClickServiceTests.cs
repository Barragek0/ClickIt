using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Reflection;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickServiceTests
    {
        [TestMethod]
        public void ClickService_Type_IsPresent()
        {
            // Sanity check: type exists in assembly (helps prevent accidental removals)
            var t = typeof(Services.ClickService);
            Assert.IsNotNull(t);
        }

        [TestMethod]
        public void ClickService_Constructor_HasExpectedNumberOfDependencies()
        {
            var ctor = typeof(Services.ClickService).GetConstructors()[0];
            // Expecting a constructor that accepts many dependencies (safety check)
            Assert.IsTrue(ctor.GetParameters().Length >= 10, "ClickService ctor should require many dependencies");
        }

        [TestMethod]
        public void DebugLog_OnlyLogs_WhenDebugModeEnabled()
        {
            var svc = (Services.ClickService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));

            var settings = new ClickItSettings();
            settings.DebugMode.Value = true;
            var messages = new System.Collections.Generic.List<string>();
            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => messages.Add(s));

            typeof(Services.ClickService).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, settings);
            typeof(Services.ClickService).GetField("errorHandler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, err);

            var mi = typeof(Services.ClickService).GetMethod("DebugLog", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            mi.Invoke(svc, new object[] { new System.Func<string>(() => "hello") });
            messages.Should().BeEmpty();

            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            mi.Invoke(svc, new object[] { new System.Func<string>(() => "hello2") });
            messages.Should().Contain("hello2");
        }

        [TestMethod]
        public void ClickAltarElement_NullElement_LogsCritical()
        {
            var svc = (Services.ClickService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));

            var settings = new ClickItSettings();
            var errors = new System.Collections.Generic.List<string>();
            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => errors.Add(s), (s, f) => { });

            settings.DebugMode.Value = true;
            typeof(Services.ClickService).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, settings);
            typeof(Services.ClickService).GetField("errorHandler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, err);

            var mi = typeof(Services.ClickService).GetMethod("ClickAltarElement", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var enumerator = mi.Invoke(svc, new object[] { null, false }) as System.Collections.IEnumerator;

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
            var svc = (Services.ClickService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService));

            var settings = new ClickItSettings();
            var errors = new System.Collections.Generic.List<string>();
            var err = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => errors.Add(s), (s, f) => { });

            settings.DebugMode.Value = true;
            typeof(Services.ClickService).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, settings);
            typeof(Services.ClickService).GetField("errorHandler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, err);
            typeof(Services.ClickService).GetField("performanceMonitor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(svc, new global::ClickIt.Utils.PerformanceMonitor(settings));

            var mi = typeof(Services.ClickService).GetMethod("TryPerformClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var result = mi.Invoke(svc, new object[] { null, new SharpDX.Vector2(0, 0) });

            (result is bool b && b).Should().BeFalse();
            errors.Should().Contain(m => m.Contains("Element became invalid or invisible before click"));
        }
    }
}
