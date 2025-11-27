using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt.Components;
using ClickIt;
using ExileCore.PoEMemory;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerAndClickServiceExtraTests
    {
        [TestMethod]
        public void IsClickHotkeyPressed_LazyMode_AllowsWhenNoRestrictedLabels()
        {
            // Use the test seam to avoid invoking native Input.GetKeyState (Vanara dependency)
            var res = InputHandler.IsClickHotkeyPressedForTests(
                lazyMode: true,
                hotkeyHeld: false,
                hasRestrictedItemsOnScreen: false,
                disableKeyHeld: false,
                disableLeftClickHeldSetting: false,
                leftClickHeld: false,
                disableRightClickHeldSetting: false,
                rightClickHeld: false);

            res.Should().BeTrue();
        }

        [TestMethod]
        public void CanClick_ReturnsFalse_WhenGameControllerIsNull()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var ih = new InputHandler(settings, perf, null);

            ih.CanClick(null).Should().BeFalse();
        }

        // NOTE: We already have extensive ClickService ShouldClickAltar coverage in ClickServiceShouldClickAltarTests
        // This file contains a small set of extra tests; avoid duplicating more fragile ClickService tests here.
    }
}
