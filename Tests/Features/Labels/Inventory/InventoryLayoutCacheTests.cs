namespace ClickIt.Tests.Features.Labels.Inventory
{
    [TestClass]
    public class InventoryLayoutCacheTests
    {
        [TestMethod]
        public void Set_ThenTryGet_ReturnsStoredSnapshot_WhenInputsStillMatch()
        {
            var cache = new InventoryLayoutCache(cacheWindowMs: 50);
            object primaryInventory = new();
            InventoryLayoutSnapshot expected = new(
                Entries: [new InventoryLayoutEntry(1, 2, 3, 4)],
                Source: "slot-items",
                DebugDetails: "raw:1 parsed:1",
                IsReliable: true,
                RawEntryCount: 1);
            long now = Environment.TickCount64;

            cache.Set(primaryInventory, now, 12, 5, expected);

            bool cached = cache.TryGet(primaryInventory, now, 12, 5, out InventoryLayoutSnapshot snapshot);

            cached.Should().BeTrue();
            snapshot.Source.Should().Be(expected.Source);
            snapshot.DebugDetails.Should().Be(expected.DebugDetails);
            snapshot.IsReliable.Should().BeTrue();
            snapshot.RawEntryCount.Should().Be(1);
            snapshot.Entries.Should().HaveCount(1);
        }

        [TestMethod]
        public void Clear_RemovesStoredSnapshot()
        {
            var cache = new InventoryLayoutCache(cacheWindowMs: 50);
            object primaryInventory = new();
            long now = Environment.TickCount64;

            cache.Set(primaryInventory, now, 12, 5, new InventoryLayoutSnapshot(
                Entries: [new InventoryLayoutEntry(0, 0, 1, 1)],
                Source: "slot-items",
                DebugDetails: "cached",
                IsReliable: true,
                RawEntryCount: 1));
            cache.Clear();

            bool cached = cache.TryGet(primaryInventory, now, 12, 5, out InventoryLayoutSnapshot snapshot);

            cached.Should().BeFalse();
            snapshot.Should().Be(InventoryLayoutSnapshot.Empty);
        }
    }
}