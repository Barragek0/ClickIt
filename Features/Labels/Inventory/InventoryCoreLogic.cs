using ClickIt.Shared;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Features.Labels.Inventory
{
    internal static class InventoryCoreLogic
    {
        private const string InventoryLayoutUnreliableNotesPrefix = "Inventory layout unreliable";

        public static bool ShouldPickupWhenInventoryFull(bool inventoryFull, bool isStackable, bool hasPartialMatchingStack)
            => !inventoryFull || (isStackable && hasPartialMatchingStack);

        public static bool IsPartialStack(int currentStackSize, int maxStackSize)
            => currentStackSize > 0 && maxStackSize > 0 && currentStackSize < maxStackSize;

        public static bool IsPartialServerStack(bool fullStack, int size)
            => size > 0 && !fullStack;

        public static bool IsInventoryCellUsageFull(int occupiedCellCount, int totalCellCapacity)
            => totalCellCapacity > 0 && occupiedCellCount >= totalCellCapacity;

        public static bool ShouldAllowPickupWhenPrimaryInventoryMissing(bool hasPrimaryInventory, string notes)
            => !hasPrimaryInventory && notes == "Primary server inventory missing";

        public static bool ShouldAllowPickupWhenGroundItemEntityMissing(bool inventoryFull, object? groundItemEntity)
            => !inventoryFull && groundItemEntity == null;

        public static bool ShouldAllowPickupWhenGroundItemIdentityMissing(bool inventoryFull, string? groundItemPath, string? groundItemName)
            => !inventoryFull
                && string.IsNullOrWhiteSpace(groundItemPath)
                && string.IsNullOrWhiteSpace(groundItemName);

        public static bool IsInventoryLayoutUnreliableNotes(string? notes)
            => !string.IsNullOrWhiteSpace(notes)
               && notes.StartsWith(InventoryLayoutUnreliableNotesPrefix, StringComparison.Ordinal);

        public static bool ShouldAllowClosedDoorPastMechanic(bool hasStoneOfPassageInInventory, string? inventoryProbeNotes)
            => hasStoneOfPassageInInventory || IsInventoryLayoutUnreliableNotes(inventoryProbeNotes);

        public static bool TryResolveInventoryItemSize(Entity itemEntity, out int width, out int height)
        {
            width = 1;
            height = 1;

            try
            {
                Base? baseComponent = itemEntity.GetComponent<Base>();
                return TryResolveInventoryItemSizeFromBase(baseComponent, out width, out height);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryResolveInventoryItemSizeFromBase(object? baseComponent, out int width, out int height)
        {
            width = 1;
            height = 1;

            if (baseComponent == null)
                return false;

            if (TryResolveInventoryItemCellSizeFromInfo(baseComponent, out width, out height))
            {
                width = Math.Max(1, width);
                height = Math.Max(1, height);
                return true;
            }

            if (!DynamicAccess.TryReadInt(baseComponent, static s => s.ItemCellsSizeX, out width)
                || !DynamicAccess.TryReadInt(baseComponent, static s => s.ItemCellsSizeY, out height))
            {
                return false;
            }

            width = Math.Max(1, width);
            height = Math.Max(1, height);
            return true;
        }

        public static bool TryResolveFallbackInventoryItemSizeFromPath(string? metadataPath, out int width, out int height)
        {
            width = 1;
            height = 1;

            if (string.IsNullOrWhiteSpace(metadataPath))
                return false;

            if (metadataPath.StartsWith("Metadata/Items/Currency/", StringComparison.OrdinalIgnoreCase)
                || metadataPath.StartsWith("Metadata/Items/DivinationCards/", StringComparison.OrdinalIgnoreCase)
                || metadataPath.StartsWith("Metadata/Items/Maps/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveInventoryItemCellSizeFromInfo(object baseComponent, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (!DynamicAccess.TryGetDynamicValue(baseComponent, static s => s.Info, out object? info) || info == null)
                return false;

            if (!DynamicAccess.TryReadInt(info, static s => s.ItemCellsSizeX, out width)
                || !DynamicAccess.TryReadInt(info, static s => s.ItemCellsSizeY, out height))
            {
                return false;
            }

            return width > 0 && height > 0;
        }
    }
}