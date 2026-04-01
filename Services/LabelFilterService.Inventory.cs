using ClickIt.Utils;
using ClickIt.Services.Label.Inventory;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        internal sealed record InventoryDebugSnapshot(
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

        private const int InventoryProbeCacheWindowMs = 50;
        private const int InventoryDebugTrailCapacity = 32;
        private const string StoneOfPassageMetadataIdentifier = "Incursion/IncursionKey";

        private InventoryDomainFacade? _inventoryDomain;

        private InventoryDomainFacade InventoryDomain => _inventoryDomain ??= CreateInventoryDomainFacade();

        internal InventoryDebugSnapshot GetLatestInventoryDebug() => InventoryDomain.GetLatestDebug();

        internal IReadOnlyList<string> GetLatestInventoryDebugTrail() => InventoryDomain.GetLatestDebugTrail();

        private void PublishInventoryDebug(InventoryDebugSnapshot snapshot) => InventoryDomain.PublishDebug(snapshot);

        private InventoryDomainFacade CreateInventoryDomainFacade()
            => new(
                new InventoryDynamicAdapter(CreateInventoryDynamicAdapterDependencies()),
                new InventoryProbeService(CreateInventoryProbeServiceDependencies()),
                new InventoryReadModelService(CreateInventorySnapshotProviderDependencies()),
                new InventoryLayoutParser(CreateInventoryLayoutParserDependencies()),
                new InventoryPickupPolicyEngine(CreateInventoryPickupPolicyDependencies()));

        private static InventoryDynamicAdapterDependencies CreateInventoryDynamicAdapterDependencies()
            => new(collection =>
            {
                bool success = TryGetFirstCollectionObject(collection, out object? first);
                return (success, first);
            });

        private InventoryProbeServiceDependencies CreateInventoryProbeServiceDependencies()
            => new(
                InventoryProbeCacheWindowMs,
                InventoryDebugTrailCapacity,
                TryBuildInventorySnapshotForInventoryProbeService,
                TryGetPrimaryServerInventoryForInventoryProbeService,
                TryGetPrimaryServerInventorySlotItemsForInventoryProbeService,
                EnumerateObjects,
                TryGetInventoryItemEntityFromEntry,
                ClassifyInventoryItemEntityForInventoryProbeService);

        private InventorySnapshotProviderDependencies CreateInventorySnapshotProviderDependencies()
            => new(
                TryGetPrimaryServerInventoryForSnapshotProvider,
                TryResolveInventoryCapacityForSnapshotProvider,
                TryResolveInventoryDimensionsForSnapshotProvider,
                TryResolveInventoryLayoutEntriesForSnapshotProvider,
                TryReadInventoryFullFlagForSnapshotProvider,
                CreateInventoryFullFlagProbe,
                TryResolveOccupiedInventoryCellsFromLayoutForSnapshotProvider,
                InventoryCapacityEngine.IsInventoryCellUsageFull,
                TryEnumeratePrimaryInventoryItemEntitiesFastForSnapshotProvider);

        private InventoryLayoutParserDependencies CreateInventoryLayoutParserDependencies()
            => new(
                EnumerateObjects,
                TryGetCachedInventoryLayoutForLayoutParser,
                SetCachedInventoryLayoutForLayoutParser,
                TryGetPrimaryServerInventorySlotItemsForLayoutParser,
                TryGetInventoryItemEntityFromEntry,
                TryResolveInventoryItemSizeForLayoutParser,
                TryGetDynamicValueForLayoutParser,
                TryReadIntForLayoutParser);

        private InventoryPickupPolicyDependencies CreateInventoryPickupPolicyDependencies()
            => new(
                IsInventoryFullForPickupPolicy,
                TryGetWorldItemEntity,
                GetWorldItemBaseName,
                IsGroundItemStackableCore,
                HasMatchingPartialStackInInventoryForPickupPolicy,
                HasInventorySpaceForGroundItemCore,
                InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing,
                InventoryCoreLogic.ShouldPickupWhenInventoryFull,
                CreateInventoryDebugSnapshot,
                PublishInventoryDebug);

        private (bool Success, InventorySnapshot Snapshot) TryBuildInventorySnapshotForInventoryProbeService(GameController? gameController)
        {
            bool success = InventoryDomain.SnapshotProvider.TryBuild(gameController, out InventorySnapshot snapshot);
            return (success, snapshot);
        }

        private (bool Success, object? PrimaryInventory) TryGetPrimaryServerInventoryForInventoryProbeService(GameController? gameController)
        {
            bool success = InventoryDomain.DynamicAdapter.TryGetPrimaryServerInventory(gameController, out object? primaryInventory);
            return (success, primaryInventory);
        }

        private (bool Success, object? SlotItemsCollection) TryGetPrimaryServerInventorySlotItemsForInventoryProbeService(object primaryInventory)
        {
            bool success = InventoryDomain.DynamicAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection);
            return (success, slotItemsCollection);
        }

        private static (bool IsInventoryItem, string Reason) ClassifyInventoryItemEntityForInventoryProbeService(Entity? entity)
        {
            bool success = IsInventoryItemEntity(entity, out string reason);
            return (success, reason);
        }

        private (bool Success, object? PrimaryInventory) TryGetPrimaryServerInventoryForSnapshotProvider(GameController? gameController)
        {
            bool success = InventoryDomain.DynamicAdapter.TryGetPrimaryServerInventory(gameController, out object? primaryInventory);
            return (success, primaryInventory);
        }

        private (bool Success, int CapacityCells) TryResolveInventoryCapacityForSnapshotProvider(object primaryInventory)
        {
            bool success = InventoryDomain.LayoutParser.TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity);
            return (success, totalCellCapacity);
        }

        private (bool Success, int Width, int Height) TryResolveInventoryDimensionsForSnapshotProvider(object primaryInventory)
        {
            bool success = InventoryDomain.LayoutParser.TryResolveInventoryDimensions(primaryInventory, out int width, out int height);
            return (success, width, height);
        }

        private (bool Success, IReadOnlyList<InventoryLayoutEntry> Entries, string Source, string DebugDetails, bool IsReliable, int RawEntryCount) TryResolveInventoryLayoutEntriesForSnapshotProvider(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight)
        {
            bool success = InventoryDomain.LayoutParser.TryResolveInventoryLayoutEntries(
                primaryInventory,
                inventoryWidth,
                inventoryHeight,
                out IReadOnlyList<InventoryLayoutEntry> entries,
                out string source,
                out string debugDetails,
                out bool isReliable,
                out int rawEntryCount);
            return (success, entries, source, debugDetails, isReliable, rawEntryCount);
        }

        private static (bool Success, bool Full, string Source) TryReadInventoryFullFlagForSnapshotProvider(object primaryInventory)
        {
            bool success = TryReadInventoryFullFlag(primaryInventory, out bool full, out string source);
            return (success, full, source);
        }

        private static (bool Success, int OccupiedCellCount) TryResolveOccupiedInventoryCellsFromLayoutForSnapshotProvider(
            IReadOnlyList<InventoryLayoutEntry> layoutEntries,
            int inventoryWidth,
            int inventoryHeight)
        {
            bool success = InventoryCapacityEngine.TryResolveOccupiedInventoryCellsFromLayout(layoutEntries, inventoryWidth, inventoryHeight, out int occupiedCellCount);
            return (success, occupiedCellCount);
        }

        private (bool Success, IReadOnlyList<Entity> Entities) TryEnumeratePrimaryInventoryItemEntitiesFastForSnapshotProvider(object primaryInventory)
        {
            bool success = InventoryDomain.ProbeService.TryEnumeratePrimaryInventoryItemEntitiesFast(primaryInventory, out IReadOnlyList<Entity> entities);
            return (success, entities);
        }

        private (bool Success, InventoryLayoutSnapshot Snapshot) TryGetCachedInventoryLayoutForLayoutParser(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight)
        {
            bool success = InventoryDomain.ProbeService.TryGetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, out InventoryLayoutSnapshot snapshot);
            return (success, snapshot);
        }

        private void SetCachedInventoryLayoutForLayoutParser(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            InventoryLayoutSnapshot snapshot)
            => InventoryDomain.ProbeService.SetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, snapshot);

        private (bool Success, object? SlotItemsCollection) TryGetPrimaryServerInventorySlotItemsForLayoutParser(object primaryInventory)
        {
            bool success = InventoryDomain.DynamicAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection);
            return (success, slotItemsCollection);
        }

        private static (bool Success, int Width, int Height) TryResolveInventoryItemSizeForLayoutParser(Entity itemEntity)
        {
            bool success = TryResolveInventoryItemSize(itemEntity, out int width, out int height);
            return (success, width, height);
        }

        private static (bool Success, object? Value) TryGetDynamicValueForLayoutParser(object? source, Func<dynamic, object?> accessor)
        {
            bool success = TryGetDynamicValue(source, accessor, out object? value);
            return (success, value);
        }

        private static (bool Success, int Value) TryReadIntForLayoutParser(object? source, Func<dynamic, object?> accessor)
        {
            bool success = TryReadInt(source, out int value, accessor);
            return (success, value);
        }

        private (bool InventoryFull, InventoryFullProbe Probe) IsInventoryFullForPickupPolicy(GameController? gameController)
        {
            bool inventoryFull = InventoryDomain.ProbeService.IsInventoryFull(gameController, out InventoryFullProbe probe);
            return (inventoryFull, probe);
        }

        private (bool HasPartialMatchingStack, int MatchingPathCount, int PartialMatchingStackCount) HasMatchingPartialStackInInventoryForPickupPolicy(
            string groundItemPath,
            Entity? groundItemEntity,
            GameController? gameController)
        {
            bool hasPartialMatchingStack = HasMatchingPartialStackInInventoryCore(
                groundItemPath,
                groundItemEntity,
                gameController,
                out int matchingPathCount,
                out int partialMatchingStackCount);
            return (hasPartialMatchingStack, matchingPathCount, partialMatchingStackCount);
        }

        private bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            => InventoryDomain.PickupPolicy.ShouldAllowWorldItemWhenInventoryFull(groundItem, gameController);

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

        private bool ShouldAllowClosedDoorPastMechanic(GameController? gameController)
        {
            bool hasStoneOfPassageInInventory = HasStoneOfPassageInInventoryCore(gameController);
            if (hasStoneOfPassageInInventory)
                return true;

            _ = IsInventoryFullCore(gameController, out InventoryFullProbe probe);
            return InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(hasStoneOfPassageInInventory, probe.Notes);
        }

        private bool HasStoneOfPassageInInventoryCore(GameController? gameController)
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

        private bool IsInventoryFullCore(GameController? gameController, out InventoryFullProbe probe)
            => InventoryDomain.ProbeService.IsInventoryFull(gameController, out probe);

        private bool HasInventorySpaceForGroundItemCore(Entity? groundItemEntity, GameController? gameController)
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

            if (!InventoryDomain.SnapshotProvider.TryBuild(gameController, out InventorySnapshot snapshot))
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

        internal static bool HasSpaceForItemFootprintCore(
            int inventoryWidth,
            int inventoryHeight,
            IReadOnlyList<InventoryLayoutEntry> occupiedEntries,
            int requiredWidth,
            int requiredHeight)
            => InventoryCapacityEngine.HasSpaceForItemFootprint(
                inventoryWidth,
                inventoryHeight,
                occupiedEntries,
                requiredWidth,
                requiredHeight);

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
            => InventoryCoreLogic.TryResolveInventoryItemSize(itemEntity, out width, out height);

        internal static bool TryResolveInventoryItemSizeFromBase(object? baseComponent, out int width, out int height)
            => InventoryCoreLogic.TryResolveInventoryItemSizeFromBase(baseComponent, out width, out height);

        internal static bool TryResolveFallbackInventoryItemSizeFromPathCore(string? metadataPath, out int width, out int height)
            => InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath(metadataPath, out width, out height);

        private static bool IsGroundItemStackableCore(Entity? itemEntity)
            => itemEntity != null && TryResolveServerStackState(itemEntity, out _, out _);

        private bool HasMatchingPartialStackInInventoryCore(
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
                    && InventoryStackingEngine.IsPartialServerStack(fullStack, stackSize))
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
            => InventoryStackingEngine.ShouldAllowIncubatorStackMatch(
                requiresIncubatorLevelMatch,
                hasGroundIncubatorLevel,
                groundIncubatorLevel,
                hasInventoryIncubatorLevel,
                inventoryIncubatorLevel);

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

        private bool TryEnumerateInventoryItemEntities(GameController? gameController, out IReadOnlyList<Entity> items)
            => InventoryDomain.ProbeService.TryEnumerateInventoryItemEntities(gameController, out items);

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

            foreach (object? entry in DynamicObjectAdapter.EnumerateObjects(collection))
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
            => DynamicObjectAdapter.EnumerateObjects(source);

        internal void ClearInventoryProbeCacheForShutdown()
            => InventoryDomain.ClearForShutdown();

        private static bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
            => DynamicObjectAdapter.TryReadBool(source, accessor, out value);

        private static bool TryReadInt(object? source, out int value, Func<dynamic, object?> accessor)
            => DynamicObjectAdapter.TryReadInt(source, accessor, out value);

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicObjectAdapter.TryGetValue(source, accessor, out value);
    }
}