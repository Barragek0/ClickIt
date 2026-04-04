namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryLayoutParserDependencies(
        Func<object, (bool Success, object? SlotItemsCollection)> TryGetPrimaryServerInventorySlotItems,
        InventoryLayoutCache LayoutCache,
        InventoryLayoutEntryResolver EntryResolver,
        Func<object?, Func<dynamic, object?>, (bool Success, int Value)> TryReadInt);

    internal sealed class InventoryLayoutParser
    {
        private readonly InventoryLayoutParserDependencies _dependencies;

        public InventoryLayoutParser(InventoryLayoutParserDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public bool TryResolveInventoryCapacity(object primaryInventory, out int totalCellCapacity)
        {
            totalCellCapacity = 0;

            if (TryReadInt(primaryInventory, static s => s.Width, out int width)
                && TryReadInt(primaryInventory, static s => s.Height, out int height)
                && width > 0
                && height > 0)
            {
                totalCellCapacity = width * height;
                return true;
            }

            if (TryReadInt(primaryInventory, static s => s.TotalBoxes, out int totalBoxes) && totalBoxes > 0)
            {
                totalCellCapacity = totalBoxes;
                return true;
            }

            if (TryReadInt(primaryInventory, static s => s.Capacity, out int capacity) && capacity > 0)
            {
                totalCellCapacity = capacity;
                return true;
            }

            totalCellCapacity = 60;
            return true;
        }

        public bool TryResolveInventoryDimensions(object primaryInventory, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (TryReadInt(primaryInventory, static s => s.Width, out int resolvedWidth)
                && TryReadInt(primaryInventory, static s => s.Height, out int resolvedHeight)
                && resolvedWidth > 0
                && resolvedHeight > 0)
            {
                width = resolvedWidth;
                height = resolvedHeight;
                return true;
            }

            if (TryReadInt(primaryInventory, static s => s.TotalBoxes, out int totalBoxes) && totalBoxes > 0)
            {
                width = 12;
                height = Math.Max(1, totalBoxes / 12);
                return true;
            }

            if (TryReadInt(primaryInventory, static s => s.Capacity, out int capacity) && capacity > 0)
            {
                width = 12;
                height = Math.Max(1, capacity / 12);
                return true;
            }

            width = 12;
            height = 5;
            return true;
        }

        public bool TryResolveInventoryLayoutEntries(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight,
            out IReadOnlyList<InventoryLayoutEntry> entries,
            out string source,
            out string debugDetails,
            out bool isReliable,
            out int rawEntryCount)
        {
            entries = Array.Empty<InventoryLayoutEntry>();
            source = string.Empty;
            debugDetails = string.Empty;
            isReliable = false;
            rawEntryCount = 0;

            if (inventoryWidth <= 0 || inventoryHeight <= 0)
                return false;

            long now = Environment.TickCount64;
            bool hasCachedLayout = _dependencies.LayoutCache.TryGet(primaryInventory, now, inventoryWidth, inventoryHeight, out InventoryLayoutSnapshot cachedSnapshot);
            if (hasCachedLayout)
            {
                entries = cachedSnapshot.Entries;
                source = cachedSnapshot.Source;
                debugDetails = cachedSnapshot.DebugDetails;
                isReliable = cachedSnapshot.IsReliable;
                rawEntryCount = cachedSnapshot.RawEntryCount;
                return true;
            }

            (bool hasSlotItems, object? slotItemsCollection) = _dependencies.TryGetPrimaryServerInventorySlotItems(primaryInventory);
            if (hasSlotItems && slotItemsCollection != null)
            {
                if (_dependencies.EntryResolver.TryBuildInventoryLayoutEntriesFromCollection(slotItemsCollection, inventoryWidth, inventoryHeight, out List<InventoryLayoutEntry> slotEntries, out int slotRawCount))
                {
                    entries = slotEntries;
                    source = "PlayerInventories[0].InventorySlotItems";
                    rawEntryCount = slotRawCount;
                    isReliable = slotRawCount == 0 || slotEntries.Count > 0;
                    debugDetails = $"raw:{slotRawCount} parsed:{slotEntries.Count}";
                    _dependencies.LayoutCache.Set(primaryInventory, now, inventoryWidth, inventoryHeight, new InventoryLayoutSnapshot(entries, source, debugDetails, isReliable, rawEntryCount));
                    return true;
                }
            }

            source = "PlayerInventories[0].InventorySlotItems";
            debugDetails = "read-failed: PlayerInventories[0].InventorySlotItems accessor unavailable or unreadable";
            _dependencies.LayoutCache.Set(primaryInventory, now, inventoryWidth, inventoryHeight, new InventoryLayoutSnapshot(entries, source, debugDetails, isReliable, rawEntryCount));
            return false;
        }

        private bool TryReadInt(object? source, Func<dynamic, object?> accessor, out int value)
        {
            (bool success, int resolvedValue) = _dependencies.TryReadInt(source, accessor);
            value = resolvedValue;
            return success;
        }
    }
}