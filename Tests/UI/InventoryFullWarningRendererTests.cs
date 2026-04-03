using ClickIt.Features.Labels.Inventory;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.UserInterface
{
    [TestClass]
    public class InventoryFullWarningRendererTests
    {
        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsTrue_WhenFullDecisionDisallowsPickup()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryFullDecision", inventoryFull: true, allowPickup: false);

            bool blocked = InventoryFullWarningRenderer.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsTrue_WhenNotFullNoFitDisallowsPickup()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false);

            bool blocked = InventoryFullWarningRenderer.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsFalse_WhenNotFullNoFitHasMissingIdentity()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false) with
            {
                GroundItemPath = string.Empty,
                GroundItemName = string.Empty
            };

            bool blocked = InventoryFullWarningRenderer.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsFalse_WhenNotFullNoFitLayoutIsUnreliable()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false) with
            {
                Notes = "Inventory layout unreliable from PlayerInventories[0].InventorySlotItems (raw:1 parsed:0)"
            };

            bool blocked = InventoryFullWarningRenderer.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldShowInventoryPickupBlockedWarning_ReturnsFalse_WhenNotFullNoFitHasLargeFreeCellCount()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false) with
            {
                CapacityCells = 60,
                OccupiedCells = 2,
                UsedCellOccupancy = true,
                Notes = "Inventory fullness from PlayerInventories[0].InventorySlotItems footprint (raw:2 parsed:2)",
                GroundItemPath = "Metadata/Items/Currency/CurrencyRerollRare",
                GroundItemName = string.Empty,
                IsGroundStackable = false
            };

            bool blocked = InventoryFullWarningRenderer.ShouldShowInventoryPickupBlockedWarning(snapshot);
            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRefreshInventoryFullWarningTimestamp_ReturnsFalse_ForSameSnapshotSequence()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false, sequence: 42);

            bool refresh = InventoryFullWarningRenderer.ShouldRefreshInventoryFullWarningTimestamp(42L, 42L, snapshot);
            refresh.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRefreshInventoryFullWarningTimestamp_ReturnsTrue_ForNewSnapshotSequence()
        {
            var snapshot = CreateInventorySnapshot(stage: "InventoryNotFullNoFit", inventoryFull: false, allowPickup: false, sequence: 43);

            bool refresh = InventoryFullWarningRenderer.ShouldRefreshInventoryFullWarningTimestamp(42L, 43L, snapshot);
            refresh.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldShowInventoryFullWarning_HidesAfterTenSeconds()
        {
            bool withinWindow = InventoryFullWarningRenderer.ShouldShowInventoryFullWarning(10_000L, 1_000L);
            bool expired = InventoryFullWarningRenderer.ShouldShowInventoryFullWarning(11_001L, 1_000L);

            withinWindow.Should().BeTrue();
            expired.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAutoCopyInventoryWarning_ThrottlesToOneSecond()
        {
            bool first = InventoryFullWarningRenderer.ShouldAutoCopyInventoryWarning(1_000L, 0L);
            bool throttled = InventoryFullWarningRenderer.ShouldAutoCopyInventoryWarning(1_500L, 1_000L);
            bool allowedAfterWindow = InventoryFullWarningRenderer.ShouldAutoCopyInventoryWarning(2_000L, 1_000L);

            first.Should().BeTrue();
            throttled.Should().BeFalse();
            allowedAfterWindow.Should().BeTrue();
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_UsesBetweenTertiaryRectangles()
        {
            RectangleF window = new RectangleF(0, 0, 1920, 1080);
            RectangleF left = new RectangleF(400, 900, 520, 1040);
            RectangleF right = new RectangleF(1400, 900, 1520, 1040);

            var pos = InventoryFullWarningRenderer.ResolveInventoryFullWarningPosition(window, left, right, null);

            pos.X.Should().BeApproximately(960f, 0.01f);
            pos.Y.Should().BeApproximately(970f, 0.01f);
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_PrefersPlayerFeetPosition()
        {
            RectangleF window = new RectangleF(0, 0, 1920, 1080);
            RectangleF left = new RectangleF(400, 900, 520, 1040);
            RectangleF right = new RectangleF(1400, 900, 1520, 1040);
            Vector2 feet = new Vector2(777f, 888f);

            var pos = InventoryFullWarningRenderer.ResolveInventoryFullWarningPosition(window, left, right, (Vector2?)feet);

            pos.Should().Be(feet);
        }

        private static InventoryDebugSnapshot CreateInventorySnapshot(string stage, bool inventoryFull, bool allowPickup, long sequence = 0)
        {
            return new InventoryDebugSnapshot(
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
