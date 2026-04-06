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
    }
}