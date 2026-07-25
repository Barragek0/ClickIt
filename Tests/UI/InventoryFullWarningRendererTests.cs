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

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_PrefersPlayerFeet_WhenAvailable()
        {
            Vector2 result = InventoryFullWarningRenderer.ResolveInventoryFullWarningPosition(
                new RectangleF(0, 0, 1920, 1080),
                new RectangleF(10, 20, 110, 120),
                new RectangleF(1810, 20, 1910, 120),
                new Vector2(640f, 480f));

            result.Should().Be(new Vector2(640f, 480f));
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_UsesWindowFallback_WhenTertiariesAreMissing()
        {
            Vector2 result = InventoryFullWarningRenderer.ResolveInventoryFullWarningPosition(
                new RectangleF(100, 50, 1200, 700),
                RectangleF.Empty,
                RectangleF.Empty,
                playerFeetScreen: null);

            result.Should().Be(new Vector2(700f, 652f));
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_UsesWindowFallback_WhenOnlyOneTertiaryIsValid()
        {
            Vector2 result = InventoryFullWarningRenderer.ResolveInventoryFullWarningPosition(
                new RectangleF(0, 0, 1600, 900),
                new RectangleF(10, 20, 150, 220),
                RectangleF.Empty,
                playerFeetScreen: null);

            result.Should().Be(new Vector2(800f, 774f));
        }

        [TestMethod]
        public void ResolveInventoryFullWarningPosition_UsesMidpointBetweenValidTertiaries()
        {
            Vector2 result = InventoryFullWarningRenderer.ResolveInventoryFullWarningPosition(
                new RectangleF(0, 0, 1600, 900),
                new RectangleF(40, 100, 240, 300),
                new RectangleF(1160, 120, 1360, 360),
                playerFeetScreen: null);

            result.Should().Be(new Vector2(700f, 230f));
        }

        [TestMethod]
        public void ShouldShowInventoryFullWarning_ReturnsExpectedState_ForInitialExpiredAndFutureTimestamps()
        {
            InventoryFullWarningRenderer.ShouldShowInventoryFullWarning(now: 1000, lastTriggeredTimestampMs: 0)
                .Should().BeFalse();

            InventoryFullWarningRenderer.ShouldShowInventoryFullWarning(now: 15_500, lastTriggeredTimestampMs: 5_000)
                .Should().BeFalse();

            InventoryFullWarningRenderer.ShouldShowInventoryFullWarning(now: 4_000, lastTriggeredTimestampMs: 5_000)
                .Should().BeFalse();

            InventoryFullWarningRenderer.ShouldShowInventoryFullWarning(now: 14_999, lastTriggeredTimestampMs: 5_000)
                .Should().BeTrue();
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
