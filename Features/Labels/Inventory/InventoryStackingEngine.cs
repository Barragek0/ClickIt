namespace ClickIt.Features.Labels.Inventory
{
    internal static class InventoryStackingEngine
    {
        internal static bool IsPartialStack(int currentStackSize, int maxStackSize)
            => InventoryCoreLogic.IsPartialStack(currentStackSize, maxStackSize);

        internal static bool IsPartialServerStack(bool fullStack, int size)
            => InventoryCoreLogic.IsPartialServerStack(fullStack, size);

        internal static bool ShouldAllowIncubatorStackMatch(
            bool requiresIncubatorLevelMatch,
            bool hasGroundIncubatorLevel,
            int groundIncubatorLevel,
            bool hasInventoryIncubatorLevel,
            int inventoryIncubatorLevel)
        {
            if (!requiresIncubatorLevelMatch)
                return true;

            if (!hasGroundIncubatorLevel || !hasInventoryIncubatorLevel)
                return false;

            return groundIncubatorLevel == inventoryIncubatorLevel;
        }
    }
}