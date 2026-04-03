using ClickIt.Shared;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Features.Labels.Inventory
{
    internal static class InventoryDynamicAccess
    {
        internal static (bool Success, object? First) TryGetFirstCollectionObject(object collection)
        {
            object? first = null;

            if (collection is System.Collections.IList list)
            {
                if (list.Count <= 0)
                    return (false, null);

                first = list[0];
                return (first != null, first);
            }

            foreach (object? entry in DynamicObjectAdapter.EnumerateObjects(collection))
            {
                first = entry;
                return (first != null, first);
            }

            return (false, null);
        }

        internal static Entity? TryGetInventoryItemEntityFromEntry(object entry)
        {
            if (entry is Entity directEntity)
                return directEntity;

            if (TryGetDynamicValue(entry, static s => s.ItemEntity, out object? nestedItemObj)
                && nestedItemObj is Entity nestedItemEntity)
                return nestedItemEntity;

            if (TryGetDynamicValue(entry, static s => s.Item, out object? itemObj)
                && itemObj is Entity itemEntity)
                return itemEntity;

            if (TryGetDynamicValue(entry, static s => s.Entity, out object? entityObj)
                && entityObj is Entity entityFromSlot)
                return entityFromSlot;

            return null;
        }

        internal static (bool IsInventoryItem, string Reason) ClassifyInventoryItemEntity(Entity? entity)
        {
            if (entity == null)
                return (false, "entity-null");

            string path = entity.Path ?? string.Empty;
            if (path.Length == 0)
                return (false, "path-empty");

            bool isItem = path.IndexOf("Metadata/Items/", StringComparison.OrdinalIgnoreCase) >= 0;
            return (isItem, isItem ? "path-item" : "path-non-item");
        }

        internal static (bool Success, bool Full, string Source) TryReadInventoryFullFlag(object primaryInventory)
        {
            if (TryReadBool(primaryInventory, out bool full, static s => s.IsFull))
                return (true, full, "IsFull");

            if (TryReadBool(primaryInventory, out full, static s => s.Full))
                return (true, full, "Full");

            if (TryReadBool(primaryInventory, out full, static s => s.InventoryFull))
                return (true, full, "InventoryFull");

            return (false, false, string.Empty);
        }

        internal static Entity? TryGetWorldItemEntity(Entity? worldItem)
        {
            if (worldItem == null)
                return null;

            try
            {
                WorldItem? worldItemComponent = worldItem.GetComponent<WorldItem>();
                return worldItemComponent?.ItemEntity;
            }
            catch
            {
                return null;
            }
        }

        internal static IEnumerable<object?> EnumerateObjects(object? source)
            => DynamicObjectAdapter.EnumerateObjects(source);

        internal static (bool Success, object? Value) TryGetDynamicValueResult(object? source, Func<dynamic, object?> accessor)
        {
            bool success = DynamicObjectAdapter.TryGetValue(source, accessor, out object? value);
            return (success, value);
        }

        internal static (bool Success, int Value) TryReadIntResult(object? source, Func<dynamic, object?> accessor)
        {
            bool success = DynamicObjectAdapter.TryReadInt(source, accessor, out int value);
            return (success, value);
        }

        private static bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
            => DynamicObjectAdapter.TryReadBool(source, accessor, out value);

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicObjectAdapter.TryGetValue(source, accessor, out value);
    }
}