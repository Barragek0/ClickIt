namespace ClickIt.Tests.Features.Labels.Inventory
{
    [TestClass]
    public class InventoryCoreLogicTests
    {
        public sealed class FakeBaseInfo
        {
            public int ItemCellsSizeX { get; set; }
            public int ItemCellsSizeY { get; set; }
            public int SizeX { get; set; }
            public int SizeY { get; set; }
        }

        public sealed class FakeBaseWithInfo
        {
            public FakeBaseInfo Info { get; set; } = new FakeBaseInfo();
        }

        [TestMethod]
        public void HasSpaceForItemFootprintCore_ReturnsFalse_WhenFreeCellsAreFragmentedForTwoByFour()
        {
            const int inventoryWidth = 3;
            const int inventoryHeight = 5;

            var occupied = new List<InventoryLayoutEntry>
            {
                new(0, 0, 1, 4),
                new(1, 2, 1, 1),
                new(1, 3, 1, 1),
                new(2, 4, 1, 1)
            };

            bool hasSpace = InventoryFitEvaluator.HasSpaceForItemFootprint(
                inventoryWidth,
                inventoryHeight,
                occupied,
                requiredWidth: 2,
                requiredHeight: 4);

            hasSpace.Should().BeFalse();
        }

        [TestMethod]
        public void HasSpaceForItemFootprintCore_ReturnsTrue_WhenContiguousTwoByFourSpaceExists()
        {
            const int inventoryWidth = 3;
            const int inventoryHeight = 5;

            var occupied = new List<InventoryLayoutEntry>
            {
                new(0, 0, 1, 5)
            };

            bool hasSpace = InventoryFitEvaluator.HasSpaceForItemFootprint(
                inventoryWidth,
                inventoryHeight,
                occupied,
                requiredWidth: 2,
                requiredHeight: 4);

            hasSpace.Should().BeTrue();
        }

        [TestMethod]
        public void TryResolveInventoryItemSizeFromBase_PrefersInfoItemCellsSize()
        {
            var fakeBase = new FakeBaseWithInfo
            {
                Info = new FakeBaseInfo
                {
                    ItemCellsSizeX = 2,
                    ItemCellsSizeY = 4
                }
            };

            bool resolved = InventoryCoreLogic.TryResolveInventoryItemSizeFromBase(fakeBase, out int width, out int height);

            resolved.Should().BeTrue();
            width.Should().Be(2);
            height.Should().Be(4);
        }

        [TestMethod]
        public void ShouldPickupWhenInventoryFull_AllowsOnlyNonFullOrPartialStackCases()
        {
            InventoryCoreLogic.ShouldPickupWhenInventoryFull(inventoryFull: false, isStackable: false, hasPartialMatchingStack: false)
                .Should().BeTrue();

            InventoryCoreLogic.ShouldPickupWhenInventoryFull(inventoryFull: true, isStackable: true, hasPartialMatchingStack: true)
                .Should().BeTrue();

            InventoryCoreLogic.ShouldPickupWhenInventoryFull(inventoryFull: true, isStackable: true, hasPartialMatchingStack: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void StackPredicates_ReturnExpectedValues_ForPartialAndServerStacks()
        {
            InventoryCoreLogic.IsPartialStack(currentStackSize: 3, maxStackSize: 10).Should().BeTrue();
            InventoryCoreLogic.IsPartialStack(currentStackSize: 10, maxStackSize: 10).Should().BeFalse();

            InventoryCoreLogic.IsPartialServerStack(fullStack: false, size: 3).Should().BeTrue();
            InventoryCoreLogic.IsPartialServerStack(fullStack: true, size: 3).Should().BeFalse();
            InventoryCoreLogic.IsPartialServerStack(fullStack: false, size: 0).Should().BeFalse();
        }

        [TestMethod]
        public void IsInventoryCellUsageFull_RequiresPositiveCapacity_AndEnoughOccupiedCells()
        {
            InventoryCoreLogic.IsInventoryCellUsageFull(occupiedCellCount: 60, totalCellCapacity: 60).Should().BeTrue();
            InventoryCoreLogic.IsInventoryCellUsageFull(occupiedCellCount: 59, totalCellCapacity: 60).Should().BeFalse();
            InventoryCoreLogic.IsInventoryCellUsageFull(occupiedCellCount: 0, totalCellCapacity: 0).Should().BeFalse();
        }

        [TestMethod]
        public void MissingInventoryAndIdentityPredicates_ReturnExpectedValues()
        {
            InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing(hasPrimaryInventory: false, notes: "Primary server inventory missing")
                .Should().BeTrue();
            InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing(hasPrimaryInventory: false, notes: "other")
                .Should().BeFalse();

            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing(inventoryFull: false, groundItemEntity: null)
                .Should().BeTrue();
            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing(inventoryFull: true, groundItemEntity: null)
                .Should().BeFalse();

            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing(inventoryFull: false, groundItemPath: null, groundItemName: " ")
                .Should().BeTrue();
            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing(inventoryFull: true, groundItemPath: null, groundItemName: null)
                .Should().BeFalse();
        }

        [TestMethod]
        public void InventoryLayoutUnreliableNotes_AndClosedDoorMechanicOverride_ReturnExpectedValues()
        {
            const string unreliable = "Inventory layout unreliable from PlayerInventories[0].InventorySlotItems";

            InventoryCoreLogic.IsInventoryLayoutUnreliableNotes(unreliable).Should().BeTrue();
            InventoryCoreLogic.IsInventoryLayoutUnreliableNotes("Inventory probe stable").Should().BeFalse();

            InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(hasStoneOfPassageInInventory: true, inventoryProbeNotes: null)
                .Should().BeTrue();
            InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(hasStoneOfPassageInInventory: false, inventoryProbeNotes: unreliable)
                .Should().BeTrue();
            InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(hasStoneOfPassageInInventory: false, inventoryProbeNotes: "stable")
                .Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveFallbackInventoryItemSizeFromPath_ReturnsTrue_ForKnownSingleCellFamilies()
        {
            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath("Metadata/Items/Currency/CurrencyRerollRare", out int currencyWidth, out int currencyHeight)
                .Should().BeTrue();
            currencyWidth.Should().Be(1);
            currencyHeight.Should().Be(1);

            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath("Metadata/Items/DivinationCards/CardTest", out int cardWidth, out int cardHeight)
                .Should().BeTrue();
            cardWidth.Should().Be(1);
            cardHeight.Should().Be(1);

            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath("Metadata/Items/Maps/MapTest", out int mapWidth, out int mapHeight)
                .Should().BeTrue();
            mapWidth.Should().Be(1);
            mapHeight.Should().Be(1);
        }

        [TestMethod]
        public void TryResolveFallbackInventoryItemSizeFromPath_ReturnsFalse_ForUnknownOrBlankPaths()
        {
            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath(null, out _, out _)
                .Should().BeFalse();

            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath("Metadata/Items/Armours/BodyArmours/Test", out _, out _)
                .Should().BeFalse();
        }
    }
}