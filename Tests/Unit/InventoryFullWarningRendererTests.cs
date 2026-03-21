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
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsFalse_WhenNotFullNoFitHasMissingIdentity()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ShouldShowInventoryPickupBlockedWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false) with
            {
                GroundItemPath = string.Empty,
                GroundItemName = string.Empty
            };

            bool blocked = (bool)method!.Invoke(null, [snapshot])!;
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsFalse_WhenNotFullNoFitLayoutIsUnreliable()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ShouldShowInventoryPickupBlockedWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false) with
            {
                Notes = "Inventory layout unreliable from PlayerInventories[0].InventorySlotItems (raw:1 parsed:0)"
            };

            bool blocked = (bool)method!.Invoke(null, [snapshot])!;
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRefreshInventoryFullWarningTimestamp_ReturnsFalse_ForSameSnapshotSequence()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ShouldRefreshInventoryFullWarningTimestamp",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false, sequence: 42);

            bool refresh = (bool)method!.Invoke(null, [42L, 42L, snapshot])!;
            refresh.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRefreshInventoryFullWarningTimestamp_ReturnsTrue_ForNewSnapshotSequence()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ShouldRefreshInventoryFullWarningTimestamp",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false, sequence: 43);

            bool refresh = (bool)method!.Invoke(null, [42L, 43L, snapshot])!;
            refresh.Should().BeTrue();
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
        public void ShouldAutoCopyInventoryWarning_ThrottlesToOneSecond()
        {
            var method = typeof(Rendering.InventoryFullWarningRenderer).GetMethod(
                "ShouldAutoCopyInventoryWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            bool first = (bool)method!.Invoke(null, [1_000L, 0L])!;
            bool throttled = (bool)method!.Invoke(null, [1_500L, 1_000L])!;
            bool allowedAfterWindow = (bool)method!.Invoke(null, [2_000L, 1_000L])!;

            first.Should().BeTrue();
            throttled.Should().BeFalse();
            allowedAfterWindow.Should().BeTrue();
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

        private static LabelFilterService.InventoryDebugSnapshot CreateInventorySnapshot(string stage, bool inventoryFull, bool allowPickup, long sequence = 0)
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
                Sequence: sequence,
                TimestampMs: 0);
        }
    }
}