using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const int InventoryProbeCacheWindowMs = 50;
        private const int InventoryDebugTrailCapacity = 32;
        private static readonly object InventoryProbeCacheLock = new();
        private static long _inventoryProbeCacheTimestampMs;
        private static GameController? _inventoryProbeCacheController;
        private static InventoryFullProbe _inventoryProbeCacheValue = InventoryFullProbe.Empty;
        private static bool _inventoryProbeCacheHasValue;
        private static long _inventoryItemsCacheTimestampMs;
        private static GameController? _inventoryItemsCacheController;
        private static IReadOnlyList<Entity> _inventoryItemsCacheValue = [];
        private static bool _inventoryItemsCacheHasValue;
        private static readonly DebugSnapshotStore<InventoryDebugSnapshot> InventoryDebugStore = new(
            InventoryDebugSnapshot.Empty,
            InventoryDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot =>
                $"{snapshot.Sequence:00000} {snapshot.Stage} | f:{snapshot.InventoryFull} a:{snapshot.DecisionAllowPickup} c:{snapshot.CapacityCells} o:{snapshot.OccupiedCells} s:{snapshot.IsGroundStackable} p:{snapshot.HasPartialMatchingStack} n:{snapshot.Notes}");

        public sealed record InventoryDebugSnapshot(
            bool HasData,
            string Stage,
            bool InventoryFull,
            string InventoryFullSource,
            bool HasPrimaryInventory,
            bool UsedFullFlag,
            bool FullFlagValue,
            bool UsedCellOccupancy,
            int CapacityCells,
            int OccupiedCells,
            int InventoryEntityCount,
            int LayoutEntryCount,
            string GroundItemPath,
            string GroundItemName,
            bool IsGroundStackable,
            int MatchingPathCount,
            int PartialMatchingStackCount,
            bool HasPartialMatchingStack,
            bool DecisionAllowPickup,
            string Notes,
            long Sequence,
            long TimestampMs)
        {
            public static readonly InventoryDebugSnapshot Empty = new(
                HasData: false,
                Stage: string.Empty,
                InventoryFull: false,
                InventoryFullSource: string.Empty,
                HasPrimaryInventory: false,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: false,
                CapacityCells: 0,
                OccupiedCells: 0,
                InventoryEntityCount: 0,
                LayoutEntryCount: 0,
                GroundItemPath: string.Empty,
                GroundItemName: string.Empty,
                IsGroundStackable: false,
                MatchingPathCount: 0,
                PartialMatchingStackCount: 0,
                HasPartialMatchingStack: false,
                DecisionAllowPickup: false,
                Notes: string.Empty,
                Sequence: 0,
                TimestampMs: 0);
        }

        private readonly record struct InventoryFullProbe(
            bool HasPrimaryInventory,
            bool UsedFullFlag,
            bool FullFlagValue,
            bool UsedCellOccupancy,
            int CapacityCells,
            int OccupiedCells,
            int InventoryEntityCount,
            int LayoutEntryCount,
            bool IsFull,
            string Source,
            string Notes)
        {
            public static readonly InventoryFullProbe Empty = new(
                HasPrimaryInventory: false,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: false,
                CapacityCells: 0,
                OccupiedCells: 0,
                InventoryEntityCount: 0,
                LayoutEntryCount: 0,
                IsFull: false,
                Source: string.Empty,
                Notes: string.Empty);
        }

        public static InventoryDebugSnapshot GetLatestInventoryDebug() => InventoryDebugStore.GetLatest();

        public static IReadOnlyList<string> GetLatestInventoryDebugTrail() => InventoryDebugStore.GetTrail();

        private static void PublishInventoryDebug(InventoryDebugSnapshot snapshot) => InventoryDebugStore.SetLatest(snapshot);

        private static bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController)
        {
            string metadata = GetWorldItemMetadataPath(item);
            string itemName = GetWorldItemBaseName(item);

            IReadOnlyList<string> whitelist = settings.ItemTypeWhitelistMetadata ?? [];
            IReadOnlyList<string> blacklist = settings.ItemTypeBlacklistMetadata ?? [];

            bool whitelistPass = whitelist.Count == 0 || ContainsAnyMetadataIdentifier(metadata, itemName, item, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && ContainsAnyMetadataIdentifier(metadata, itemName, item, blacklist);
            if (blacklistMatch)
                return false;

            return ShouldAllowWorldItemWhenInventoryFull(item, gameController);
        }

        private static bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
        {
            bool inventoryFull = IsInventoryFullCore(gameController, out InventoryFullProbe probe);
            if (!inventoryFull)
            {
                PublishInventoryDebug(CreateInventoryDebugSnapshot(
                    stage: "InventoryNotFullAllow",
                    probe,
                    groundItemPath: string.Empty,
                    groundItemName: string.Empty,
                    isStackable: false,
                    matchingPathCount: 0,
                    partialMatchingStackCount: 0,
                    hasPartialMatchingStack: false,
                    allowPickup: true));

                return true;
            }

            Entity? groundItemEntity = TryGetWorldItemEntity(groundItem);
            string groundItemPath = groundItemEntity?.Path ?? string.Empty;
            string groundItemName = GetWorldItemBaseName(groundItem);
            bool isStackable = IsGroundItemStackableCore(groundItemEntity);
            int matchingPathCount = 0;
            int partialMatchingStackCount = 0;

            bool hasPartialMatchingStack = isStackable
                && HasMatchingPartialStackInInventoryCore(
                    groundItemPath,
                    groundItemEntity,
                    gameController,
                    out matchingPathCount,
                    out partialMatchingStackCount);

            bool allowPickup = ShouldPickupWhenInventoryFullCore(
                inventoryFull: true,
                isStackable,
                hasPartialMatchingStack);

            PublishInventoryDebug(CreateInventoryDebugSnapshot(
                stage: "InventoryFullDecision",
                probe,
                groundItemPath,
                groundItemName,
                isStackable,
                matchingPathCount,
                partialMatchingStackCount,
                hasPartialMatchingStack,
                allowPickup));

            return allowPickup;
        }

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

        private static bool IsInventoryFullCore(GameController? gameController)
            => IsInventoryFullCore(gameController, out _);

        private static bool IsInventoryFullCore(GameController? gameController, out InventoryFullProbe probe)
        {
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryProbe(gameController, now, out InventoryFullProbe cachedProbe))
            {
                probe = cachedProbe;
                return probe.IsFull;
            }

            if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
            {
                probe = InventoryFullProbe.Empty with { Notes = "Primary server inventory missing" };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (TryReadInventoryFullFlag(primaryInventory, out bool full, out string source))
            {
                probe = CreateInventoryFullFlagProbe(full, source);
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (!TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity))
            {
                probe = InventoryFullProbe.Empty with { HasPrimaryInventory = true, Notes = "Unable to resolve inventory capacity" };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (!TryEnumeratePrimaryInventoryItemEntities(primaryInventory, out IReadOnlyList<Entity> inventoryItems, out string itemEnumDebug))
            {
                probe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    Notes = $"Unable to enumerate PlayerInventories[0].Inventory.Items ({itemEnumDebug})"
                };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (!TryResolveOccupiedInventoryCells(inventoryItems, totalCellCapacity, out int occupiedCellCount))
            {
                probe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    InventoryEntityCount = inventoryItems.Count,
                    LayoutEntryCount = inventoryItems.Count,
                    Notes = "Unable to resolve occupied inventory cells from PlayerInventories[0].Inventory.Items"
                };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            bool isFull = IsInventoryCellUsageFullCore(occupiedCellCount, totalCellCapacity);
            probe = new InventoryFullProbe(
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: totalCellCapacity,
                OccupiedCells: occupiedCellCount,
                InventoryEntityCount: inventoryItems.Count,
                LayoutEntryCount: inventoryItems.Count,
                IsFull: isFull,
                Source: "CellOccupancy",
                Notes: $"Inventory fullness from PlayerInventories[0].Inventory.Items footprint ({itemEnumDebug})");
            SetCachedInventoryProbe(gameController, now, probe);
            return probe.IsFull;
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

        private static bool TryResolveOccupiedInventoryCells(IReadOnlyList<Entity> inventoryItems, int totalCellCapacity, out int occupiedCellCount)
        {
            occupiedCellCount = 0;
            if (totalCellCapacity <= 0)
                return false;

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity itemEntity = inventoryItems[i];
                if (itemEntity == null)
                    continue;

                TryResolveInventoryItemSize(itemEntity, out int width, out int height);
                occupiedCellCount += Math.Max(1, width) * Math.Max(1, height);
                if (occupiedCellCount >= totalCellCapacity)
                {
                    occupiedCellCount = totalCellCapacity;
                    return true;
                }
            }

            occupiedCellCount = Math.Min(totalCellCapacity, occupiedCellCount);
            return true;
        }

        private static bool TryResolveInventoryCapacity(object primaryInventory, out int totalCellCapacity)
        {
            totalCellCapacity = 0;

            if (TryReadInt(primaryInventory, out int width, s => s.Width)
                && TryReadInt(primaryInventory, out int height, s => s.Height)
                && width > 0
                && height > 0)
            {
                totalCellCapacity = width * height;
                return true;
            }

            if (TryReadInt(primaryInventory, out int totalBoxes, s => s.TotalBoxes) && totalBoxes > 0)
            {
                totalCellCapacity = totalBoxes;
                return true;
            }

            if (TryReadInt(primaryInventory, out int capacity, s => s.Capacity) && capacity > 0)
            {
                totalCellCapacity = capacity;
                return true;
            }

            totalCellCapacity = 60;
            return true;
        }

        private static bool TryResolveInventoryItemSize(Entity itemEntity, out int width, out int height)
        {
            width = 1;
            height = 1;

            try
            {
                Base? baseComponent = itemEntity.GetComponent<Base>();
                bool widthResolved = TryReadInt(baseComponent, out width, s => s.Width);
                bool heightResolved = TryReadInt(baseComponent, out height, s => s.Height);
                if (widthResolved && heightResolved)
                {
                    width = Math.Max(1, width);
                    height = Math.Max(1, height);
                    return true;
                }
            }
            catch
            {
            }

            return false;
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

            items = [];
            if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
            {
                SetCachedInventoryItems(gameController, now, items);
                return false;
            }

            if (!TryEnumeratePrimaryInventoryItemEntities(primaryInventory, out IReadOnlyList<Entity> entities, out _))
            {
                SetCachedInventoryItems(gameController, now, items);
                return false;
            }

            items = entities;
            SetCachedInventoryItems(gameController, now, items);
            return items.Count > 0;
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

            items = [];
            return false;
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
                _inventoryItemsCacheValue = [];
                _inventoryItemsCacheHasValue = false;
            }
        }

        private static bool TryEnumeratePrimaryInventoryItemEntities(object primaryInventory, out IReadOnlyList<Entity> items, out string debugDetails)
        {
            items = [];
            debugDetails = string.Empty;

            if (!TryGetPrimaryServerInventoryItems(primaryInventory, out object? collectionObj, out string collectionDebug) || collectionObj == null)
            {
                debugDetails = $"items-collection: {collectionDebug}";
                return false;
            }

            int totalEntries = 0;
            int nullEntries = 0;
            int directEntityEntries = 0;
            int nestedEntityEntries = 0;
            int rejectedNonItemEntity = 0;
            int rejectedNestedNonEntity = 0;

            var uniqueAddresses = new HashSet<long>();
            var uniqueEntities = new List<Entity>(64);
            int dedupBeforeCount = 0;

            foreach (object? entry in EnumerateObjects(collectionObj))
            {
                totalEntries++;

                if (entry == null)
                {
                    nullEntries++;
                    continue;
                }

                if (entry is Entity directEntity)
                {
                    directEntityEntries++;
                    if (IsInventoryItemEntity(directEntity, out _))
                    {
                        dedupBeforeCount++;
                        AddUniqueInventoryEntity(directEntity, uniqueAddresses, uniqueEntities);
                    }
                    else
                        rejectedNonItemEntity++;
                    continue;
                }

                if (TryGetDynamicValue(entry, s => s.ItemEntity, out object? nestedItemObj)
                    && nestedItemObj is Entity nestedItemEntity
                    && IsInventoryItemEntity(nestedItemEntity, out _))
                {
                    nestedEntityEntries++;
                    dedupBeforeCount++;
                    AddUniqueInventoryEntity(nestedItemEntity, uniqueAddresses, uniqueEntities);
                    continue;
                }

                rejectedNestedNonEntity++;
            }

            if (uniqueEntities.Count == 0)
            {
                debugDetails = BuildInventoryEnumerationDebugDetails(
                    collectionDebug,
                    totalEntries,
                    nullEntries,
                    directEntityEntries,
                    nestedEntityEntries,
                    rejectedNonItemEntity,
                    rejectedNestedNonEntity);
                return false;
            }

            items = uniqueEntities;
            debugDetails = BuildInventoryEnumerationDebugDetails(
                collectionDebug,
                totalEntries,
                nullEntries,
                directEntityEntries,
                nestedEntityEntries,
                rejectedNonItemEntity,
                rejectedNestedNonEntity,
                dedupBeforeCount: dedupBeforeCount,
                dedupAfterCount: items.Count);
            return items.Count > 0;
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

        private static string BuildInventoryEnumerationDebugDetails(
            string collectionDebug,
            int totalEntries,
            int nullEntries,
            int directEntityEntries,
            int nestedEntityEntries,
            int rejectedNonItemEntity,
            int rejectedNestedNonEntity,
            int? dedupBeforeCount = null,
            int? dedupAfterCount = null)
        {
            string details = $"{collectionDebug}; entries:{totalEntries} null:{nullEntries} direct:{directEntityEntries} nested:{nestedEntityEntries} rejectedNonItem:{rejectedNonItemEntity} rejectedNested:{rejectedNestedNonEntity}";
            if (!dedupBeforeCount.HasValue || !dedupAfterCount.HasValue)
                return details;

            return $"{details} dedup:{dedupBeforeCount.Value}->{dedupAfterCount.Value}";
        }

        private static bool TryGetPrimaryServerInventory(GameController? gameController, out object? primaryInventory)
        {
            primaryInventory = null;

            object? data = gameController?.IngameState?.Data;
            if (data == null)
                return false;

            if (!TryGetDynamicValue(data, s => s.ServerData, out object? serverData) || serverData == null)
                return false;
            if (!TryGetDynamicValue(serverData, s => s.PlayerInventories, out object? playerInventories) || playerInventories == null)
                return false;
            if (!TryGetFirstCollectionObject(playerInventories, out object? firstInventory) || firstInventory == null)
                return false;

            primaryInventory = firstInventory;
            return true;
        }

        private static bool TryGetPrimaryServerInventoryItems(object primaryInventory, out object? itemsCollection, out string debugDetails)
        {
            itemsCollection = null;
            debugDetails = string.Empty;

            if (!TryGetDynamicValue(primaryInventory, s => s.Inventory, out object? inventoryObj) || inventoryObj == null)
            {
                debugDetails = "read-failed: PlayerInventories[0].Inventory accessor unavailable";
                return false;
            }

            if (!TryGetDynamicValue(inventoryObj, s => s.Items, out itemsCollection))
            {
                debugDetails = "read-failed: PlayerInventories[0].Inventory.Items accessor unavailable";
                return false;
            }

            if (itemsCollection == null)
            {
                debugDetails = "read-ok: PlayerInventories[0].Inventory.Items is null";
                return false;
            }

            int previewCount = CountPreviewObjects(itemsCollection, 8);

            debugDetails = $"read-ok: PlayerInventories[0].Inventory.Items type={itemsCollection.GetType().Name} previewCount={previewCount}";
            return true;
        }

        private static bool TryGetPrimaryServerInventoryItems(object primaryInventory, out object? itemsCollection)
            => TryGetPrimaryServerInventoryItems(primaryInventory, out itemsCollection, out _);

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

            foreach (object? entry in EnumerateObjects(collection))
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
            reason = isItem ? "path-item" : $"path-non-item:{(path.Length <= 56 ? path : path.Substring(0, 56) + "...")}";
            return isItem;
        }

        private static IEnumerable<object?> EnumerateObjects(object? source)
        {
            if (source == null)
                yield break;

            if (source is string)
            {
                yield return source;
                yield break;
            }

            if (source is System.Collections.IEnumerable enumerable)
            {
                foreach (object? entry in enumerable)
                    yield return entry;
                yield break;
            }

            yield return source;
        }

        private static int CountPreviewObjects(object? source, int maxCount)
        {
            if (maxCount <= 0)
                return 0;

            int count = 0;
            foreach (object? _ in EnumerateObjects(source))
            {
                count++;
                if (count >= maxCount)
                    break;
            }

            return count;
        }

        private static bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
            => DynamicAccess.TryReadBool(source, accessor, out value);

        private static bool TryReadInt(object? source, out int value, Func<dynamic, object?> accessor)
            => DynamicAccess.TryReadInt(source, accessor, out value);

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicAccess.TryGetDynamicValue(source, accessor, out value);
    }
}