using ClickIt.Tests.TestUtils;
using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerToggleItemsTests
    {
        [TestMethod]
        public void GetToggleItemsPostClickBlockMs_ClampsNegativeValueToZero()
        {
            var settings = new ClickItSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = -25;
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            handler.GetToggleItemsPostClickBlockMs().Should().Be(0);
        }

        [TestMethod]
        public void IsInToggleItemsPostClickBlockWindow_ReturnsTrue_WhenInsideBlockWindow()
        {
            var settings = new ClickItSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = 500;
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            long now = Environment.TickCount64;
            PrivateFieldAccessor.Set(handler, "_lastToggleItemsTimestampMs", now - 50L);

            bool inWindow = InvokeIsInToggleItemsPostClickBlockWindow(handler);

            inWindow.Should().BeTrue();
        }

        [TestMethod]
        public void IsInToggleItemsPostClickBlockWindow_ReturnsFalse_WhenTimestampIsInFuture()
        {
            var settings = new ClickItSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = 500;
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            long now = Environment.TickCount64;
            PrivateFieldAccessor.Set(handler, "_lastToggleItemsTimestampMs", now + 100L);

            bool inWindow = InvokeIsInToggleItemsPostClickBlockWindow(handler);

            inWindow.Should().BeFalse();
        }

        [TestMethod]
        public void TriggerToggleItems_ReturnsFalse_WhenDisabled()
        {
            var settings = new ClickItSettings();
            settings.ToggleItems.Value = false;
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            bool triggered = handler.TriggerToggleItems();

            triggered.Should().BeFalse();
        }

        [TestMethod]
        public void TriggerToggleItems_ReturnsFalse_WhenIntervalHasNotElapsed()
        {
            var settings = new ClickItSettings();
            settings.ToggleItems.Value = true;
            settings.ToggleItemsIntervalMs.Value = 1000;
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            PrivateFieldAccessor.Set(handler, "_lastToggleItemsTimestampMs", Environment.TickCount64);

            bool triggered = handler.TriggerToggleItems();

            triggered.Should().BeFalse();
        }

        private static bool InvokeIsInToggleItemsPostClickBlockWindow(InputHandler handler)
        {
            var method = typeof(InputHandler).GetMethod(
                "IsInToggleItemsPostClickBlockWindow",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Should().NotBeNull();
            return (bool)method!.Invoke(handler, null)!;
        }
    }
}