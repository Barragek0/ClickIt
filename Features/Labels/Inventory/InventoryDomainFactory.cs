using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryDomainServices(
        InventoryDynamicAdapter DynamicAdapter,
        InventoryProbeService ProbeService,
        InventoryReadModelService SnapshotProvider,
        InventoryLayoutParser LayoutParser,
        InventoryPickupPolicyEngine PickupPolicy,
        InventoryStackMatchService StackMatchService,
        InventoryInteractionPolicy InteractionPolicy);

    internal readonly record struct InventoryDomainFactoryDependencies(
        Func<Entity, string> GetWorldItemBaseName,
        string StoneOfPassageMetadataIdentifier);

    internal static class InventoryDomainFactory
    {
        private const int InventoryProbeCacheWindowMs = 50;
        private const int InventoryDebugTrailCapacity = 32;

        internal static InventoryDomainServices Create(InventoryDomainFactoryDependencies dependencies)
        {
            InventoryDynamicAdapter dynamicAdapter = new(new InventoryDynamicAdapterDependencies(InventoryDynamicAccess.TryGetFirstCollectionObject));
            InventoryProbeService? probeService = null;
            InventoryReadModelService? snapshotProvider = null;
            InventoryLayoutParser? layoutParser = null;

            layoutParser = new InventoryLayoutParser(new InventoryLayoutParserDependencies(
                InventoryDynamicAccess.EnumerateObjects,
                TryGetCachedInventoryLayout,
                SetCachedInventoryLayout,
                TryGetPrimaryServerInventorySlotItems,
                InventoryDynamicAccess.TryGetInventoryItemEntityFromEntry,
                TryResolveInventoryItemSize,
                InventoryDynamicAccess.TryGetDynamicValueResult,
                InventoryDynamicAccess.TryReadIntResult));

            snapshotProvider = new InventoryReadModelService(new InventorySnapshotProviderDependencies(
                TryGetPrimaryServerInventory,
                TryResolveInventoryCapacity,
                TryResolveInventoryDimensions,
                TryResolveInventoryLayoutEntries,
                InventoryDynamicAccess.TryReadInventoryFullFlag,
                CreateInventoryFullFlagProbe,
                TryResolveOccupiedInventoryCellsFromLayout,
                InventoryCapacityEngine.IsInventoryCellUsageFull,
                TryEnumeratePrimaryInventoryItemEntitiesFast));

            probeService = new InventoryProbeService(new InventoryProbeServiceDependencies(
                InventoryProbeCacheWindowMs,
                InventoryDebugTrailCapacity,
                TryBuildInventorySnapshot,
                TryGetPrimaryServerInventory,
                TryGetPrimaryServerInventorySlotItems,
                InventoryDynamicAccess.EnumerateObjects,
                InventoryDynamicAccess.TryGetInventoryItemEntityFromEntry,
                InventoryDynamicAccess.ClassifyInventoryItemEntity));

            InventoryStackMatchService stackMatchService = new(new InventoryStackMatchDependencies(TryEnumerateInventoryItemEntities));

            InventoryPickupPolicyEngine pickupPolicy = new(new InventoryPickupPolicyDependencies(
                IsInventoryFull,
                InventoryDynamicAccess.TryGetWorldItemEntity,
                dependencies.GetWorldItemBaseName,
                InventoryStackMatchService.IsGroundItemStackable,
                stackMatchService.HasMatchingPartialStackInInventory,
                HasInventorySpaceForGroundItem,
                InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing,
                InventoryCoreLogic.ShouldPickupWhenInventoryFull,
                CreateInventoryDebugSnapshot,
                snapshot => probeService.PublishDebug(snapshot)));

            InventoryInteractionPolicy interactionPolicy = new(
                probeService,
                pickupPolicy,
                dependencies.StoneOfPassageMetadataIdentifier);

            return new InventoryDomainServices(
                dynamicAdapter,
                probeService,
                snapshotProvider,
                layoutParser,
                pickupPolicy,
                stackMatchService,
                interactionPolicy);

            (bool Success, InventorySnapshot Snapshot) TryBuildInventorySnapshot(GameController? gameController)
            {
                bool success = snapshotProvider!.TryBuild(gameController, out InventorySnapshot snapshot);
                return (success, snapshot);
            }

            (bool Success, object? PrimaryInventory) TryGetPrimaryServerInventory(GameController? gameController)
            {
                bool success = dynamicAdapter.TryGetPrimaryServerInventory(gameController, out object? primaryInventory);
                return (success, primaryInventory);
            }

            (bool Success, object? SlotItemsCollection) TryGetPrimaryServerInventorySlotItems(object primaryInventory)
            {
                bool success = dynamicAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection);
                return (success, slotItemsCollection);
            }

            (bool Success, int CapacityCells) TryResolveInventoryCapacity(object primaryInventory)
            {
                bool success = layoutParser!.TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity);
                return (success, totalCellCapacity);
            }

            (bool Success, int Width, int Height) TryResolveInventoryDimensions(object primaryInventory)
            {
                bool success = layoutParser!.TryResolveInventoryDimensions(primaryInventory, out int width, out int height);
                return (success, width, height);
            }

            (bool Success, IReadOnlyList<InventoryLayoutEntry> Entries, string Source, string DebugDetails, bool IsReliable, int RawEntryCount) TryResolveInventoryLayoutEntries(
                object primaryInventory,
                int inventoryWidth,
                int inventoryHeight)
            {
                bool success = layoutParser!.TryResolveInventoryLayoutEntries(
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

            (bool Success, int OccupiedCellCount) TryResolveOccupiedInventoryCellsFromLayout(
                IReadOnlyList<InventoryLayoutEntry> layoutEntries,
                int inventoryWidth,
                int inventoryHeight)
            {
                bool success = InventoryCapacityEngine.TryResolveOccupiedInventoryCellsFromLayout(layoutEntries, inventoryWidth, inventoryHeight, out int occupiedCellCount);
                return (success, occupiedCellCount);
            }

            (bool Success, IReadOnlyList<Entity> Entities) TryEnumeratePrimaryInventoryItemEntitiesFast(object primaryInventory)
            {
                bool success = probeService!.TryEnumeratePrimaryInventoryItemEntitiesFast(primaryInventory, out IReadOnlyList<Entity> entities);
                return (success, entities);
            }

            (bool Success, InventoryLayoutSnapshot Snapshot) TryGetCachedInventoryLayout(
                object primaryInventory,
                long now,
                int inventoryWidth,
                int inventoryHeight)
            {
                bool success = probeService!.TryGetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, out InventoryLayoutSnapshot snapshot);
                return (success, snapshot);
            }

            void SetCachedInventoryLayout(
                object primaryInventory,
                long now,
                int inventoryWidth,
                int inventoryHeight,
                InventoryLayoutSnapshot snapshot)
                => probeService!.SetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, snapshot);

            (bool InventoryFull, InventoryFullProbe Probe) IsInventoryFull(GameController? gameController)
            {
                bool inventoryFull = probeService!.IsInventoryFull(gameController, out InventoryFullProbe probe);
                return (inventoryFull, probe);
            }

            (bool Success, IReadOnlyList<Entity> Items) TryEnumerateInventoryItemEntities(GameController? gameController)
            {
                bool success = probeService!.TryEnumerateInventoryItemEntities(gameController, out IReadOnlyList<Entity> items);
                return (success, items);
            }

            bool HasInventorySpaceForGroundItem(Entity? groundItemEntity, GameController? gameController)
            {
                if (groundItemEntity == null)
                    return false;

                if (!InventoryCoreLogic.TryResolveInventoryItemSize(groundItemEntity, out int requiredWidth, out int requiredHeight)
                    && !InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath(groundItemEntity.Path, out requiredWidth, out requiredHeight))
                {
                    return false;
                }

                if (requiredWidth <= 0 || requiredHeight <= 0)
                    return false;

                if (!snapshotProvider!.TryBuild(gameController, out InventorySnapshot snapshot))
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
        }

        private static (bool Success, int Width, int Height) TryResolveInventoryItemSize(Entity itemEntity)
            => InventoryCoreLogic.TryResolveInventoryItemSize(itemEntity, out int width, out int height)
                ? (true, width, height)
                : (false, 0, 0);

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
    }
}