using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const int InventoryProbeCacheWindowMs = 50;
        private const int InventoryDebugTrailCapacity = 32;
        private const string StoneOfPassageMetadataIdentifier = "Incursion/IncursionKey";
        private const string InventoryLayoutUnreliableNotesPrefix = "Inventory layout unreliable";

        private static readonly object InventoryProbeCacheLock = new();
        private static long _inventoryProbeCacheTimestampMs;
        private static GameController? _inventoryProbeCacheController;
        private static InventoryFullProbe _inventoryProbeCacheValue = InventoryFullProbe.Empty;
        private static bool _inventoryProbeCacheHasValue;

        private static long _inventoryItemsCacheTimestampMs;
        private static GameController? _inventoryItemsCacheController;
        private static IReadOnlyList<Entity> _inventoryItemsCacheValue = Array.Empty<Entity>();
        private static bool _inventoryItemsCacheHasValue;

        [ThreadStatic]
        private static HashSet<long>? _threadInventoryUniqueEntityAddresses;

        private static long _inventoryLayoutCacheTimestampMs;
        private static object? _inventoryLayoutCachePrimaryInventory;
        private static int _inventoryLayoutCacheWidth;
        private static int _inventoryLayoutCacheHeight;
        private static IReadOnlyList<InventoryLayoutEntry> _inventoryLayoutCacheEntries = Array.Empty<InventoryLayoutEntry>();
        private static string _inventoryLayoutCacheSource = string.Empty;
        private static string _inventoryLayoutCacheDebugDetails = string.Empty;
        private static bool _inventoryLayoutCacheIsReliable;
        private static int _inventoryLayoutCacheRawEntryCount;
        private static bool _inventoryLayoutCacheHasValue;

        private static readonly DebugSnapshotStore<InventoryDebugSnapshot> InventoryDebugStore = new(
            InventoryDebugSnapshot.Empty,
            InventoryDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot =>
                $"{snapshot.Sequence:00000} {snapshot.Stage} | f:{snapshot.InventoryFull} a:{snapshot.DecisionAllowPickup} c:{snapshot.CapacityCells} o:{snapshot.OccupiedCells} s:{snapshot.IsGroundStackable} p:{snapshot.HasPartialMatchingStack} n:{snapshot.Notes}");

        public static InventoryDebugSnapshot GetLatestInventoryDebug() => InventoryDebugStore.GetLatest();

        public static IReadOnlyList<string> GetLatestInventoryDebugTrail() => InventoryDebugStore.GetTrail();

        private static void PublishInventoryDebug(InventoryDebugSnapshot snapshot) => InventoryDebugStore.SetLatest(snapshot);

        private static bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            => InventoryPickupPolicy.ShouldAllowWorldItemWhenInventoryFull(groundItem, gameController);

        private static InventoryDebugSnapshot CreateInventoryDebugSnapshot(
            string stage,
            InventoryFullProbe probe,
            string groundItemPath,
            string groundItemName,
            bool isStackable,
            int matchingPathCount,
            int partialMatchingStackCount,
            bool hasPartialMatchingStack,
            bool allowPickup)
        {
            return new InventoryDebugSnapshot(
                HasData: true,
                Stage: stage,
                InventoryFull: probe.IsFull,
                InventoryFullSource: probe.Source,
                HasPrimaryInventory: probe.HasPrimaryInventory,
                UsedFullFlag: probe.UsedFullFlag,
                FullFlagValue: probe.FullFlagValue,
                UsedCellOccupancy: probe.UsedCellOccupancy,
                CapacityCells: probe.CapacityCells,
                OccupiedCells: probe.OccupiedCells,
                InventoryEntityCount: probe.InventoryEntityCount,
                LayoutEntryCount: probe.LayoutEntryCount,
                GroundItemPath: groundItemPath,
                GroundItemName: groundItemName,
                IsGroundStackable: isStackable,
                MatchingPathCount: matchingPathCount,
                PartialMatchingStackCount: partialMatchingStackCount,
                HasPartialMatchingStack: hasPartialMatchingStack,
                DecisionAllowPickup: allowPickup,
                Notes: probe.Notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64);
        }

        internal static bool ShouldPickupWhenInventoryFullCore(bool inventoryFull, bool isStackable, bool hasPartialMatchingStack)
            => !inventoryFull || (isStackable && hasPartialMatchingStack);

        internal static bool IsPartialStackCore(int currentStackSize, int maxStackSize)
            => currentStackSize > 0 && maxStackSize > 0 && currentStackSize < maxStackSize;

        internal static bool IsPartialServerStackCore(bool fullStack, int size)
            => size > 0 && !fullStack;

        internal static bool IsInventoryCellUsageFullCore(int occupiedCellCount, int totalCellCapacity)
            => totalCellCapacity > 0 && occupiedCellCount >= totalCellCapacity;

        internal static bool ShouldAllowPickupWhenPrimaryInventoryMissingCore(bool hasPrimaryInventory, string notes)
            => !hasPrimaryInventory && notes == "Primary server inventory missing";

        internal static bool ShouldAllowPickupWhenGroundItemEntityMissingCore(bool inventoryFull, Entity? groundItemEntity)
            => !inventoryFull && groundItemEntity == null;

        internal static bool ShouldAllowPickupWhenGroundItemIdentityMissingCore(bool inventoryFull, string? groundItemPath, string? groundItemName)
            => !inventoryFull
                && string.IsNullOrWhiteSpace(groundItemPath)
                && string.IsNullOrWhiteSpace(groundItemName);

        internal static bool IsInventoryLayoutUnreliableNotesCore(string? notes)
            => !string.IsNullOrWhiteSpace(notes)
               && notes.StartsWith(InventoryLayoutUnreliableNotesPrefix, StringComparison.Ordinal);

        internal static bool ShouldAllowClosedDoorPastMechanicCore(bool hasStoneOfPassageInInventory, string? inventoryProbeNotes)
            => hasStoneOfPassageInInventory || IsInventoryLayoutUnreliableNotesCore(inventoryProbeNotes);

        private static bool ShouldAllowClosedDoorPastMechanic(GameController? gameController)
        {
            bool hasStoneOfPassageInInventory = HasStoneOfPassageInInventoryCore(gameController);
            if (hasStoneOfPassageInInventory)
                return true;

            _ = IsInventoryFullCore(gameController, out InventoryFullProbe probe);
            return ShouldAllowClosedDoorPastMechanicCore(hasStoneOfPassageInInventory, probe.Notes);
        }

        private static bool HasStoneOfPassageInInventoryCore(GameController? gameController)
        {
            if (!TryEnumerateInventoryItemEntities(gameController, out IReadOnlyList<Entity> inventoryItems))
                return false;

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity inventoryItem = inventoryItems[i];
                string metadataPath = inventoryItem?.Path ?? string.Empty;
                if (metadataPath.Contains(StoneOfPassageMetadataIdentifier, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsInventoryFullCore(GameController? gameController, out InventoryFullProbe probe)
        {
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryProbe(gameController, now, out InventoryFullProbe cachedProbe))
            {
                probe = cachedProbe;
                return probe.IsFull;
            }

            if (!InventorySnapshotProvider.TryBuild(gameController, out InventorySnapshot snapshot))
            {
                probe = InventoryFullProbe.Empty with { Notes = "Primary server inventory missing" };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            probe = snapshot.FullProbe;

            SetCachedInventoryProbe(gameController, now, probe);
            return probe.IsFull;
        }

        private static bool HasInventorySpaceForGroundItemCore(Entity? groundItemEntity, GameController? gameController)
        {
            if (groundItemEntity == null)
                return false;

            if (!TryResolveInventoryItemSize(groundItemEntity, out int requiredWidth, out int requiredHeight))
            {
                if (!TryResolveFallbackInventoryItemSizeFromPathCore(groundItemEntity.Path, out requiredWidth, out requiredHeight))
                    return false;
            }

            if (requiredWidth <= 0 || requiredHeight <= 0)
                return false;

            if (!InventorySnapshotProvider.TryBuild(gameController, out InventorySnapshot snapshot))
                return false;

            if (!snapshot.HasPrimaryInventory || !snapshot.Layout.IsReliable)
                return false;

            return InventoryFitEvaluator.HasSpaceForItemFootprint(
                snapshot.Width,
                snapshot.Height,
                snapshot.Layout.Entries,
                requiredWidth,
                requiredHeight);
        }

        private static bool TryResolveInventoryDimensions(object primaryInventory, out int width, out int height)
            => InventoryLayoutParser.TryResolveInventoryDimensions(primaryInventory, out width, out height);

        private static bool TryResolveInventoryLayoutEntries(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight,
            out IReadOnlyList<InventoryLayoutEntry> entries)
            => TryResolveInventoryLayoutEntries(primaryInventory, inventoryWidth, inventoryHeight, out entries, out _, out _, out _, out _);

        private static bool TryResolveInventoryLayoutEntries(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight,
            out IReadOnlyList<InventoryLayoutEntry> entries,
            out string source)
            => TryResolveInventoryLayoutEntries(primaryInventory, inventoryWidth, inventoryHeight, out entries, out source, out _, out _, out _);

        private static bool TryResolveInventoryLayoutEntries(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight,
            out IReadOnlyList<InventoryLayoutEntry> entries,
            out string source,
            out string debugDetails,
            out bool isReliable,
            out int rawEntryCount)
            => InventoryLayoutParser.TryResolveInventoryLayoutEntries(
                primaryInventory,
                inventoryWidth,
                inventoryHeight,
                out entries,
                out source,
                out debugDetails,
                out isReliable,
                out rawEntryCount);

        private static bool TryBuildInventoryLayoutEntriesFromCollection(
            object collection,
            int inventoryWidth,
            int inventoryHeight,
            out List<InventoryLayoutEntry> entries,
            out int rawEntryCount)
            => InventoryLayoutParser.TryBuildInventoryLayoutEntriesFromCollection(
                collection,
                inventoryWidth,
                inventoryHeight,
                out entries,
                out rawEntryCount);

        private static bool TryResolveOccupiedInventoryCellsFromLayout(
            IReadOnlyList<InventoryLayoutEntry> layoutEntries,
            int inventoryWidth,
            int inventoryHeight,
            out int occupiedCellCount)
            => InventoryFitEvaluator.TryResolveOccupiedInventoryCellsFromLayout(
                layoutEntries,
                inventoryWidth,
                inventoryHeight,
                out occupiedCellCount);

        private static Entity? TryGetInventoryItemEntityFromEntry(object entry)
        {
            if (entry is Entity directEntity)
                return directEntity;

            if (TryGetDynamicValue(entry, s => s.ItemEntity, out object? nestedItemObj)
                && nestedItemObj is Entity nestedItemEntity)
                return nestedItemEntity;

            if (TryGetDynamicValue(entry, s => s.Item, out object? itemObj)
                && itemObj is Entity itemEntity)
                return itemEntity;

            if (TryGetDynamicValue(entry, s => s.Entity, out object? entityObj)
                && entityObj is Entity entityFromSlot)
                return entityFromSlot;

            return null;
        }

        private static bool TryResolveInventoryItemPosition(object entry, Entity? itemEntity, out int x, out int y)
            => InventoryLayoutParser.TryResolveInventoryItemPosition(entry, itemEntity, out x, out y);

        private static bool TryResolveInventoryEntrySize(object entry, Entity? itemEntity, out int width, out int height)
            => InventoryLayoutParser.TryResolveInventoryEntrySize(entry, itemEntity, out width, out height);

        private static bool TryReadInventoryCoordinates(object source, out int x, out int y)
            => InventoryLayoutParser.TryReadInventoryCoordinates(source, out x, out y);

        private static bool TryReadCoordinatePair(object source, string xFieldName, string yFieldName, out int x, out int y)
            => InventoryLayoutParser.TryReadCoordinatePair(source, xFieldName, yFieldName, out x, out y);

        private static bool TryReadIntByName(object source, string memberName, out int value)
            => InventoryLayoutParser.TryReadIntByName(source, memberName, out value);

        internal static bool HasSpaceForItemFootprintCore(
            int inventoryWidth,
            int inventoryHeight,
            IReadOnlyList<InventoryLayoutEntry> occupiedEntries,
            int requiredWidth,
            int requiredHeight)
            => InventoryFitEvaluator.HasSpaceForItemFootprint(
                inventoryWidth,
                inventoryHeight,
                occupiedEntries,
                requiredWidth,
                requiredHeight);

        private static bool CanPlaceAt(Span<byte> occupied, int inventoryWidth, int startX, int startY, int width, int height)
        {
            for (int y = startY; y < startY + height; y++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    if (occupied[(y * inventoryWidth) + x] != 0)
                        return false;
                }
            }

            return true;
        }

        private static bool TryResolveInventoryCapacity(object primaryInventory, out int totalCellCapacity)
            => InventoryLayoutParser.TryResolveInventoryCapacity(primaryInventory, out totalCellCapacity);

        private static bool TryReadInventoryFullFlag(object primaryInventory, out bool full, out string source)
        {
            full = false;
            source = string.Empty;

            if (TryReadBool(primaryInventory, out full, s => s.IsFull))
            {
                source = "IsFull";
                return true;
            }

            if (TryReadBool(primaryInventory, out full, s => s.Full))
            {
                source = "Full";
                return true;
            }

            if (TryReadBool(primaryInventory, out full, s => s.InventoryFull))
            {
                source = "InventoryFull";
                return true;
            }

            return false;
        }

        private static InventoryFullProbe CreateInventoryFullFlagProbe(bool full, string source)
        {
            return new InventoryFullProbe(
                HasPrimaryInventory: true,
                UsedFullFlag: true,
                FullFlagValue: full,
                UsedCellOccupancy: false,
                CapacityCells: 0,
                OccupiedCells: 0,
                InventoryEntityCount: 0,
                LayoutEntryCount: 0,
                IsFull: full,
                Source: source,
                Notes: $"Inventory fullness from server flag {source}");
        }

        private static bool TryResolveInventoryItemSize(Entity itemEntity, out int width, out int height)
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

        internal static bool TryResolveInventoryItemSizeFromBase(object? baseComponent, out int width, out int height)
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

            if (!TryReadInt(baseComponent, out width, s => s.ItemCellsSizeX)
                || !TryReadInt(baseComponent, out height, s => s.ItemCellsSizeY))
            {
                return false;
            }

            width = Math.Max(1, width);
            height = Math.Max(1, height);
            return true;
        }

        internal static bool TryResolveFallbackInventoryItemSizeFromPathCore(string? metadataPath, out int width, out int height)
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

            if (!TryGetDynamicValue(baseComponent, s => s.Info, out object? info) || info == null)
                return false;

            if (!TryReadInt(info, out width, s => s.ItemCellsSizeX)
                || !TryReadInt(info, out height, s => s.ItemCellsSizeY))
            {
                return false;
            }

            return width > 0 && height > 0;
        }

        private static bool IsGroundItemStackableCore(Entity? itemEntity)
            => itemEntity != null && TryResolveServerStackState(itemEntity, out _, out _);

        private static bool HasMatchingPartialStackInInventoryCore(
            string? worldItemPath,
            Entity? groundItemEntity,
            GameController? gameController,
            out int matchingPathCount,
            out int partialMatchingStackCount)
        {
            matchingPathCount = 0;
            partialMatchingStackCount = 0;

            if (string.IsNullOrWhiteSpace(worldItemPath))
                return false;

            if (!TryEnumerateInventoryItemEntities(gameController, out IReadOnlyList<Entity> inventoryItems))
                return false;

            bool requiresIncubatorLevelMatch = IsIncubatorPath(worldItemPath);
            bool hasGroundIncubatorLevel = TryResolveCurrencyItemLevel(groundItemEntity, out int groundIncubatorLevel);

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity inventoryItem = inventoryItems[i];
                if (inventoryItem == null)
                    continue;

                string inventoryPath = inventoryItem.Path ?? string.Empty;
                if (!inventoryPath.Equals(worldItemPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                matchingPathCount++;

                bool hasInventoryIncubatorLevel = TryResolveCurrencyItemLevel(inventoryItem, out int inventoryIncubatorLevel);
                if (!ShouldAllowIncubatorStackMatchCore(
                    requiresIncubatorLevelMatch,
                    hasGroundIncubatorLevel,
                    groundIncubatorLevel,
                    hasInventoryIncubatorLevel,
                    inventoryIncubatorLevel))
                {
                    continue;
                }

                if (TryResolveServerStackState(inventoryItem, out bool fullStack, out int stackSize)
                    && IsPartialServerStackCore(fullStack, stackSize))
                {
                    partialMatchingStackCount++;
                    return true;
                }
            }

            return false;
        }

        private static bool IsIncubatorPath(string? metadataPath)
            => !string.IsNullOrWhiteSpace(metadataPath)
               && metadataPath.IndexOf("Incubation", StringComparison.OrdinalIgnoreCase) >= 0;

        internal static bool ShouldAllowIncubatorStackMatchCore(
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

        private static bool TryResolveCurrencyItemLevel(Entity? itemEntity, out int currencyItemLevel)
        {
            currencyItemLevel = 0;
            if (itemEntity == null)
                return false;

            try
            {
                Base? baseComponent = itemEntity.GetComponent<Base>();
                if (baseComponent == null)
                    return false;

                if (TryReadInt(baseComponent, out currencyItemLevel, s => s.CurrencyItemLevel))
                    return currencyItemLevel > 0;

                currencyItemLevel = baseComponent.CurrencyItemLevel;
                return currencyItemLevel > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveServerStackState(Entity itemEntity, out bool fullStack, out int stackSize)
        {
            fullStack = false;
            stackSize = 0;

            try
            {
                object? stack = itemEntity.GetComponent<Stack>();
                bool hasFullFlag = TryReadStackFullFlag(stack, out fullStack);
                bool hasSize = TryReadStackSize(stack, out stackSize);
                return hasFullFlag && hasSize && stackSize > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadStackFullFlag(object? stack, out bool fullStack)
            => TryReadBool(stack, out fullStack, s => s.FullStack)
               || TryReadBool(stack, out fullStack, s => s.IsFull)
               || TryReadBool(stack, out fullStack, s => s.Full);

        private static bool TryReadStackSize(object? stack, out int stackSize)
            => TryReadInt(stack, out stackSize, s => s.Size)
               || TryReadInt(stack, out stackSize, s => s.Count)
               || TryReadInt(stack, out stackSize, s => s.StackSize)
               || TryReadInt(stack, out stackSize, s => s.Amount);

        private static Entity? TryGetWorldItemEntity(Entity? worldItem)
        {
            if (worldItem == null)
                return null;

            try
            {
                WorldItem? worldItemComp = worldItem.GetComponent<WorldItem>();
                return worldItemComp?.ItemEntity;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryEnumerateInventoryItemEntities(GameController? gameController, out IReadOnlyList<Entity> items)
        {
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryItems(gameController, now, out IReadOnlyList<Entity> cachedItems))
            {
                items = cachedItems;
                return items.Count > 0;
            }

            items = Array.Empty<Entity>();
            if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
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

        private static bool TryEnumeratePrimaryInventoryItemEntitiesFast(object primaryInventory, out IReadOnlyList<Entity> items)
        {
            items = Array.Empty<Entity>();

            if (!TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? collectionObj) || collectionObj == null)
                return false;

            HashSet<long> uniqueAddresses = GetThreadInventoryUniqueEntityAddressSet();
            uniqueAddresses.Clear();

            var uniqueEntities = new List<Entity>(32);

            foreach (object? entry in SharedDynamicAdapter.EnumerateObjects(collectionObj))
            {
                if (entry == null)
                    continue;

                Entity? entity = TryGetInventoryItemEntityFromEntry(entry);
                if (entity == null || !IsInventoryItemEntity(entity, out _))
                    continue;

                AddUniqueInventoryEntity(entity, uniqueAddresses, uniqueEntities);
            }

            if (uniqueEntities.Count == 0)
                return false;

            items = uniqueEntities;
            return true;
        }

        private static HashSet<long> GetThreadInventoryUniqueEntityAddressSet()
        {
            HashSet<long>? addresses = _threadInventoryUniqueEntityAddresses;
            if (addresses != null)
                return addresses;

            addresses = new HashSet<long>();
            _threadInventoryUniqueEntityAddresses = addresses;
            return addresses;
        }

        private static void AddUniqueInventoryEntity(Entity entity, HashSet<long> uniqueAddresses, List<Entity> uniqueEntities)
        {
            if (entity == null)
                return;

            long address = entity.Address;
            if (address == 0 || !uniqueAddresses.Add(address))
                return;

            uniqueEntities.Add(entity);
        }

        private static bool TryGetPrimaryServerInventory(GameController? gameController, out object? primaryInventory)
            => InventoryDynamicAdapter.TryGetPrimaryServerInventory(gameController, out primaryInventory);

        private static bool TryGetPrimaryServerInventorySlotItems(object primaryInventory, out object? slotItemsCollection, out string debugDetails)
        {
            slotItemsCollection = null;
            debugDetails = string.Empty;

            if (!TryGetDynamicValue(primaryInventory, s => s.Inventory, out object? inventoryObj) || inventoryObj == null)
            {
                debugDetails = "read-failed: PlayerInventories[0].Inventory accessor unavailable";
                return false;
            }

            if (!TryGetDynamicValue(inventoryObj, s => s.InventorySlotItems, out slotItemsCollection))
            {
                debugDetails = "read-failed: PlayerInventories[0].Inventory.InventorySlotItems accessor unavailable";
                return false;
            }

            if (slotItemsCollection == null)
            {
                debugDetails = "read-ok: PlayerInventories[0].Inventory.InventorySlotItems is null";
                return false;
            }

            int previewCount = CountPreviewObjects(slotItemsCollection, 8);
            debugDetails = $"read-ok: PlayerInventories[0].Inventory.InventorySlotItems type={slotItemsCollection.GetType().Name} previewCount={previewCount}";
            return true;
        }

        private static bool TryGetPrimaryServerInventorySlotItems(object primaryInventory, out object? slotItemsCollection)
            => InventoryDynamicAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out slotItemsCollection);

        private static bool TryGetFirstCollectionObject(object collection, out object? first)
        {
            first = null;

            if (collection is System.Collections.IList list)
            {
                if (list.Count <= 0)
                    return false;

                first = list[0];
                return first != null;
            }

            foreach (object? entry in SharedDynamicAdapter.EnumerateObjects(collection))
            {
                first = entry;
                return first != null;
            }

            return false;
        }

        private static bool IsInventoryItemEntity(Entity? entity, out string reason)
        {
            reason = string.Empty;
            if (entity == null)
            {
                reason = "entity-null";
                return false;
            }

            string path = entity.Path ?? string.Empty;
            if (path.Length == 0)
            {
                reason = "path-empty";
                return false;
            }

            bool isItem = path.IndexOf("Metadata/Items/", StringComparison.OrdinalIgnoreCase) >= 0;
            reason = isItem ? "path-item" : "path-non-item";
            return isItem;
        }

        private static IEnumerable<object?> EnumerateObjects(object? source)
            => SharedDynamicAdapter.EnumerateObjects(source);

        private static int CountPreviewObjects(object? source, int maxCount)
        {
            if (maxCount <= 0)
                return 0;

            int count = 0;
            foreach (object? _ in SharedDynamicAdapter.EnumerateObjects(source))
            {
                count++;
                if (count >= maxCount)
                    break;
            }

            return count;
        }

        private static bool TryGetCachedInventoryProbe(GameController? gameController, long now, out InventoryFullProbe probe)
        {
            lock (InventoryProbeCacheLock)
            {
                if (_inventoryProbeCacheHasValue
                    && ReferenceEquals(_inventoryProbeCacheController, gameController)
                    && IsCacheFresh(now, _inventoryProbeCacheTimestampMs, InventoryProbeCacheWindowMs))
                {
                    probe = _inventoryProbeCacheValue;
                    return true;
                }
            }

            probe = InventoryFullProbe.Empty;
            return false;
        }

        private static void SetCachedInventoryProbe(GameController? gameController, long now, InventoryFullProbe probe)
        {
            lock (InventoryProbeCacheLock)
            {
                _inventoryProbeCacheController = gameController;
                _inventoryProbeCacheTimestampMs = now;
                _inventoryProbeCacheValue = probe;
                _inventoryProbeCacheHasValue = true;
            }
        }

        private static bool TryGetCachedInventoryItems(GameController? gameController, long now, out IReadOnlyList<Entity> items)
        {
            lock (InventoryProbeCacheLock)
            {
                if (_inventoryItemsCacheHasValue
                    && ReferenceEquals(_inventoryItemsCacheController, gameController)
                    && IsCacheFresh(now, _inventoryItemsCacheTimestampMs, InventoryProbeCacheWindowMs))
                {
                    items = _inventoryItemsCacheValue;
                    return true;
                }
            }

            items = Array.Empty<Entity>();
            return false;
        }

        private static bool TryGetCachedInventoryLayout(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            out InventoryLayoutSnapshot snapshot)
        {
            lock (InventoryProbeCacheLock)
            {
                if (_inventoryLayoutCacheHasValue
                    && ReferenceEquals(_inventoryLayoutCachePrimaryInventory, primaryInventory)
                    && _inventoryLayoutCacheWidth == inventoryWidth
                    && _inventoryLayoutCacheHeight == inventoryHeight
                    && IsCacheFresh(now, _inventoryLayoutCacheTimestampMs, InventoryProbeCacheWindowMs))
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

        private static void SetCachedInventoryLayout(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            InventoryLayoutSnapshot snapshot)
        {
            lock (InventoryProbeCacheLock)
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

        private static void SetCachedInventoryItems(GameController? gameController, long now, IReadOnlyList<Entity> items)
        {
            lock (InventoryProbeCacheLock)
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

        internal static void ClearInventoryProbeCacheForShutdown()
        {
            lock (InventoryProbeCacheLock)
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

                _threadInventoryUniqueEntityAddresses = null;
            }
        }

        private static bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
            => SharedDynamicAdapter.TryReadBool(source, accessor, out value);

        private static bool TryReadInt(object? source, out int value, Func<dynamic, object?> accessor)
            => SharedDynamicAdapter.TryReadInt(source, accessor, out value);

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => SharedDynamicAdapter.TryGetValue(source, accessor, out value);
    }
}