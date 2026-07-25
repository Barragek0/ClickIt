namespace ClickIt.Features.Labels.Inventory.Composition
{
    internal sealed class InventoryDomainCompositionContext(
        InventoryDomainFactoryDependencies dependencies,
        int inventoryProbeCacheWindowMs,
        int inventoryDebugTrailCapacity) : IDisposable
    {
        private readonly InventoryDomainFactoryDependencies _dependencies = dependencies;
        private readonly int _inventoryProbeCacheWindowMs = inventoryProbeCacheWindowMs;
        private readonly int _inventoryDebugTrailCapacity = inventoryDebugTrailCapacity;
        private readonly InventoryDynamicAdapter _dynamicAdapter = new(new InventoryDynamicAdapterDependencies(InventoryDynamicAccess.TryGetFirstCollectionObject));
        private InventoryLayoutCache? _layoutCache;
        private InventoryLayoutEntryResolver? _layoutEntryResolver;
        private InventoryItemEntityService? _itemEntityService;
        private InventoryProbeService? _probeService;
        private InventoryReadModelService? _snapshotProvider;
        private InventoryLayoutParser? _layoutParser;

        internal InventoryDomainServices Build()
        {
            _layoutCache = new InventoryLayoutCache(_inventoryProbeCacheWindowMs);
            _layoutEntryResolver = new InventoryLayoutEntryResolver(new InventoryLayoutEntryResolverDependencies(
                InventoryDynamicAccess.EnumerateObjects,
                InventoryDynamicAccess.TryGetInventoryItemEntityFromEntry,
                InventoryDomainFactory.TryResolveInventoryItemSize,
                InventoryDynamicAccess.TryGetDynamicValueResult,
                InventoryDynamicAccess.TryReadIntResult));

            _layoutParser = new InventoryLayoutParser(new InventoryLayoutParserDependencies(
                TryGetPrimaryServerInventorySlotItems,
                _layoutCache,
                _layoutEntryResolver,
                InventoryDynamicAccess.TryReadIntResult));

            _itemEntityService = new InventoryItemEntityService(new InventoryItemEntityServiceDependencies(
                _inventoryProbeCacheWindowMs,
                TryGetPrimaryServerInventory,
                TryGetPrimaryServerInventorySlotItems,
                InventoryDynamicAccess.EnumerateObjects,
                InventoryDynamicAccess.TryGetInventoryItemEntityFromEntry,
                InventoryDynamicAccess.ClassifyInventoryItemEntity));

            _snapshotProvider = new InventoryReadModelService(new InventorySnapshotProviderDependencies(
                TryGetPrimaryServerInventory,
                TryResolveInventoryCapacity,
                TryResolveInventoryDimensions,
                TryResolveInventoryLayoutEntries,
                InventoryDynamicAccess.TryReadInventoryFullFlag,
                InventoryDomainFactory.CreateInventoryFullFlagProbe,
                TryResolveOccupiedInventoryCellsFromLayout,
                InventoryCapacityEngine.IsInventoryCellUsageFull,
                TryEnumeratePrimaryInventoryItemEntitiesFast));

            _probeService = new InventoryProbeService(new InventoryProbeServiceDependencies(
                _inventoryProbeCacheWindowMs,
                _inventoryDebugTrailCapacity,
                TryBuildInventorySnapshot,
                _layoutCache));

            InventoryStackMatchService stackMatchService = new(new InventoryStackMatchDependencies(TryEnumerateInventoryItemEntities));

            InventoryPickupPolicyEngine pickupPolicy = new(new InventoryPickupPolicyDependencies(
                IsInventoryFull,
                InventoryDynamicAccess.TryGetWorldItemEntity,
                _dependencies.GetWorldItemBaseName,
                InventoryStackMatchService.IsGroundItemStackable,
                stackMatchService.HasMatchingPartialStackInInventory,
                HasInventorySpaceForGroundItem,
                InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing,
                InventoryCoreLogic.ShouldPickupWhenInventoryFull,
                InventoryDomainFactory.CreateInventoryDebugSnapshot,
                snapshot => _probeService.PublishDebug(snapshot)));

            InventoryInteractionPolicy interactionPolicy = new(
                _probeService,
                _itemEntityService,
                pickupPolicy,
                _dependencies.StoneOfPassageMetadataIdentifier);

            return new InventoryDomainServices(
                _dynamicAdapter,
                _probeService,
                _snapshotProvider,
                _layoutParser,
                pickupPolicy,
                stackMatchService,
                interactionPolicy);
        }

        private (bool Success, InventorySnapshot Snapshot) TryBuildInventorySnapshot(GameController? gameController)
        {
            bool success = _snapshotProvider!.TryBuild(gameController, out InventorySnapshot snapshot);
            return (success, snapshot);
        }

        private (bool Success, object? PrimaryInventory) TryGetPrimaryServerInventory(GameController? gameController)
        {
            bool success = _dynamicAdapter.TryGetPrimaryServerInventory(gameController, out object? primaryInventory);
            return (success, primaryInventory);
        }

        private (bool Success, object? SlotItemsCollection) TryGetPrimaryServerInventorySlotItems(object primaryInventory)
        {
            bool success = InventoryDynamicAdapter.TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection);
            return (success, slotItemsCollection);
        }

        private (bool Success, int CapacityCells) TryResolveInventoryCapacity(object primaryInventory)
        {
            bool success = _layoutParser!.TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity);
            return (success, totalCellCapacity);
        }

        private (bool Success, int Width, int Height) TryResolveInventoryDimensions(object primaryInventory)
        {
            bool success = _layoutParser!.TryResolveInventoryDimensions(primaryInventory, out int width, out int height);
            return (success, width, height);
        }

        private (bool Success, IReadOnlyList<InventoryLayoutEntry> Entries, string Source, string DebugDetails, bool IsReliable, int RawEntryCount) TryResolveInventoryLayoutEntries(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight)
        {
            bool success = _layoutParser!.TryResolveInventoryLayoutEntries(
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

        private static (bool Success, int OccupiedCellCount) TryResolveOccupiedInventoryCellsFromLayout(
            IReadOnlyList<InventoryLayoutEntry> layoutEntries,
            int inventoryWidth,
            int inventoryHeight)
        {
            bool success = InventoryCapacityEngine.TryResolveOccupiedInventoryCellsFromLayout(layoutEntries, inventoryWidth, inventoryHeight, out int occupiedCellCount);
            return (success, occupiedCellCount);
        }

        private (bool Success, IReadOnlyList<Entity> Entities) TryEnumeratePrimaryInventoryItemEntitiesFast(object primaryInventory)
        {
            bool success = _itemEntityService!.TryEnumeratePrimaryInventoryItemEntitiesFast(primaryInventory, out IReadOnlyList<Entity> entities);
            return (success, entities);
        }

        private (bool InventoryFull, InventoryFullProbe Probe) IsInventoryFull(GameController? gameController)
        {
            bool inventoryFull = _probeService!.IsInventoryFull(gameController, out InventoryFullProbe probe);
            return (inventoryFull, probe);
        }

        private (bool Success, IReadOnlyList<Entity> Items) TryEnumerateInventoryItemEntities(GameController? gameController)
        {
            bool success = _itemEntityService!.TryEnumerateInventoryItemEntities(gameController, out IReadOnlyList<Entity> items);
            return (success, items);
        }

        private bool HasInventorySpaceForGroundItem(Entity? groundItemEntity, GameController? gameController)
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

            if (!_snapshotProvider!.TryBuild(gameController, out InventorySnapshot snapshot))
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

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}