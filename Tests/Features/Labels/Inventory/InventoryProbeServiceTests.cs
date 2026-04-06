namespace ClickIt.Tests.Features.Labels.Inventory
{
    [TestClass]
    public class InventoryProbeServiceTests
    {
        [TestMethod]
        public void GetLatestDebug_ReturnsEmpty_WhenNoDebugWasPublished()
        {
            InventoryProbeService service = CreateProbeService();

            InventoryDebugSnapshot snapshot = service.GetLatestDebug();

            snapshot.Should().Be(InventoryDebugSnapshot.Empty);
        }

        [TestMethod]
        public void GetLatestDebugTrail_ReturnsPublishedTrailEntries()
        {
            InventoryProbeService service = CreateProbeService();

            service.PublishDebug(CreateDebugSnapshot("First", 1));
            service.PublishDebug(CreateDebugSnapshot("Second", 2));

            IReadOnlyList<string> trail = service.GetLatestDebugTrail();

            trail.Should().NotBeEmpty();
            trail[^1].Should().Contain("Second");
        }

        private static InventoryProbeService CreateProbeService()
        {
            return new InventoryProbeService(new InventoryProbeServiceDependencies(
                CacheWindowMs: 50,
                DebugTrailCapacity: 8,
                TryBuildInventorySnapshot: _ => (true, default),
                LayoutCache: new InventoryLayoutCache(cacheWindowMs: 50)));
        }

        private static InventoryDebugSnapshot CreateDebugSnapshot(string stage, long sequence)
        {
            return new InventoryDebugSnapshot(
                HasData: true,
                Stage: stage,
                InventoryFull: false,
                InventoryFullSource: "CellOccupancy",
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: 60,
                OccupiedCells: 12,
                InventoryEntityCount: 10,
                LayoutEntryCount: 10,
                GroundItemPath: "Metadata/Items/Test",
                GroundItemName: "Test",
                IsGroundStackable: false,
                MatchingPathCount: 0,
                PartialMatchingStackCount: 0,
                HasPartialMatchingStack: false,
                DecisionAllowPickup: true,
                Notes: string.Empty,
                Sequence: sequence,
                TimestampMs: 0);
        }
    }
}