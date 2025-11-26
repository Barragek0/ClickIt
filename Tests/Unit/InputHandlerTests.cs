using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt;
using Moq;
using SharpDX;
using System;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerTests
    {
        [TestMethod]
        public void CanClick_ReturnsFalse_WhenGameControllerIsNull()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);
            handler.CanClick(null).Should().BeFalse();
        }

        [TestMethod]
        public void TriggerToggleItems_ReturnsFalse_WhenToggleItemsDisabled()
        {
            var settings = new ClickItSettings();
            settings.ToggleItems.Value = false;
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);
            handler.TriggerToggleItems().Should().BeFalse();
        }

        // Note: CalculateClickPosition relies directly on ExileCore runtime types
        // whose members are non-virtual and tied to native memory reads. Creating
        // reliable unit tests for that method requires adapter shims or refactors
        // in the production code. For now we keep light, safe tests in this file
        // that don't touch those native types.
        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsActual_WhenNotLazyMode()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            // lazy mode false — should follow hotkeyHeld
            var resTrue = handler.IsClickHotkeyPressedForTests(
                lazyMode: false,
                hotkeyHeld: true,
                hasRestrictedItemsOnScreen: false,
                disableKeyHeld: false,
                disableLeftClickHeldSetting: false,
                leftClickHeld: false,
                disableRightClickHeldSetting: false,
                rightClickHeld: false);

            var resFalse = handler.IsClickHotkeyPressedForTests(
                lazyMode: false,
                hotkeyHeld: false,
                hasRestrictedItemsOnScreen: false,
                disableKeyHeld: false,
                disableLeftClickHeldSetting: false,
                leftClickHeld: false,
                disableRightClickHeldSetting: false,
                rightClickHeld: false);

            resTrue.Should().BeTrue();
            resFalse.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsTrue_WhenHotkeyHeldInLazyMode()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = handler.IsClickHotkeyPressedForTests(
                lazyMode: true,
                hotkeyHeld: true,
                hasRestrictedItemsOnScreen: true, // should be ignored when hotkey is held
                disableKeyHeld: true,
                disableLeftClickHeldSetting: true,
                leftClickHeld: true,
                disableRightClickHeldSetting: true,
                rightClickHeld: true);

            res.Should().BeTrue();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenRestrictedItemsPresent()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = handler.IsClickHotkeyPressedForTests(
                lazyMode: true,
                hotkeyHeld: false,
                hasRestrictedItemsOnScreen: true,
                disableKeyHeld: false,
                disableLeftClickHeldSetting: false,
                leftClickHeld: false,
                disableRightClickHeldSetting: false,
                rightClickHeld: false);

            res.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenDisableKeyHeld()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = handler.IsClickHotkeyPressedForTests(
                lazyMode: true,
                hotkeyHeld: false,
                hasRestrictedItemsOnScreen: false,
                disableKeyHeld: true,
                disableLeftClickHeldSetting: false,
                leftClickHeld: false,
                disableRightClickHeldSetting: false,
                rightClickHeld: false);

            res.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenLeftClickBlocks()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = handler.IsClickHotkeyPressedForTests(
                lazyMode: true,
                hotkeyHeld: false,
                hasRestrictedItemsOnScreen: false,
                disableKeyHeld: false,
                disableLeftClickHeldSetting: true,
                leftClickHeld: true,
                disableRightClickHeldSetting: false,
                rightClickHeld: false);

            res.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsFalse_WhenRightClickBlocks()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = handler.IsClickHotkeyPressedForTests(
                lazyMode: true,
                hotkeyHeld: false,
                hasRestrictedItemsOnScreen: false,
                disableKeyHeld: false,
                disableLeftClickHeldSetting: false,
                leftClickHeld: false,
                disableRightClickHeldSetting: true,
                rightClickHeld: true);

            res.Should().BeFalse();
        }

        [TestMethod]
        public void IsClickHotkeyPressed_ReturnsTrue_WhenNoBlockers()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var res = handler.IsClickHotkeyPressedForTests(
                lazyMode: true,
                hotkeyHeld: false,
                hasRestrictedItemsOnScreen: false,
                disableKeyHeld: false,
                disableLeftClickHeldSetting: true,
                leftClickHeld: false,
                disableRightClickHeldSetting: true,
                rightClickHeld: false);

            res.Should().BeTrue();
        }
    }
}
