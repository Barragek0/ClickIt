using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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
            handler.SetLastToggleItemsTimestampForTests(now - 50L);

            bool inWindow = handler.IsInToggleItemsPostClickBlockWindowForTests();

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
            handler.SetLastToggleItemsTimestampForTests(now + 100L);

            bool inWindow = handler.IsInToggleItemsPostClickBlockWindowForTests();

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

            handler.SetLastToggleItemsTimestampForTests(Environment.TickCount64);

            bool triggered = handler.TriggerToggleItems();

            triggered.Should().BeFalse();
        }
    }
}