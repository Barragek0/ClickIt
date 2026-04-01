using ClickIt.Utils;
using ClickIt.Services.Label.Inventory;
using ClickIt.Services.Label.Inventory.Composition;
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

        private static readonly InventoryDomainServices InventoryDomainServices = InventoryCompositionRoot.Compose(
            CreateInventoryDynamicAdapterDependencies(),
            CreateInventoryProbeServiceDependencies(),
            CreateInventorySnapshotProviderDependencies(),
            CreateInventoryLayoutParserDependencies(),
            CreateInventoryPickupPolicyDependencies());

        private static readonly InventoryDynamicAdapter InventoryDynamicAccessAdapter = InventoryDomainServices.DynamicAdapter;
        private static readonly InventoryProbeService InventoryProbeServiceInstance = InventoryDomainServices.ProbeService;
        private static readonly IInventorySnapshotProvider InventorySnapshotProviderService = InventoryDomainServices.SnapshotProvider;
        private static readonly InventoryLayoutParser InventoryLayoutParserService = InventoryDomainServices.LayoutParser;
        private static readonly InventoryPickupPolicyEngine InventoryPickupPolicyService = InventoryDomainServices.PickupPolicy;

        internal InventoryDebugSnapshot GetLatestInventoryDebug() => InventoryProbeServiceInstance.GetLatestDebug();

        internal IReadOnlyList<string> GetLatestInventoryDebugTrail() => InventoryProbeServiceInstance.GetLatestDebugTrail();

        private static void PublishInventoryDebug(InventoryDebugSnapshot snapshot) => InventoryProbeServiceInstance.PublishDebug(snapshot);

        private static InventoryDynamicAdapterDependencies CreateInventoryDynamicAdapterDependencies()
            => new(collection =>
            {
                bool success = TryGetFirstCollectionObject(collection, out object? first);
                return (success, first);
            });

        private static InventoryProbeServiceDependencies CreateInventoryProbeServiceDependencies()
            => new(
                InventoryProbeCacheWindowMs,
                InventoryDebugTrailCapacity,
                TryBuildInventorySnapshotForInventoryProbeService,
                TryGetPrimaryServerInventoryForInventoryProbeService,
                TryGetPrimaryServerInventorySlotItemsForInventoryProbeService,
                EnumerateObjects,
                TryGetInventoryItemEntityFromEntry,
                ClassifyInventoryItemEntityForInventoryProbeService);

        private static InventorySnapshotProviderDependencies CreateInventorySnapshotProviderDependencies()
            => new(
                TryGetPrimaryServerInventoryForSnapshotProvider,
                TryResolveInventoryCapacityForSnapshotProvider,
                TryResolveInventoryDimensionsForSnapshotProvider,
                TryResolveInventoryLayoutEntriesForSnapshotProvider,
                TryReadInventoryFullFlagForSnapshotProvider,
                CreateInventoryFullFlagProbe,
                TryResolveOccupiedInventoryCellsFromLayoutForSnapshotProvider,
                IsInventoryCellUsageFullCore,
                TryEnumeratePrimaryInventoryItemEntitiesFastForSnapshotProvider);

        private static InventoryLayoutParserDependencies CreateInventoryLayoutParserDependencies()
            => new(
                EnumerateObjects,
                TryGetCachedInventoryLayoutForLayoutParser,
                SetCachedInventoryLayoutForLayoutParser,
                TryGetPrimaryServerInventorySlotItemsForLayoutParser,
                TryGetInventoryItemEntityFromEntry,
                TryResolveInventoryItemSizeForLayoutParser,
                TryGetDynamicValueForLayoutParser,
                TryReadIntForLayoutParser);

        private static InventoryPickupPolicyDependencies CreateInventoryPickupPolicyDependencies()
            => new(
                IsInventoryFullForPickupPolicy,
                TryGetWorldItemEntity,
                GetWorldItemBaseName,
                IsGroundItemStackableCore,
                HasMatchingPartialStackInInventoryForPickupPolicy,
                HasInventorySpaceForGroundItemCore,
                ShouldAllowPickupWhenPrimaryInventoryMissingCore,
                ShouldAllowPickupWhenGroundItemEntityMissingCore,
                ShouldAllowPickupWhenGroundItemIdentityMissingCore,
                ShouldPickupWhenInventoryFullCore,
                CreateInventoryDebugSnapshot,
                PublishInventoryDebug);

        private static (bool Success, InventorySnapshot Snapshot) TryBuildInventorySnapshotForInventoryProbeService(GameController? gameController)
        {
            bool success = InventorySnapshotProviderService.TryBuild(gameController, out InventorySnapshot snapshot);
            return (success, snapshot);
        }

        private static (bool Success, object? PrimaryInventory) TryGetPrimaryServerInventoryForInventoryProbeService(GameController? gameController)
        {
            bool success = InventoryDynamicAccessAdapter.TryGetPrimaryServerInventory(gameController, out object? primaryInventory);
            return (success, primaryInventory);
        }

        private static (bool Success, object? SlotItemsCollection) TryGetPrimaryServerInventorySlotItemsForInventoryProbeService(object primaryInventory)
        {
            bool success = InventoryDynamicAccessAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection);
            return (success, slotItemsCollection);
        }

        private static (bool IsInventoryItem, string Reason) ClassifyInventoryItemEntityForInventoryProbeService(Entity? entity)
        {
            bool success = IsInventoryItemEntity(entity, out string reason);
            return (success, reason);
        }

        private static (bool Success, object? PrimaryInventory) TryGetPrimaryServerInventoryForSnapshotProvider(GameController? gameController)
        {
            bool success = InventoryDynamicAccessAdapter.TryGetPrimaryServerInventory(gameController, out object? primaryInventory);
            return (success, primaryInventory);
        }

        private static (bool Success, int CapacityCells) TryResolveInventoryCapacityForSnapshotProvider(object primaryInventory)
        {
            bool success = TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity);
            return (success, totalCellCapacity);
        }

        private static (bool Success, int Width, int Height) TryResolveInventoryDimensionsForSnapshotProvider(object primaryInventory)
        {
            bool success = TryResolveInventoryDimensions(primaryInventory, out int width, out int height);
            return (success, width, height);
        }

        private static (bool Success, IReadOnlyList<InventoryLayoutEntry> Entries, string Source, string DebugDetails, bool IsReliable, int RawEntryCount) TryResolveInventoryLayoutEntriesForSnapshotProvider(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight)
        {
            bool success = TryResolveInventoryLayoutEntries(
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
            bool success = TryResolveOccupiedInventoryCellsFromLayout(layoutEntries, inventoryWidth, inventoryHeight, out int occupiedCellCount);
            return (success, occupiedCellCount);
        }

        private static (bool Success, IReadOnlyList<Entity> Entities) TryEnumeratePrimaryInventoryItemEntitiesFastForSnapshotProvider(object primaryInventory)
        {
            bool success = InventoryProbeServiceInstance.TryEnumeratePrimaryInventoryItemEntitiesFast(primaryInventory, out IReadOnlyList<Entity> entities);
            return (success, entities);
        }

        private static (bool Success, InventoryLayoutSnapshot Snapshot) TryGetCachedInventoryLayoutForLayoutParser(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight)
        {
            bool success = InventoryProbeServiceInstance.TryGetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, out InventoryLayoutSnapshot snapshot);
            return (success, snapshot);
        }

        private static void SetCachedInventoryLayoutForLayoutParser(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            InventoryLayoutSnapshot snapshot)
            => InventoryProbeServiceInstance.SetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, snapshot);

        private static (bool Success, object? SlotItemsCollection) TryGetPrimaryServerInventorySlotItemsForLayoutParser(object primaryInventory)
        {
            bool success = TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection);
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

        private static (bool InventoryFull, InventoryFullProbe Probe) IsInventoryFullForPickupPolicy(GameController? gameController)
        {
            bool inventoryFull = InventoryProbeServiceInstance.IsInventoryFull(gameController, out InventoryFullProbe probe);
            return (inventoryFull, probe);
        }

        private static (bool HasPartialMatchingStack, int MatchingPathCount, int PartialMatchingStackCount) HasMatchingPartialStackInInventoryForPickupPolicy(
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

        private static bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            => InventoryPickupPolicyService.ShouldAllowWorldItemWhenInventoryFull(groundItem, gameController);

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
            => InventoryCoreLogic.ShouldPickupWhenInventoryFull(inventoryFull, isStackable, hasPartialMatchingStack);

        internal static bool IsPartialStackCore(int currentStackSize, int maxStackSize)
            => InventoryStackingEngine.IsPartialStack(currentStackSize, maxStackSize);

        internal static bool IsPartialServerStackCore(bool fullStack, int size)
            => InventoryStackingEngine.IsPartialServerStack(fullStack, size);

        internal static bool IsInventoryCellUsageFullCore(int occupiedCellCount, int totalCellCapacity)
            => InventoryCapacityEngine.IsInventoryCellUsageFull(occupiedCellCount, totalCellCapacity);

        internal static bool ShouldAllowPickupWhenPrimaryInventoryMissingCore(bool hasPrimaryInventory, string notes)
            => InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing(hasPrimaryInventory, notes);

        internal static bool ShouldAllowPickupWhenGroundItemEntityMissingCore(bool inventoryFull, Entity? groundItemEntity)
            => InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing(inventoryFull, groundItemEntity);

        internal static bool ShouldAllowPickupWhenGroundItemIdentityMissingCore(bool inventoryFull, string? groundItemPath, string? groundItemName)
            => InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing(inventoryFull, groundItemPath, groundItemName);

        internal static bool IsInventoryLayoutUnreliableNotesCore(string? notes)
            => InventoryCoreLogic.IsInventoryLayoutUnreliableNotes(notes);

        internal static bool ShouldAllowClosedDoorPastMechanicCore(bool hasStoneOfPassageInInventory, string? inventoryProbeNotes)
            => InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(hasStoneOfPassageInInventory, inventoryProbeNotes);

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
            => InventoryProbeServiceInstance.IsInventoryFull(gameController, out probe);

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

            if (!InventorySnapshotProviderService.TryBuild(gameController, out InventorySnapshot snapshot))
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
            => InventoryLayoutParserService.TryResolveInventoryDimensions(primaryInventory, out width, out height);

        private static bool TryResolveInventoryLayoutEntries(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight,
            out IReadOnlyList<InventoryLayoutEntry> entries,
            out string source,
            out string debugDetails,
            out bool isReliable,
            out int rawEntryCount)
            => InventoryLayoutParserService.TryResolveInventoryLayoutEntries(
                primaryInventory,
                inventoryWidth,
                inventoryHeight,
                out entries,
                out source,
                out debugDetails,
                out isReliable,
                out rawEntryCount);

        private static bool TryResolveOccupiedInventoryCellsFromLayout(
            IReadOnlyList<InventoryLayoutEntry> layoutEntries,
            int inventoryWidth,
            int inventoryHeight,
            out int occupiedCellCount)
            => InventoryCapacityEngine.TryResolveOccupiedInventoryCellsFromLayout(
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

        private static bool TryResolveInventoryCapacity(object primaryInventory, out int totalCellCapacity)
            => InventoryLayoutParserService.TryResolveInventoryCapacity(primaryInventory, out totalCellCapacity);

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

        private static bool TryEnumerateInventoryItemEntities(GameController? gameController, out IReadOnlyList<Entity> items)
            => InventoryProbeServiceInstance.TryEnumerateInventoryItemEntities(gameController, out items);

        private static bool TryGetPrimaryServerInventorySlotItems(object primaryInventory, out object? slotItemsCollection)
            => InventoryDynamicAccessAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out slotItemsCollection);

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
            => InventoryProbeServiceInstance.ClearForShutdown();

        private static bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
            => DynamicObjectAdapter.TryReadBool(source, accessor, out value);

        private static bool TryReadInt(object? source, out int value, Func<dynamic, object?> accessor)
            => DynamicObjectAdapter.TryReadInt(source, accessor, out value);

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicObjectAdapter.TryGetValue(source, accessor, out value);
    }
}