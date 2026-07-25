namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryItemEntityServiceDependencies(
        int CacheWindowMs,
        Func<GameController?, (bool Success, object? PrimaryInventory)> TryGetPrimaryServerInventory,
        Func<object, (bool Success, object? SlotItemsCollection)> TryGetPrimaryServerInventorySlotItems,
        Func<object?, IEnumerable<object?>> EnumerateObjects,
        Func<object, Entity?> TryGetInventoryItemEntityFromEntry,
        Func<Entity?, (bool IsInventoryItem, string Reason)> ClassifyInventoryItemEntity);

    internal sealed class InventoryItemEntityService(InventoryItemEntityServiceDependencies dependencies) : IDisposable
    {
        private readonly InventoryItemEntityServiceDependencies _dependencies = dependencies;
        private readonly ThreadLocal<HashSet<long>> _uniqueEntityAddresses = new(static () => []);
        private readonly TimedValueCache<GameController?, IReadOnlyList<Entity>> _inventoryItemsCache = new(
            dependencies.CacheWindowMs,
            settings: new TimedValueCacheSettings(
                RequireNonNegativeAge: true,
                RequirePositiveCachedTimestamp: true));

        public bool TryEnumerateInventoryItemEntities(GameController? gameController, out IReadOnlyList<Entity> items)
        {
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryItems(gameController, now, out IReadOnlyList<Entity> cachedItems))
            {
                items = cachedItems;
                return items.Count > 0;
            }

            items = [];
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
            items = [];

            (bool hasSlotItems, object? collectionObj) = _dependencies.TryGetPrimaryServerInventorySlotItems(primaryInventory);
            if (!hasSlotItems || collectionObj == null)
                return false;


            HashSet<long> uniqueAddresses = _uniqueEntityAddresses.Value ?? [];
            uniqueAddresses.Clear();

            List<Entity> uniqueEntities = new(32);
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
            _inventoryItemsCache.Invalidate();

            if (_uniqueEntityAddresses.IsValueCreated)
                _uniqueEntityAddresses.Value?.Clear();

        }

        public void Dispose()
        {
            ClearForShutdown();
            _uniqueEntityAddresses.Dispose();
        }

        private bool TryGetCachedInventoryItems(GameController? gameController, long now, out IReadOnlyList<Entity> items)
        {
            return _inventoryItemsCache.TryGetValue(gameController, now, out items);
        }

        private void SetCachedInventoryItems(GameController? gameController, long now, IReadOnlyList<Entity> items)
        {
            _inventoryItemsCache.SetValue(gameController, now, items);
        }
    }
}