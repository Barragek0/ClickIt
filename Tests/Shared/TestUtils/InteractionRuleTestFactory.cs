namespace ClickIt.Tests.Shared.TestUtils
{
    public static class InteractionRuleTestFactory
    {
        internal static InventoryInteractionPolicy CreateInventoryInteractionPolicy(InventorySnapshot snapshot)
        {
            InventoryProbeService probeService = new(new InventoryProbeServiceDependencies(
                CacheWindowMs: 50,
                DebugTrailCapacity: 8,
                TryBuildInventorySnapshot: _ => (true, snapshot),
                LayoutCache: new InventoryLayoutCache(cacheWindowMs: 50)));

            InventoryItemEntityService itemEntityService = new(new InventoryItemEntityServiceDependencies(
                CacheWindowMs: 50,
                TryGetPrimaryServerInventory: _ => (false, null),
                TryGetPrimaryServerInventorySlotItems: _ => (false, null),
                EnumerateObjects: _ => System.Array.Empty<object?>(),
                TryGetInventoryItemEntityFromEntry: _ => null,
                ClassifyInventoryItemEntity: _ => (false, string.Empty)));

            InventoryPickupPolicyEngine pickupPolicy = new(new InventoryPickupPolicyDependencies(
                _ => (false, snapshot.FullProbe with { HasPrimaryInventory = snapshot.HasPrimaryInventory }),
                entity => entity,
                _ => "Test Item",
                _ => false,
                (_, _, _) => (false, 0, 0),
                (_, _) => false,
                InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing,
                InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing,
                (_, _, _) => false,
                (_, _, _, _, _, _, _, _, allowPickup) => new InventoryDebugSnapshot(
                    HasData: true,
                    Stage: "Test",
                    InventoryFull: false,
                    InventoryFullSource: string.Empty,
                    HasPrimaryInventory: snapshot.HasPrimaryInventory,
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
                    DecisionAllowPickup: allowPickup,
                    Notes: snapshot.FullProbe.Notes,
                    Sequence: 0,
                    TimestampMs: 0),
                _ => { }));

            return new InventoryInteractionPolicy(probeService, itemEntityService, pickupPolicy, "Incursion/IncursionKey");
        }

        internal static InventoryInteractionPolicy CreateInventoryInteractionPolicy(bool allowClosedDoorPast)
            => CreateInventoryInteractionPolicy(
                default(InventorySnapshot) with
                {
                    HasPrimaryInventory = true,
                    FullProbe = InventoryFullProbe.Empty with
                    {
                        HasPrimaryInventory = true,
                        Notes = allowClosedDoorPast
                            ? "Inventory layout unreliable from inventory slots (raw:5 parsed:0)"
                            : string.Empty
                    }
                });
    }

    public sealed class LabelProbe : LabelOnGround
    {
        public new object? Label { get; set; }

        public new object? ItemOnGround { get; set; }
    }

    public sealed class StrongboxItemProbe
    {
        public object? ChestComponent { get; set; }

        public MonsterRarity Rarity { get; set; }

        public string RenderName { get; set; } = string.Empty;

        public object? GetComponent<T>()
            => ChestComponent;
    }

    public sealed class ChestProbe
    {
        public bool IsLocked { get; set; }
    }
}
