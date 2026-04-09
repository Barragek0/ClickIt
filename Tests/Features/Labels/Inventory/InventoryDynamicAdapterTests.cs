namespace ClickIt.Tests.Features.Labels.Inventory
{
    [TestClass]
    public class InventoryDynamicAdapterTests
    {
        public sealed class FakeInventoryWrapper
        {
            public object? InventorySlotItems { get; set; }
        }

        public sealed class FakePrimaryInventory
        {
            public object? Inventory { get; set; }
        }

        [TestMethod]
        public void TryGetPrimaryServerInventorySlotItems_ReturnsSlotItemsCollection_WhenInventoryPathResolves()
        {
            object expectedCollection = new object[] { new object(), new object() };
            FakePrimaryInventory primaryInventory = new()
            {
                Inventory = new FakeInventoryWrapper
                {
                    InventorySlotItems = expectedCollection
                }
            };

            bool success = InventoryDynamicAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection);

            success.Should().BeTrue();
            slotItemsCollection.Should().BeSameAs(expectedCollection);
        }

        [TestMethod]
        public void TryGetPrimaryServerInventorySlotItems_ReturnsFalse_WhenInventorySlotItemsMissing()
        {
            FakePrimaryInventory primaryInventory = new()
            {
                Inventory = new FakeInventoryWrapper()
            };

            bool success = InventoryDynamicAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection);

            success.Should().BeFalse();
            slotItemsCollection.Should().BeNull();
        }
    }
}