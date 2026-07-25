namespace ClickIt.Features.Labels.Inventory
{
    internal static class InventoryCapacityEngine
    {
        internal static bool IsInventoryCellUsageFull(int occupiedCellCount, int totalCellCapacity)
            => InventoryCoreLogic.IsInventoryCellUsageFull(occupiedCellCount, totalCellCapacity);

        internal static bool TryResolveOccupiedInventoryCellsFromLayout(
            IReadOnlyList<InventoryLayoutEntry> layoutEntries,
            int inventoryWidth,
            int inventoryHeight,
            out int occupiedCellCount)
            => InventoryFitEvaluator.TryResolveOccupiedInventoryCellsFromLayout(
                layoutEntries,
                inventoryWidth,
                inventoryHeight,
                out occupiedCellCount);

        internal static bool HasSpaceForItemFootprint(
            int inventoryWidth,
            int inventoryHeight,
            IReadOnlyList<InventoryLayoutEntry> occupiedEntries,
            int requiredWidth,
            int requiredHeight)
            => InventoryFitEvaluator.HasSpaceForItemFootprint(
                inventoryWidth,
                inventoryHeight,
                occupiedEntries,
                requiredWidth,
                requiredHeight);
    }
}