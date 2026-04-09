namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryLayoutEntryResolverDependencies(
        Func<object?, IEnumerable<object?>> EnumerateObjects,
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

    internal sealed class InventoryLayoutEntryResolver
    {
        private readonly InventoryLayoutEntryResolverDependencies _dependencies;
        private readonly IInventoryIntMemberReadStrategy[] _intMemberReadStrategies;

        public InventoryLayoutEntryResolver(InventoryLayoutEntryResolverDependencies dependencies)
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

                int clampedX = SystemMath.Clamp(x, 0, SystemMath.Max(0, inventoryWidth - 1));
                int clampedY = SystemMath.Clamp(y, 0, SystemMath.Max(0, inventoryHeight - 1));
                int clampedWidth = SystemMath.Max(1, SystemMath.Min(width, inventoryWidth - clampedX));
                int clampedHeight = SystemMath.Max(1, SystemMath.Min(height, inventoryHeight - clampedY));

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
                width = SystemMath.Max(1, width);
                height = SystemMath.Max(1, height);
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