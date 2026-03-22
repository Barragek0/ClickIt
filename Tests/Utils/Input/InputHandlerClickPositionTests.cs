using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExileCore.Shared.Enums;
using SharpDX;
using System.Collections.Generic;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerClickPositionTests
    {
        [TestMethod]
        public void ResolveVisibleClickPoint_WhenPreferredPointIsUnblocked_ReturnsPreferredPoint()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF> { new RectangleF(0, 0, 10, 10) };

            Vector2 result = InputHandler.ResolveVisibleClickPoint(target, preferred, blocked);

            result.Should().Be(preferred);
        }

        [TestMethod]
        public void ResolveVisibleClickPoint_WhenCenterBlocked_ReturnsPointInsideTargetOutsideBlockedArea()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF> { new RectangleF(40, 10, 20, 20) };

            Vector2 result = InputHandler.ResolveVisibleClickPoint(target, preferred, blocked);

            bool insideTarget = result.X >= target.Left && result.X <= target.Right && result.Y >= target.Top && result.Y <= target.Bottom;
            bool insideBlocked = result.X >= blocked[0].Left && result.X <= blocked[0].Right && result.Y >= blocked[0].Top && result.Y <= blocked[0].Bottom;

            insideTarget.Should().BeTrue();
            insideBlocked.Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveVisibleClickPoint_WhenEntireTargetBlocked_ReturnsFalse()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF> { new RectangleF(0, 0, 100, 40) };

            bool hasVisiblePoint = InputHandler.TryResolveVisibleClickPoint(target, preferred, blocked, out Vector2 resolved);

            hasVisiblePoint.Should().BeFalse();
            resolved.Should().Be(preferred);
        }

        [TestMethod]
        public void TryResolveVisibleClickablePoint_UsesClickableSubregion_WhenCenterIsNotClickable()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF>();

            static bool IsClickable(Vector2 p) => p.X < 40 || p.X > 60;

            bool ok = InputHandler.TryResolveVisibleClickablePoint(target, preferred, blocked, IsClickable, out Vector2 resolved);

            ok.Should().BeTrue();
            resolved.X.Should().NotBeInRange(40f, 60f);
            resolved.Y.Should().BeGreaterThanOrEqualTo(target.Top);
            resolved.Y.Should().BeLessThanOrEqualTo(target.Bottom);
        }

        [TestMethod]
        public void TryResolveVisibleClickablePoint_ReturnsFalse_WhenNoClickablePointExists()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF>();

            static bool IsClickable(Vector2 _) => false;

            bool ok = InputHandler.TryResolveVisibleClickablePoint(target, preferred, blocked, IsClickable, out Vector2 resolved);

            ok.Should().BeFalse();
            resolved.Should().Be(preferred);
        }

        [TestMethod]
        public void IsSafeAutomationPoint_ReturnsTrue_WhenInsideWindowAndVirtualScreen()
        {
            var point = new Vector2(200, 200);
            var gameWindow = new RectangleF(100, 100, 500, 400);
            var virtualScreen = new RectangleF(0, 0, 1920, 1080);

            InputHandler.IsSafeAutomationPoint(point, gameWindow, virtualScreen).Should().BeTrue();
        }

        [TestMethod]
        public void IsSafeAutomationPoint_ReturnsFalse_WhenOutsideGameWindow()
        {
            var point = new Vector2(50, 50);
            var gameWindow = new RectangleF(100, 100, 500, 400);
            var virtualScreen = new RectangleF(0, 0, 1920, 1080);

            InputHandler.IsSafeAutomationPoint(point, gameWindow, virtualScreen).Should().BeFalse();
        }

        [TestMethod]
        public void IsSafeAutomationPoint_ReturnsFalse_WhenOutsideVirtualScreen()
        {
            var point = new Vector2(-5000, -5000);
            var gameWindow = new RectangleF(0, 0, 1920, 1080);
            var virtualScreen = new RectangleF(0, 0, 1920, 1080);

            InputHandler.IsSafeAutomationPoint(point, gameWindow, virtualScreen).Should().BeFalse();
        }

        [TestMethod]
        public void IsHeistContractWorldItem_DetectsByPathAndName()
        {
            InputHandler.IsHeistContractWorldItem(
                "Metadata/Items/Heist/Contracts/ContractWeapons1",
                "Whatever").Should().BeTrue();

            InputHandler.IsHeistContractWorldItem(
                string.Empty,
                "Contract: Smuggler's Den").Should().BeTrue();

            InputHandler.IsHeistContractWorldItem(
                "Metadata/Items/Currency/CurrencyRerollRare",
                "Chaos Orb").Should().BeFalse();
        }

        [TestMethod]
        public void IsHeistBlueprintWorldItem_DetectsByPathAndName()
        {
            InputHandler.IsHeistBlueprintWorldItem(
                "Metadata/Items/Heist/HeistBlueprint/BlueprintGeneric",
                "Whatever").Should().BeTrue();

            InputHandler.IsHeistBlueprintWorldItem(
                "Metadata/Items/Currency/Heist/Blueprint/BlueprintCurrency1",
                "Whatever").Should().BeTrue();

            InputHandler.IsHeistBlueprintWorldItem(
                string.Empty,
                "Blueprint: Smuggler's Den").Should().BeTrue();

            InputHandler.IsHeistBlueprintWorldItem(
                "Metadata/Items/Currency/CurrencyRerollRare",
                "Chaos Orb").Should().BeFalse();
        }

        [TestMethod]
        public void IsRoguesMarkerWorldItem_DetectsByPathAndName()
        {
            InputHandler.IsRoguesMarkerWorldItem(
                "Metadata/Items/Heist/HeistCoin/HeistCoin1",
                "Whatever").Should().BeTrue();

            InputHandler.IsRoguesMarkerWorldItem(
                string.Empty,
                "Rogue's Marker").Should().BeTrue();

            InputHandler.IsRoguesMarkerWorldItem(
                "Metadata/Items/Currency/CurrencyRerollRare",
                "Chaos Orb").Should().BeFalse();
        }

        [TestMethod]
        public void ShouldForceUiHoverVerificationForWorldItem_ReturnsTrue_ForHeistContractsBlueprintsAndMarkers()
        {
            InputHandler.ShouldForceUiHoverVerificationForWorldItem(
                "Metadata/Items/Heist/Contracts/ContractGeneric",
                "Contract: Test").Should().BeTrue();

            InputHandler.ShouldForceUiHoverVerificationForWorldItem(
                "Metadata/Items/Heist/HeistBlueprint/BlueprintGeneric",
                "Blueprint: Test").Should().BeTrue();

            InputHandler.ShouldForceUiHoverVerificationForWorldItem(
                "Metadata/Items/Heist/HeistCoin/HeistCoin1",
                "Rogue's Marker").Should().BeTrue();

            InputHandler.ShouldForceUiHoverVerificationForWorldItem(
                "Metadata/Items/Currency/CurrencyRerollRare",
                "Chaos Orb").Should().BeFalse();
        }

        [TestMethod]
        public void ResolvePreferredLabelPoint_UsesLowerLabelArea_ForHeistContracts()
        {
            var rect = new RectangleF(100, 200, 180, 40);

            Vector2 preferred = InputHandler.ResolvePreferredLabelPoint(
                rect,
                EntityType.WorldItem,
                chestHeightOffset: 0,
                "Metadata/Items/Heist/Contracts/ContractGeneric",
                "Contract: Test");

            preferred.Y.Should().BeGreaterThan(rect.Center.Y);
            preferred.Y.Should().BeLessThan(rect.Bottom);
            preferred.X.Should().Be(rect.Center.X);
        }

        [TestMethod]
        public void HasUnblockedOverlapProbePoint_ReturnsTrue_WhenNoPotentialBlockers()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);

            bool hasUnblocked = InputHandler.HasUnblockedOverlapProbePoint(target, preferred, []);

            hasUnblocked.Should().BeTrue();
        }

        [TestMethod]
        public void HasUnblockedOverlapProbePoint_ReturnsTrue_WhenAtLeastOneProbePointIsVisible()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var potentialBlockers = new List<RectangleF>
            {
                new RectangleF(45, 15, 10, 10)
            };

            bool hasUnblocked = InputHandler.HasUnblockedOverlapProbePoint(target, preferred, potentialBlockers);

            hasUnblocked.Should().BeTrue();
        }

        [TestMethod]
        public void HasUnblockedOverlapProbePoint_ReturnsFalse_WhenEveryProbePointIsBlocked()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var potentialBlockers = new List<RectangleF>
            {
                new RectangleF(0, 0, 100, 40)
            };

            bool hasUnblocked = InputHandler.HasUnblockedOverlapProbePoint(target, preferred, potentialBlockers);

            hasUnblocked.Should().BeFalse();
        }
    }
}
