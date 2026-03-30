using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Label.Inventory
{
    internal readonly record struct InventoryProbeServiceDependencies(
        int CacheWindowMs,
        int DebugTrailCapacity,
        Func<GameController?, (bool Success, InventorySnapshot Snapshot)> TryBuildInventorySnapshot,
        Func<GameController?, (bool Success, object? PrimaryInventory)> TryGetPrimaryServerInventory,
        Func<object, (bool Success, object? SlotItemsCollection)> TryGetPrimaryServerInventorySlotItems,
        Func<object?, IEnumerable<object?>> EnumerateObjects,
        Func<object, Entity?> TryGetInventoryItemEntityFromEntry,
        Func<Entity?, (bool IsInventoryItem, string Reason)> ClassifyInventoryItemEntity);

    internal sealed class InventoryProbeService
    {
        private readonly InventoryProbeServiceDependencies _dependencies;
        private readonly object _cacheLock = new();
        private readonly ThreadLocal<HashSet<long>> _uniqueEntityAddresses = new(static () => new HashSet<long>());
        private readonly DebugSnapshotStore<LabelFilterService.InventoryDebugSnapshot> _debugStore;

        private long _inventoryProbeCacheTimestampMs;
        private GameController? _inventoryProbeCacheController;
        private InventoryFullProbe _inventoryProbeCacheValue = InventoryFullProbe.Empty;
        private bool _inventoryProbeCacheHasValue;

        private long _inventoryItemsCacheTimestampMs;
        private GameController? _inventoryItemsCacheController;
        private IReadOnlyList<Entity> _inventoryItemsCacheValue = Array.Empty<Entity>();
        private bool _inventoryItemsCacheHasValue;

        private long _inventoryLayoutCacheTimestampMs;
        private object? _inventoryLayoutCachePrimaryInventory;
        private int _inventoryLayoutCacheWidth;
        private int _inventoryLayoutCacheHeight;
        private IReadOnlyList<InventoryLayoutEntry> _inventoryLayoutCacheEntries = Array.Empty<InventoryLayoutEntry>();
        private string _inventoryLayoutCacheSource = string.Empty;
        private string _inventoryLayoutCacheDebugDetails = string.Empty;
        private bool _inventoryLayoutCacheIsReliable;
        private int _inventoryLayoutCacheRawEntryCount;
        private bool _inventoryLayoutCacheHasValue;

        public InventoryProbeService(InventoryProbeServiceDependencies dependencies)
        {
            _dependencies = dependencies;
            _debugStore = new DebugSnapshotStore<LabelFilterService.InventoryDebugSnapshot>(
                LabelFilterService.InventoryDebugSnapshot.Empty,
                dependencies.DebugTrailCapacity,
                static (snapshot, sequence) => snapshot with { Sequence = sequence },
                static snapshot =>
                    $"{snapshot.Sequence:00000} {snapshot.Stage} | f:{snapshot.InventoryFull} a:{snapshot.DecisionAllowPickup} c:{snapshot.CapacityCells} o:{snapshot.OccupiedCells} s:{snapshot.IsGroundStackable} p:{snapshot.HasPartialMatchingStack} n:{snapshot.Notes}");
        }

        public LabelFilterService.InventoryDebugSnapshot GetLatestDebug() => _debugStore.GetLatest();

        public IReadOnlyList<string> GetLatestDebugTrail() => _debugStore.GetTrail();

        public void PublishDebug(LabelFilterService.InventoryDebugSnapshot snapshot) => _debugStore.SetLatest(snapshot);

        public bool IsInventoryFull(GameController? gameController, out InventoryFullProbe probe)
        {
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryProbe(gameController, now, out InventoryFullProbe cachedProbe))
            {
                probe = cachedProbe;
                return probe.IsFull;
            }

            (bool success, InventorySnapshot snapshot) = _dependencies.TryBuildInventorySnapshot(gameController);
            if (!success)
            {
                probe = InventoryFullProbe.Empty with { Notes = "Primary server inventory missing" };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            probe = snapshot.FullProbe;
            SetCachedInventoryProbe(gameController, now, probe);
            return probe.IsFull;
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

        public bool TryGetCachedInventoryLayout(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            out InventoryLayoutSnapshot snapshot)
        {
            lock (_cacheLock)
            {
                if (_inventoryLayoutCacheHasValue
                    && ReferenceEquals(_inventoryLayoutCachePrimaryInventory, primaryInventory)
                    && _inventoryLayoutCacheWidth == inventoryWidth
                    && _inventoryLayoutCacheHeight == inventoryHeight
                    && IsCacheFresh(now, _inventoryLayoutCacheTimestampMs, _dependencies.CacheWindowMs))
                {
                    snapshot = new InventoryLayoutSnapshot(
                        Entries: _inventoryLayoutCacheEntries,
                        Source: _inventoryLayoutCacheSource,
                        DebugDetails: _inventoryLayoutCacheDebugDetails,
                        IsReliable: _inventoryLayoutCacheIsReliable,
                        RawEntryCount: _inventoryLayoutCacheRawEntryCount);
                    return true;
                }
            }

            snapshot = InventoryLayoutSnapshot.Empty;
            return false;
        }

        public void SetCachedInventoryLayout(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            InventoryLayoutSnapshot snapshot)
        {
            lock (_cacheLock)
            {
                _inventoryLayoutCachePrimaryInventory = primaryInventory;
                _inventoryLayoutCacheTimestampMs = now;
                _inventoryLayoutCacheWidth = inventoryWidth;
                _inventoryLayoutCacheHeight = inventoryHeight;
                _inventoryLayoutCacheEntries = snapshot.Entries ?? Array.Empty<InventoryLayoutEntry>();
                _inventoryLayoutCacheSource = snapshot.Source ?? string.Empty;
                _inventoryLayoutCacheDebugDetails = snapshot.DebugDetails ?? string.Empty;
                _inventoryLayoutCacheIsReliable = snapshot.IsReliable;
                _inventoryLayoutCacheRawEntryCount = snapshot.RawEntryCount;
                _inventoryLayoutCacheHasValue = true;
            }
        }

        public void ClearForShutdown()
        {
            lock (_cacheLock)
            {
                _inventoryProbeCacheTimestampMs = 0;
                _inventoryProbeCacheController = null;
                _inventoryProbeCacheValue = InventoryFullProbe.Empty;
                _inventoryProbeCacheHasValue = false;

                _inventoryItemsCacheTimestampMs = 0;
                _inventoryItemsCacheController = null;
                _inventoryItemsCacheValue = Array.Empty<Entity>();
                _inventoryItemsCacheHasValue = false;

                _inventoryLayoutCacheTimestampMs = 0;
                _inventoryLayoutCachePrimaryInventory = null;
                _inventoryLayoutCacheWidth = 0;
                _inventoryLayoutCacheHeight = 0;
                _inventoryLayoutCacheEntries = Array.Empty<InventoryLayoutEntry>();
                _inventoryLayoutCacheSource = string.Empty;
                _inventoryLayoutCacheDebugDetails = string.Empty;
                _inventoryLayoutCacheIsReliable = false;
                _inventoryLayoutCacheRawEntryCount = 0;
                _inventoryLayoutCacheHasValue = false;
            }

            if (_uniqueEntityAddresses.IsValueCreated)
                _uniqueEntityAddresses.Value?.Clear();
        }

        private bool TryGetCachedInventoryProbe(GameController? gameController, long now, out InventoryFullProbe probe)
        {
            lock (_cacheLock)
            {
                if (_inventoryProbeCacheHasValue
                    && ReferenceEquals(_inventoryProbeCacheController, gameController)
                    && IsCacheFresh(now, _inventoryProbeCacheTimestampMs, _dependencies.CacheWindowMs))
                {
                    probe = _inventoryProbeCacheValue;
                    return true;
                }
            }

            probe = InventoryFullProbe.Empty;
            return false;
        }

        private void SetCachedInventoryProbe(GameController? gameController, long now, InventoryFullProbe probe)
        {
            lock (_cacheLock)
            {
                _inventoryProbeCacheController = gameController;
                _inventoryProbeCacheTimestampMs = now;
                _inventoryProbeCacheValue = probe;
                _inventoryProbeCacheHasValue = true;
            }
        }

        private bool TryGetCachedInventoryItems(GameController? gameController, long now, out IReadOnlyList<Entity> items)
        {
            lock (_cacheLock)
            {
                if (_inventoryItemsCacheHasValue
                    && ReferenceEquals(_inventoryItemsCacheController, gameController)
                    && IsCacheFresh(now, _inventoryItemsCacheTimestampMs, _dependencies.CacheWindowMs))
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

        private static bool IsCacheFresh(long now, long cachedAtMs, int windowMs)
        {
            if (cachedAtMs <= 0 || windowMs <= 0)
                return false;

            long age = now - cachedAtMs;
            return age >= 0 && age <= windowMs;
        }
    }
}