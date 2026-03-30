using ClickIt.Utils;
using ExileCore;

namespace ClickIt.Services.Label.Inventory
{
    internal readonly record struct InventoryDynamicAdapterDependencies(
        Func<object, (bool Success, object? First)> TryGetFirstCollectionObject);

    internal sealed class InventoryDynamicAdapter(InventoryDynamicAdapterDependencies dependencies)
    {
        private readonly InventoryDynamicAdapterDependencies _dependencies = dependencies;

        public bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
            => DynamicAccess.TryReadBool(source, accessor, out value);

        public bool TryReadInt(object? source, out int value, Func<dynamic, object?> accessor)
            => DynamicAccess.TryReadInt(source, accessor, out value);

        public bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicAccess.TryGetDynamicValue(source, accessor, out value);

        public bool TryGetPrimaryServerInventory(GameController? gameController, out object? primaryInventory)
        {
            primaryInventory = null;

            object? data = gameController?.IngameState?.Data;
            if (data == null)
                return false;

            if (!TryGetDynamicValue(data, static s => s.ServerData, out object? serverData) || serverData == null)
                return false;

            if (!TryGetDynamicValue(serverData, static s => s.PlayerInventories, out object? playerInventories) || playerInventories == null)
                return false;

            (bool foundFirstInventory, object? firstInventory) = _dependencies.TryGetFirstCollectionObject(playerInventories);
            if (!foundFirstInventory || firstInventory == null)
                return false;

            primaryInventory = firstInventory;
            return true;
        }

        public bool TryGetPrimaryServerInventorySlotItems(object primaryInventory, out object? slotItemsCollection)
        {
            slotItemsCollection = null;

            if (!TryGetDynamicValue(primaryInventory, static s => s.Inventory, out object? inventoryObj) || inventoryObj == null)
                return false;

            if (!TryGetDynamicValue(inventoryObj, static s => s.InventorySlotItems, out slotItemsCollection))
                return false;

            return slotItemsCollection != null;
        }
    }
}