using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryLayoutParserDependencies(
        Func<object?, IEnumerable<object?>> EnumerateObjects,
        Func<object, long, int, int, (bool Success, InventoryLayoutSnapshot Snapshot)> TryGetCachedInventoryLayout,
        Action<object, long, int, int, InventoryLayoutSnapshot> SetCachedInventoryLayout,
        Func<object, (bool Success, object? SlotItemsCollection)> TryGetPrimaryServerInventorySlotItems,
        Func<object, Entity?> TryGetInventoryItemEntityFromEntry,
        Func<Entity, (bool Success, int Width, int Height)> TryResolveInventoryItemSize,
        Func<object?, Func<dynamic, object?>, (bool Success, object? Value)> TryGetDynamicValue,
        Func<object?, Func<dynamic, object?>, (bool Success, int Value)> TryReadInt);

    internal interface IInventoryIntMemberReadStrategy
    {
        string Name { get; }
        bool TryRead(object source, out int value);
    }

    internal sealed class InventoryIntMemberReadStrategy(string name, Func<object, (bool Success, int Value)> reader) : IInventoryIntMemberReadStrategy
    {
        public string Name { get; } = name;

        public bool TryRead(object source, out int value)
        {
            (bool success, int resolvedValue) = reader(source);
            value = resolvedValue;
            return success;
        }
    }

    internal sealed class InventoryLayoutParser
    {
        private readonly InventoryLayoutParserDependencies _dependencies;
        private readonly IInventoryIntMemberReadStrategy[] _intMemberReadStrategies;

        public InventoryLayoutParser(InventoryLayoutParserDependencies dependencies)
        {
            _dependencies = dependencies;
            _intMemberReadStrategies =
            [
                CreateIntReadStrategy("PosX", source => ReadIntDynamic(source, static d => d.PosX)),
                CreateIntReadStrategy("PosY", source => ReadIntDynamic(source, static d => d.PosY)),
                CreateIntReadStrategy("InventoryX", source => ReadIntDynamic(source, static d => d.InventoryX)),
                CreateIntReadStrategy("InventoryY", source => ReadIntDynamic(source, static d => d.InventoryY)),
                CreateIntReadStrategy("ItemCellX", source => ReadIntDynamic(source, static d => d.ItemCellX)),
                CreateIntReadStrategy("ItemCellY", source => ReadIntDynamic(source, static d => d.ItemCellY)),
                CreateIntReadStrategy("CellX", source => ReadIntDynamic(source, static d => d.CellX)),
                CreateIntReadStrategy("CellY", source => ReadIntDynamic(source, static d => d.CellY)),
                CreateIntReadStrategy("PositionX", source => ReadIntDynamic(source, static d => d.PositionX)),
                CreateIntReadStrategy("PositionY", source => ReadIntDynamic(source, static d => d.PositionY)),
                CreateIntReadStrategy("X", source => ReadIntDynamic(source, static d => d.X)),
                CreateIntReadStrategy("Y", source => ReadIntDynamic(source, static d => d.Y)),
                CreateIntReadStrategy("Column", source => ReadIntDynamic(source, static d => d.Column)),
                CreateIntReadStrategy("Row", source => ReadIntDynamic(source, static d => d.Row)),
                CreateIntReadStrategy("SizeX", source => ReadIntDynamic(source, static d => d.SizeX)),
                CreateIntReadStrategy("SizeY", source => ReadIntDynamic(source, static d => d.SizeY))
            ];
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
            (bool hasCachedLayout, InventoryLayoutSnapshot cachedSnapshot) = _dependencies.TryGetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight);
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
                if (TryBuildInventoryLayoutEntriesFromCollection(slotItemsCollection, inventoryWidth, inventoryHeight, out List<InventoryLayoutEntry> slotEntries, out int slotRawCount))
                {
                    entries = slotEntries;
                    source = "PlayerInventories[0].InventorySlotItems";
                    rawEntryCount = slotRawCount;
                    isReliable = slotRawCount == 0 || slotEntries.Count > 0;
                    debugDetails = $"raw:{slotRawCount} parsed:{slotEntries.Count}";
                    _dependencies.SetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, new InventoryLayoutSnapshot(entries, source, debugDetails, isReliable, rawEntryCount));
                    return true;
                }
            }

            source = "PlayerInventories[0].InventorySlotItems";
            debugDetails = "read-failed: PlayerInventories[0].InventorySlotItems accessor unavailable or unreadable";
            _dependencies.SetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, new InventoryLayoutSnapshot(entries, source, debugDetails, isReliable, rawEntryCount));
            return false;
        }

        public bool TryBuildInventoryLayoutEntriesFromCollection(
            object collection,
            int inventoryWidth,
            int inventoryHeight,
            out List<InventoryLayoutEntry> entries,
            out int rawEntryCount)
        {
            entries = new List<InventoryLayoutEntry>();
            rawEntryCount = 0;
            if (inventoryWidth <= 0 || inventoryHeight <= 0)
                return false;

            entries.EnsureCapacity(32);
            foreach (object? entry in _dependencies.EnumerateObjects(collection))
            {
                if (entry == null)
                    continue;

                rawEntryCount++;
                Entity? itemEntity = _dependencies.TryGetInventoryItemEntityFromEntry(entry);

                if (!TryResolveInventoryItemPosition(entry, itemEntity, out int x, out int y))
                    continue;

                if (!TryResolveInventoryEntrySize(entry, itemEntity, out int width, out int height))
                    continue;

                int clampedX = Math.Clamp(x, 0, Math.Max(0, inventoryWidth - 1));
                int clampedY = Math.Clamp(y, 0, Math.Max(0, inventoryHeight - 1));
                int clampedWidth = Math.Max(1, Math.Min(width, inventoryWidth - clampedX));
                int clampedHeight = Math.Max(1, Math.Min(height, inventoryHeight - clampedY));

                entries.Add(new InventoryLayoutEntry(clampedX, clampedY, clampedWidth, clampedHeight));
            }

            return true;
        }

        public bool TryResolveInventoryItemPosition(object entry, Entity? itemEntity, out int x, out int y)
        {
            if (TryReadInventoryCoordinates(entry, out x, out y))
                return true;

            if (itemEntity != null && TryReadInventoryCoordinates(itemEntity, out x, out y))
                return true;

            x = 0;
            y = 0;
            return false;
        }

        public bool TryResolveInventoryEntrySize(object entry, Entity? itemEntity, out int width, out int height)
        {
            if (TryReadCoordinatePair(entry, "SizeX", "SizeY", out width, out height)
                && width > 0
                && height > 0)
            {
                width = Math.Max(1, width);
                height = Math.Max(1, height);
                return true;
            }

            if (itemEntity != null)
            {
                (bool success, int resolvedWidth, int resolvedHeight) = _dependencies.TryResolveInventoryItemSize(itemEntity);
                if (success)
                {
                    width = resolvedWidth;
                    height = resolvedHeight;
                    return true;
                }
            }

            width = 1;
            height = 1;
            return false;
        }

        public bool TryReadInventoryCoordinates(object source, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (TryReadCoordinatePair(source, "PosX", "PosY", out x, out y)) return true;
            if (TryReadCoordinatePair(source, "InventoryX", "InventoryY", out x, out y)) return true;
            if (TryReadCoordinatePair(source, "ItemCellX", "ItemCellY", out x, out y)) return true;
            if (TryReadCoordinatePair(source, "CellX", "CellY", out x, out y)) return true;
            if (TryReadCoordinatePair(source, "PositionX", "PositionY", out x, out y)) return true;
            if (TryReadCoordinatePair(source, "X", "Y", out x, out y)) return true;
            if (TryReadCoordinatePair(source, "Column", "Row", out x, out y)) return true;

            if (TryGetDynamicValue(source, static s => s.InventoryPosition, out object? inventoryPosition)
                && inventoryPosition != null
                && TryReadCoordinatePair(inventoryPosition, "X", "Y", out x, out y))
                return true;

            if (TryGetDynamicValue(source, static s => s.InventoryPositionNum, out object? inventoryPositionNum)
                && inventoryPositionNum != null
                && TryReadCoordinatePair(inventoryPositionNum, "X", "Y", out x, out y))
                return true;

            if (TryGetDynamicValue(source, static s => s.Location, out object? location)
                && location != null)
            {
                if (TryGetDynamicValue(location, static s => s.InventoryPosition, out object? locationInventoryPosition)
                    && locationInventoryPosition != null
                    && TryReadCoordinatePair(locationInventoryPosition, "X", "Y", out x, out y))
                    return true;

                if (TryGetDynamicValue(location, static s => s.InventoryPositionNum, out object? locationInventoryPositionNum)
                    && locationInventoryPositionNum != null
                    && TryReadCoordinatePair(locationInventoryPositionNum, "X", "Y", out x, out y))
                    return true;
            }

            return false;
        }

        public bool TryReadCoordinatePair(object source, string xFieldName, string yFieldName, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (!TryReadIntByName(source, xFieldName, out x))
                return false;

            if (!TryReadIntByName(source, yFieldName, out y))
                return false;

            return x >= 0 && y >= 0;
        }

        public bool TryReadIntByName(object source, string memberName, out int value)
        {
            for (int i = 0; i < _intMemberReadStrategies.Length; i++)
            {
                IInventoryIntMemberReadStrategy strategy = _intMemberReadStrategies[i];
                if (!strategy.Name.Equals(memberName, StringComparison.Ordinal))
                    continue;

                return strategy.TryRead(source, out value);
            }

            value = 0;
            return false;
        }

        private static IInventoryIntMemberReadStrategy CreateIntReadStrategy(
            string name,
            Func<object, (bool Success, int Value)> reader)
            => new InventoryIntMemberReadStrategy(name, reader);

        private (bool Success, int Value) ReadIntDynamic(object source, Func<dynamic, object?> accessor)
        {
            return TryReadInt(source, accessor, out int value)
                ? (true, value)
                : (false, 0);
        }

        private bool TryReadInt(object? source, Func<dynamic, object?> accessor, out int value)
        {
            (bool success, int resolvedValue) = _dependencies.TryReadInt(source, accessor);
            value = resolvedValue;
            return success;
        }

        private bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
        {
            (bool success, object? resolvedValue) = _dependencies.TryGetDynamicValue(source, accessor);
            value = resolvedValue;
            return success;
        }
    }
}