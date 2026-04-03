using ClickIt.Shared;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ClickIt.Tests.Common.Input
{
    [TestClass]
    public class InputHandlerToggleItemsTests
    {
        [TestMethod]
        public void GetToggleItemsPostClickBlockMs_ClampsNegativeValueToZero()
        {
            var settings = new ClickItSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = -25;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            controller.GetToggleItemsPostClickBlockMs().Should().Be(0);
        }

        [TestMethod]
        public void IsInToggleItemsPostClickBlockWindow_ReturnsTrue_WhenInsideBlockWindow()
        {
            var settings = new ClickItSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = 500;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            long now = Environment.TickCount64;
            controller.SetLastToggleItemsTimestamp(now - 50L);

            bool inWindow = controller.IsInPostClickBlockWindow();

            inWindow.Should().BeTrue();
        }

        [TestMethod]
        public void IsInToggleItemsPostClickBlockWindow_ReturnsFalse_WhenTimestampIsInFuture()
        {
            var settings = new ClickItSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = 500;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            long now = Environment.TickCount64;
            controller.SetLastToggleItemsTimestamp(now + 100L);

            bool inWindow = controller.IsInPostClickBlockWindow();

            inWindow.Should().BeFalse();
        }

        [TestMethod]
        public void TriggerToggleItems_ReturnsFalse_WhenDisabled()
        {
            var settings = new ClickItSettings();
            settings.ToggleItems.Value = false;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            bool triggered = controller.TriggerToggleItems();

            triggered.Should().BeFalse();
        }

        [TestMethod]
        public void TriggerToggleItems_ReturnsFalse_WhenIntervalHasNotElapsed()
        {
            var settings = new ClickItSettings();
            settings.ToggleItems.Value = true;
            settings.ToggleItemsIntervalMs.Value = 1000;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            controller.SetLastToggleItemsTimestamp(Environment.TickCount64);

            bool triggered = controller.TriggerToggleItems();

            triggered.Should().BeFalse();
        }
    }
}