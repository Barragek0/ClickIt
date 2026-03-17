using System.Reflection;
using ClickIt.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InventoryFullWarningRendererTests
    {
        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsTrue_WhenFullDecisionDisallowsPickup()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ShouldShowInventoryPickupBlockedWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var snapshot = CreateInventorySnapshot(stage: "InventoryFullDecision", inventoryFull: true, allowPickup: false);

            bool blocked = (bool)method!.Invoke(null, [snapshot])!;
            blocked.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsTrue_WhenNotFullNoFitDisallowsPickup()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ShouldShowInventoryPickupBlockedWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false);

            bool blocked = (bool)method!.Invoke(null, [snapshot])!;
            blocked.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldShowInventoryFullWarning_HidesAfterTenSeconds()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ShouldShowInventoryFullWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            bool withinWindow = (bool)method!.Invoke(null, [10_000L, 1_000L])!;
            bool expired = (bool)method!.Invoke(null, [11_001L, 1_000L])!;

            withinWindow.Should().BeTrue();
            expired.Should().BeFalse();
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_UsesBetweenTertiaryRectangles()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ResolveInventoryFullWarningPosition",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            RectangleF window = new RectangleF(0, 0, 1920, 1080);
            RectangleF left = new RectangleF(400, 900, 520, 1040);
            RectangleF right = new RectangleF(1400, 900, 1520, 1040);

            var pos = (Vector2)method!.Invoke(null, [window, left, right, null])!;

            pos.X.Should().BeApproximately(960f, 0.01f);
            pos.Y.Should().BeApproximately(970f, 0.01f);
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_PrefersPlayerFeetPosition()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ResolveInventoryFullWarningPosition",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            RectangleF window = new RectangleF(0, 0, 1920, 1080);
            RectangleF left = new RectangleF(400, 900, 520, 1040);
            RectangleF right = new RectangleF(1400, 900, 1520, 1040);
            Vector2 feet = new Vector2(777f, 888f);

            var pos = (Vector2)method!.Invoke(null, [window, left, right, (Vector2?)feet])!;

            pos.Should().Be(feet);
        }

        private static LabelFilterService.InventoryDebugSnapshot CreateInventorySnapshot(string stage, bool inventoryFull, bool allowPickup)
        {
            return new LabelFilterService.InventoryDebugSnapshot(
                HasData: true,
                Stage: stage,
                InventoryFull: inventoryFull,
                InventoryFullSource: "CellOccupancy",
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: 60,
                OccupiedCells: 60,
                InventoryEntityCount: 24,
                LayoutEntryCount: 24,
                GroundItemPath: "Metadata/Items/Whatever",
                GroundItemName: "Item",
                IsGroundStackable: false,
                MatchingPathCount: 0,
                PartialMatchingStackCount: 0,
                HasPartialMatchingStack: false,
                DecisionAllowPickup: allowPickup,
                Notes: string.Empty,
                Sequence: 0,
                TimestampMs: 0);
        }
    }
}