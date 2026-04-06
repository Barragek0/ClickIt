namespace ClickIt.Tests.UI
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
