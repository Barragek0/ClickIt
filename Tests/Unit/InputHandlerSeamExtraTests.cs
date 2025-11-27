using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerSeamExtraTests
    {
        [TestMethod]
        public void IsPOEActiveForTests_ReturnsFalse_WhenWindowNotForeground()
        {
            InputHandler.IsPOEActiveForTests(windowIsForeground: false).Should().BeFalse();
            InputHandler.IsPOEActiveForTests(windowIsForeground: true).Should().BeTrue();
        }

        [TestMethod]
        public void IsPanelOpenForTests_ReturnsTrue_WhenRightPanelAddressNonZero()
        {
            // left zero, right non-zero
            InputHandler.IsPanelOpenForTests(openLeftPanelAddress: 0, openRightPanelAddress: 123L).Should().BeTrue();

            // both zero -> false
            InputHandler.IsPanelOpenForTests(openLeftPanelAddress: 0, openRightPanelAddress: 0).Should().BeFalse();
        }

        [TestMethod]
        public void IsInTownOrHideoutForTests_ReturnsTrue_WhenHideoutTrue()
        {
            InputHandler.IsInTownOrHideoutForTests(isTown: false, isHideout: true).Should().BeTrue();
            InputHandler.IsInTownOrHideoutForTests(isTown: false, isHideout: false).Should().BeFalse();
        }

        [TestMethod]
        public void GetCanClickFailureReasonForTests_Returns_PoENotInFocus_When_WindowNotForeground()
        {
            var res = InputHandler.GetCanClickFailureReasonForTests(windowIsForeground: false);
            res.Should().Be("PoE not in focus.");
        }

        [TestMethod]
        public void GetCanClickFailureReasonForTests_Returns_PanelOpen_When_RightPanelNonZero_And_BlockSettingEnabled()
        {
            var res = InputHandler.GetCanClickFailureReasonForTests(windowIsForeground: true, blockOnOpenLeftRightPanel: true, openRightPanelAddress: 555L);
            res.Should().Be("Panel is open.");
        }

        [TestMethod]
        public void GetCanClickFailureReasonForTests_Returns_ClickingDisabled_When_NoIssues()
        {
            var res = InputHandler.GetCanClickFailureReasonForTests(windowIsForeground: true);
            res.Should().Be("Clicking disabled.");
        }

        [TestMethod]
        public void CalculateClickPositionForTests_Handles_ChestAndNonChestOffsets()
        {
            var settings = new ClickItSettings();
            settings.ChestHeightOffset.Value = 10;
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            var rect = new global::SharpDX.RectangleF(10, 20, 30, 40); // center = (25, 40)
            var windowTopLeft = new global::SharpDX.Vector2(5, 7);

            // Non-chest: jitter applied normally
            var p1 = handler.CalculateClickPositionForTests(rect, windowTopLeft, global::ExileCore.Shared.Enums.EntityType.WorldItem, 1.5f, -2f);
            p1.Should().Be(rect.Center + windowTopLeft + new global::SharpDX.Vector2(1.5f, -2f));

            // Chest: chest height offset is subtracted from jitter Y
            var p2 = handler.CalculateClickPositionForTests(rect, windowTopLeft, global::ExileCore.Shared.Enums.EntityType.Chest, 1.5f, -2f);
            p2.Should().Be(rect.Center + windowTopLeft + new global::SharpDX.Vector2(1.5f, -2f - settings.ChestHeightOffset.Value));
        }

        [TestMethod]
        public void TriggerToggleItemsForTests_Respects_Setting_And_RandomValue()
        {
            var settings = new ClickItSettings();
            var perf = new PerformanceMonitor(settings);
            var handler = new InputHandler(settings, perf);

            // ToggleItems default true — nextRandomValue 0 should return true
            handler.TriggerToggleItemsForTests(0).Should().BeTrue();

            // non-zero random value should not trigger
            handler.TriggerToggleItemsForTests(1).Should().BeFalse();

            // disable feature -> always false
            settings.ToggleItems.Value = false;
            handler.TriggerToggleItemsForTests(0).Should().BeFalse();
        }
    }
}
