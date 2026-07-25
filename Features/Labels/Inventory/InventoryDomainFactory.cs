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
            => new InventoryDomainCompositionContext(
                dependencies,
                InventoryProbeCacheWindowMs,
                InventoryDebugTrailCapacity)
                .Build();

        internal static (bool Success, int Width, int Height) TryResolveInventoryItemSize(Entity itemEntity)
            => InventoryCoreLogic.TryResolveInventoryItemSize(itemEntity, out int width, out int height)
                ? (true, width, height)
                : (false, 0, 0);

        internal static InventoryFullProbe CreateInventoryFullFlagProbe(bool full, string source)
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

        internal static InventoryDebugSnapshot CreateInventoryDebugSnapshot(
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