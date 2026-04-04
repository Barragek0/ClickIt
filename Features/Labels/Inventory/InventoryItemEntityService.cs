namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryItemEntityServiceDependencies(
        int CacheWindowMs,
        Func<GameController?, (bool Success, object? PrimaryInventory)> TryGetPrimaryServerInventory,
        Func<object, (bool Success, object? SlotItemsCollection)> TryGetPrimaryServerInventorySlotItems,
        Func<object?, IEnumerable<object?>> EnumerateObjects,
        Func<object, Entity?> TryGetInventoryItemEntityFromEntry,
        Func<Entity?, (bool IsInventoryItem, string Reason)> ClassifyInventoryItemEntity);

    internal sealed class InventoryItemEntityService
    {
        private readonly InventoryItemEntityServiceDependencies _dependencies;
        private readonly object _cacheLock = new();
        private readonly ThreadLocal<HashSet<long>> _uniqueEntityAddresses = new(static () => new HashSet<long>());

        private long _inventoryItemsCacheTimestampMs;
        private GameController? _inventoryItemsCacheController;
        private IReadOnlyList<Entity> _inventoryItemsCacheValue = Array.Empty<Entity>();
        private bool _inventoryItemsCacheHasValue;

        public InventoryItemEntityService(InventoryItemEntityServiceDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public bool TryEnumerateInventoryItemEntities(GameController? gameController, out IReadOnlyList<Entity> items)
        {
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryItems(gameController, now, out IReadOnlyList<Entity> cachedItems))
            {
                items = cachedItems;
                return items.Count > 0;
            }

            items = Array.Empty<Entity>();
            (bool hasPrimaryInventory, object? primaryInventory) = _dependencies.TryGetPrimaryServerInventory(gameController);
            if (!hasPrimaryInventory || primaryInventory == null)
            {
                SetCachedInventoryItems(gameController, now, items);
                return false;
            }

            if (!TryEnumeratePrimaryInventoryItemEntitiesFast(primaryInventory, out IReadOnlyList<Entity> entities))
            {
                SetCachedInventoryItems(gameController, now, items);
                return false;
            }

            items = entities;
            SetCachedInventoryItems(gameController, now, items);
            return items.Count > 0;
        }

        public bool TryEnumeratePrimaryInventoryItemEntitiesFast(object primaryInventory, out IReadOnlyList<Entity> items)
        {
            items = Array.Empty<Entity>();

            (bool hasSlotItems, object? collectionObj) = _dependencies.TryGetPrimaryServerInventorySlotItems(primaryInventory);
            if (!hasSlotItems || collectionObj == null)
                return false;

            HashSet<long> uniqueAddresses = _uniqueEntityAddresses.Value ?? new HashSet<long>();
            uniqueAddresses.Clear();

            var uniqueEntities = new List<Entity>(32);
            foreach (object? entry in _dependencies.EnumerateObjects(collectionObj))
            {
                if (entry == null)
                    continue;

                Entity? entity = _dependencies.TryGetInventoryItemEntityFromEntry(entry);
                if (entity == null || !_dependencies.ClassifyInventoryItemEntity(entity).IsInventoryItem)
                    continue;

                long address = entity.Address;
                if (address == 0 || !uniqueAddresses.Add(address))
                    continue;

                uniqueEntities.Add(entity);
            }

            if (uniqueEntities.Count == 0)
                return false;

            items = uniqueEntities;
            return true;
        }

        public void ClearForShutdown()
        {
            lock (_cacheLock)
            {
                _inventoryItemsCacheTimestampMs = 0;
                _inventoryItemsCacheController = null;
                _inventoryItemsCacheValue = Array.Empty<Entity>();
                _inventoryItemsCacheHasValue = false;
            }

            if (_uniqueEntityAddresses.IsValueCreated)
                _uniqueEntityAddresses.Value?.Clear();
        }

        private bool TryGetCachedInventoryItems(GameController? gameController, long now, out IReadOnlyList<Entity> items)
        {
            lock (_cacheLock)
            {
                if (_inventoryItemsCacheHasValue
                    && ReferenceEquals(_inventoryItemsCacheController, gameController)
                    && InventoryCacheWindowPolicy.IsFresh(now, _inventoryItemsCacheTimestampMs, _dependencies.CacheWindowMs))
                {
                    items = _inventoryItemsCacheValue;
                    return true;
                }
            }

            items = Array.Empty<Entity>();
            return false;
        }

        private void SetCachedInventoryItems(GameController? gameController, long now, IReadOnlyList<Entity> items)
        {
            lock (_cacheLock)
            {
                _inventoryItemsCacheController = gameController;
                _inventoryItemsCacheTimestampMs = now;
                _inventoryItemsCacheValue = items;
                _inventoryItemsCacheHasValue = true;
            }
        }
    }
}