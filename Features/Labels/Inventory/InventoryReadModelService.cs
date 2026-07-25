namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventorySnapshotProviderDependencies(
        Func<GameController?, (bool Success, object? PrimaryInventory)> TryGetPrimaryServerInventory,
        Func<object, (bool Success, int CapacityCells)> TryResolveInventoryCapacity,
        Func<object, (bool Success, int Width, int Height)> TryResolveInventoryDimensions,
        Func<object, int, int, (bool Success, IReadOnlyList<InventoryLayoutEntry> Entries, string Source, string DebugDetails, bool IsReliable, int RawEntryCount)> TryResolveInventoryLayoutEntries,
        Func<object, (bool Success, bool Full, string Source)> TryReadInventoryFullFlag,
        Func<bool, string, InventoryFullProbe> CreateInventoryFullFlagProbe,
        Func<IReadOnlyList<InventoryLayoutEntry>, int, int, (bool Success, int OccupiedCellCount)> TryResolveOccupiedInventoryCellsFromLayout,
        Func<int, int, bool> IsInventoryCellUsageFull,
        Func<object, (bool Success, IReadOnlyList<Entity> Entities)> TryEnumeratePrimaryInventoryItemEntitiesFast);

    internal sealed class InventoryReadModelService(InventorySnapshotProviderDependencies dependencies) : IInventorySnapshotProvider
    {
        private readonly InventorySnapshotProviderDependencies _dependencies = dependencies;

        public bool TryBuild(GameController? gameController, out InventorySnapshot snapshot)
        {
            snapshot = InventorySnapshot.Empty;

            (bool hasPrimaryInventory, object? primaryInventory) = _dependencies.TryGetPrimaryServerInventory(gameController);
            if (!hasPrimaryInventory || primaryInventory == null)
                return false;

            (bool hasCapacity, int totalCellCapacity) = _dependencies.TryResolveInventoryCapacity(primaryInventory);
            if (!hasCapacity)
                return false;

            (bool hasDimensions, int inventoryWidth, int inventoryHeight) = _dependencies.TryResolveInventoryDimensions(primaryInventory);
            if (!hasDimensions)
                return false;

            (bool hasLayout, IReadOnlyList<InventoryLayoutEntry> layoutEntries, string layoutSource, string layoutDebugDetails, bool isLayoutReliable, int rawEntryCount)
                = _dependencies.TryResolveInventoryLayoutEntries(primaryInventory, inventoryWidth, inventoryHeight);
            if (!hasLayout)
            {
                InventoryFullProbe layoutFailureProbe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    Notes = $"Unable to resolve inventory layout entries from {layoutSource} ({layoutDebugDetails})"
                };

                snapshot = InventorySnapshot.Empty with
                {
                    HasPrimaryInventory = true,
                    PrimaryInventory = primaryInventory,
                    CapacityCells = totalCellCapacity,
                    Width = inventoryWidth,
                    Height = inventoryHeight,
                    FullProbe = layoutFailureProbe
                };
                return true;
            }

            InventoryLayoutSnapshot layoutSnapshot = new(
                Entries: layoutEntries,
                Source: layoutSource,
                DebugDetails: layoutDebugDetails,
                IsReliable: isLayoutReliable,
                RawEntryCount: rawEntryCount);

            (bool hasFullFlag, bool fullFlagValue, string fullFlagSource) = _dependencies.TryReadInventoryFullFlag(primaryInventory);
            if (hasFullFlag)
            {
                InventoryFullProbe fullFlagProbe = _dependencies.CreateInventoryFullFlagProbe(fullFlagValue, fullFlagSource);
                snapshot = new InventorySnapshot(
                    HasPrimaryInventory: true,
                    PrimaryInventory: primaryInventory,
                    CapacityCells: totalCellCapacity,
                    Width: inventoryWidth,
                    Height: inventoryHeight,
                    Layout: layoutSnapshot,
                    OccupiedCells: 0,
                    FullProbe: fullFlagProbe,
                    InventoryItems: ResolveInventoryItems(primaryInventory));
                return true;
            }

            if (!isLayoutReliable)
            {
                InventoryFullProbe unreliableProbe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    InventoryEntityCount = rawEntryCount,
                    LayoutEntryCount = layoutEntries.Count,
                    Notes = $"Inventory layout unreliable from {layoutSource} ({layoutDebugDetails})"
                };

                snapshot = new InventorySnapshot(
                    HasPrimaryInventory: true,
                    PrimaryInventory: primaryInventory,
                    CapacityCells: totalCellCapacity,
                    Width: inventoryWidth,
                    Height: inventoryHeight,
                    Layout: layoutSnapshot,
                    OccupiedCells: 0,
                    FullProbe: unreliableProbe,
                    InventoryItems: ResolveInventoryItems(primaryInventory));
                return true;
            }

            (bool hasOccupiedCellCount, int occupiedCellCount) = _dependencies.TryResolveOccupiedInventoryCellsFromLayout(layoutEntries, inventoryWidth, inventoryHeight);
            if (!hasOccupiedCellCount)
            {
                InventoryFullProbe occupiedCellFailureProbe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    InventoryEntityCount = rawEntryCount,
                    LayoutEntryCount = layoutEntries.Count,
                    Notes = $"Unable to resolve occupied cells from {layoutSource}"
                };

                snapshot = new InventorySnapshot(
                    HasPrimaryInventory: true,
                    PrimaryInventory: primaryInventory,
                    CapacityCells: totalCellCapacity,
                    Width: inventoryWidth,
                    Height: inventoryHeight,
                    Layout: layoutSnapshot,
                    OccupiedCells: 0,
                    FullProbe: occupiedCellFailureProbe,
                    InventoryItems: ResolveInventoryItems(primaryInventory));
                return true;
            }

            bool isFull = _dependencies.IsInventoryCellUsageFull(occupiedCellCount, totalCellCapacity);
            InventoryFullProbe occupancyProbe = new(
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: totalCellCapacity,
                OccupiedCells: occupiedCellCount,
                InventoryEntityCount: rawEntryCount,
                LayoutEntryCount: layoutEntries.Count,
                IsFull: isFull,
                Source: "CellOccupancy",
                Notes: $"Inventory fullness from {layoutSource} footprint ({layoutDebugDetails})");

            snapshot = new InventorySnapshot(
                HasPrimaryInventory: true,
                PrimaryInventory: primaryInventory,
                CapacityCells: totalCellCapacity,
                Width: inventoryWidth,
                Height: inventoryHeight,
                Layout: layoutSnapshot,
                OccupiedCells: occupiedCellCount,
                FullProbe: occupancyProbe,
                InventoryItems: ResolveInventoryItems(primaryInventory));
            return true;
        }

        private IReadOnlyList<Entity> ResolveInventoryItems(object primaryInventory)
        {
            (bool success, IReadOnlyList<Entity> entities) = _dependencies.TryEnumeratePrimaryInventoryItemEntitiesFast(primaryInventory);
            return success ? entities : [];
        }
    }
}