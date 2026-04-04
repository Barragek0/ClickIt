namespace ClickIt.Tests.Features.Labels.Inventory
{
    [TestClass]
    public class InventoryReadModelServiceTests
    {
        [TestMethod]
        public void TryBuild_ReturnsLayoutFailureSnapshot_WhenLayoutEntriesCannotBeResolved()
        {
            object primaryInventory = new();
            var service = CreateService(
                tryGetPrimaryServerInventory: _ => (true, primaryInventory),
                tryResolveInventoryCapacity: _ => (true, 60),
                tryResolveInventoryDimensions: _ => (true, 12, 5),
                tryResolveInventoryLayoutEntries: (_, _, _) => (false, Array.Empty<InventoryLayoutEntry>(), "slot-items", "read-failed", false, 0));

            bool success = service.TryBuild(null, out InventorySnapshot snapshot);

            success.Should().BeTrue();
            snapshot.HasPrimaryInventory.Should().BeTrue();
            snapshot.PrimaryInventory.Should().BeSameAs(primaryInventory);
            snapshot.CapacityCells.Should().Be(60);
            snapshot.Layout.Entries.Should().BeEmpty();
            snapshot.FullProbe.HasPrimaryInventory.Should().BeTrue();
            snapshot.FullProbe.Notes.Should().Contain("Unable to resolve inventory layout entries from slot-items");
        }

        [TestMethod]
        public void TryBuild_PrefersInventoryFullFlagProbe_WhenAvailable()
        {
            object primaryInventory = new();
            InventoryLayoutEntry[] entries = [new(1, 1, 1, 1)];
            Entity[] inventoryItems = [(Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity))];
            var service = CreateService(
                tryGetPrimaryServerInventory: _ => (true, primaryInventory),
                tryResolveInventoryCapacity: _ => (true, 60),
                tryResolveInventoryDimensions: _ => (true, 12, 5),
                tryResolveInventoryLayoutEntries: (_, _, _) => (true, entries, "slot-items", "raw:1 parsed:1", true, 1),
                tryReadInventoryFullFlag: _ => (true, true, "IsFull"),
                createInventoryFullFlagProbe: (isFull, source) => new InventoryFullProbe(
                    HasPrimaryInventory: true,
                    UsedFullFlag: true,
                    FullFlagValue: isFull,
                    UsedCellOccupancy: false,
                    CapacityCells: 60,
                    OccupiedCells: 0,
                    InventoryEntityCount: 1,
                    LayoutEntryCount: 1,
                    IsFull: isFull,
                    Source: source,
                    Notes: "from-flag"),
                tryEnumeratePrimaryInventoryItemEntitiesFast: _ => (true, inventoryItems));

            bool success = service.TryBuild(null, out InventorySnapshot snapshot);

            success.Should().BeTrue();
            snapshot.FullProbe.UsedFullFlag.Should().BeTrue();
            snapshot.FullProbe.IsFull.Should().BeTrue();
            snapshot.FullProbe.Source.Should().Be("IsFull");
            snapshot.InventoryItems.Should().HaveCount(1);
            snapshot.OccupiedCells.Should().Be(0);
        }

        [TestMethod]
        public void TryBuild_UsesCellOccupancyProbe_WhenLayoutIsReliable_AndNoFullFlagExists()
        {
            object primaryInventory = new();
            InventoryLayoutEntry[] entries = [new(0, 0, 2, 2), new(2, 0, 1, 1)];
            var service = CreateService(
                tryGetPrimaryServerInventory: _ => (true, primaryInventory),
                tryResolveInventoryCapacity: _ => (true, 60),
                tryResolveInventoryDimensions: _ => (true, 12, 5),
                tryResolveInventoryLayoutEntries: (_, _, _) => (true, entries, "slot-items", "raw:2 parsed:2", true, 2),
                tryReadInventoryFullFlag: _ => (false, false, string.Empty),
                tryResolveOccupiedInventoryCellsFromLayout: (_, _, _) => (true, 5),
                isInventoryCellUsageFull: (occupied, capacity) => occupied >= capacity,
                tryEnumeratePrimaryInventoryItemEntitiesFast: _ => (false, Array.Empty<Entity>()));

            bool success = service.TryBuild(null, out InventorySnapshot snapshot);

            success.Should().BeTrue();
            snapshot.Layout.IsReliable.Should().BeTrue();
            snapshot.OccupiedCells.Should().Be(5);
            snapshot.FullProbe.UsedCellOccupancy.Should().BeTrue();
            snapshot.FullProbe.InventoryEntityCount.Should().Be(2);
            snapshot.FullProbe.LayoutEntryCount.Should().Be(2);
            snapshot.FullProbe.IsFull.Should().BeFalse();
            snapshot.FullProbe.Notes.Should().Contain("raw:2 parsed:2");
        }

        private static InventoryReadModelService CreateService(
            Func<GameController?, (bool Success, object? PrimaryInventory)>? tryGetPrimaryServerInventory = null,
            Func<object, (bool Success, int CapacityCells)>? tryResolveInventoryCapacity = null,
            Func<object, (bool Success, int Width, int Height)>? tryResolveInventoryDimensions = null,
            Func<object, int, int, (bool Success, IReadOnlyList<InventoryLayoutEntry> Entries, string Source, string DebugDetails, bool IsReliable, int RawEntryCount)>? tryResolveInventoryLayoutEntries = null,
            Func<object, (bool Success, bool Full, string Source)>? tryReadInventoryFullFlag = null,
            Func<bool, string, InventoryFullProbe>? createInventoryFullFlagProbe = null,
            Func<IReadOnlyList<InventoryLayoutEntry>, int, int, (bool Success, int OccupiedCellCount)>? tryResolveOccupiedInventoryCellsFromLayout = null,
            Func<int, int, bool>? isInventoryCellUsageFull = null,
            Func<object, (bool Success, IReadOnlyList<Entity> Entities)>? tryEnumeratePrimaryInventoryItemEntitiesFast = null)
        {
            return new InventoryReadModelService(new InventorySnapshotProviderDependencies(
                TryGetPrimaryServerInventory: tryGetPrimaryServerInventory ?? (_ => (false, null)),
                TryResolveInventoryCapacity: tryResolveInventoryCapacity ?? (_ => (false, 0)),
                TryResolveInventoryDimensions: tryResolveInventoryDimensions ?? (_ => (false, 0, 0)),
                TryResolveInventoryLayoutEntries: tryResolveInventoryLayoutEntries ?? ((_, _, _) => (false, Array.Empty<InventoryLayoutEntry>(), string.Empty, string.Empty, false, 0)),
                TryReadInventoryFullFlag: tryReadInventoryFullFlag ?? (_ => (false, false, string.Empty)),
                CreateInventoryFullFlagProbe: createInventoryFullFlagProbe ?? ((isFull, source) => new InventoryFullProbe(
                    HasPrimaryInventory: true,
                    UsedFullFlag: true,
                    FullFlagValue: isFull,
                    UsedCellOccupancy: false,
                    CapacityCells: 0,
                    OccupiedCells: 0,
                    InventoryEntityCount: 0,
                    LayoutEntryCount: 0,
                    IsFull: isFull,
                    Source: source,
                    Notes: string.Empty)),
                TryResolveOccupiedInventoryCellsFromLayout: tryResolveOccupiedInventoryCellsFromLayout ?? ((_, _, _) => (false, 0)),
                IsInventoryCellUsageFull: isInventoryCellUsageFull ?? ((_, _) => false),
                TryEnumeratePrimaryInventoryItemEntitiesFast: tryEnumeratePrimaryInventoryItemEntitiesFast ?? (_ => (false, Array.Empty<Entity>()))));
        }
    }
}