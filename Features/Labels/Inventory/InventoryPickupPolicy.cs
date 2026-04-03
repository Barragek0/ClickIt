namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryPickupSnapshot(
        InventoryFullProbe Probe,
        bool InventoryFull,
        Entity? GroundItemEntity,
        string GroundItemPath,
        string GroundItemName,
        bool IsStackable,
        int MatchingPathCount,
        int PartialMatchingStackCount,
        bool HasPartialMatchingStack,
        bool HasSpaceForGroundItem);

    internal readonly record struct InventoryPickupPolicyDependencies(
        Func<GameController?, (bool InventoryFull, InventoryFullProbe Probe)> IsInventoryFull,
        Func<Entity, Entity?> TryGetWorldItemEntity,
        Func<Entity, string> GetWorldItemBaseName,
        Func<Entity?, bool> IsGroundItemStackable,
        Func<string, Entity?, GameController?, (bool HasPartialMatchingStack, int MatchingPathCount, int PartialMatchingStackCount)> HasMatchingPartialStackInInventory,
        Func<Entity?, GameController?, bool> HasInventorySpaceForGroundItem,
        Func<bool, string, bool> ShouldAllowPickupWhenPrimaryInventoryMissing,
        Func<bool, Entity?, bool> ShouldAllowPickupWhenGroundItemEntityMissing,
        Func<bool, string, string, bool> ShouldAllowPickupWhenGroundItemIdentityMissing,
        Func<bool, bool, bool, bool> ShouldPickupWhenInventoryFull,
        Func<string, InventoryFullProbe, string, string, bool, int, int, bool, bool, InventoryDebugSnapshot> CreateInventoryDebugSnapshot,
        Action<InventoryDebugSnapshot> PublishInventoryDebug);

    internal sealed class InventoryPickupSnapshotBuilder(InventoryPickupPolicyDependencies dependencies)
    {
        private readonly InventoryPickupPolicyDependencies _dependencies = dependencies;

        public InventoryPickupSnapshot Build(Entity groundItem, GameController? gameController)
        {
            (bool inventoryFull, InventoryFullProbe probe) = _dependencies.IsInventoryFull(gameController);

            Entity? groundItemEntity = _dependencies.TryGetWorldItemEntity(groundItem);
            string groundItemPath = groundItemEntity?.Path ?? string.Empty;
            string groundItemName = _dependencies.GetWorldItemBaseName(groundItem);
            bool isStackable = _dependencies.IsGroundItemStackable(groundItemEntity);

            int matchingPathCount = 0;
            int partialMatchingStackCount = 0;
            bool hasPartialMatchingStack = isStackable;
            if (isStackable)
            {
                (hasPartialMatchingStack, matchingPathCount, partialMatchingStackCount) = _dependencies.HasMatchingPartialStackInInventory(
                    groundItemPath,
                    groundItemEntity,
                    gameController);
            }

            bool hasSpaceForGroundItem = _dependencies.HasInventorySpaceForGroundItem(groundItemEntity, gameController);

            return new InventoryPickupSnapshot(
                Probe: probe,
                InventoryFull: inventoryFull,
                GroundItemEntity: groundItemEntity,
                GroundItemPath: groundItemPath,
                GroundItemName: groundItemName,
                IsStackable: isStackable,
                MatchingPathCount: matchingPathCount,
                PartialMatchingStackCount: partialMatchingStackCount,
                HasPartialMatchingStack: hasPartialMatchingStack,
                HasSpaceForGroundItem: hasSpaceForGroundItem);
        }
    }

    internal sealed class InventoryPickupPolicyEngine
    {
        private readonly InventoryPickupPolicyDependencies _dependencies;
        private readonly InventoryPickupSnapshotBuilder _snapshotBuilder;

        public InventoryPickupPolicyEngine(InventoryPickupPolicyDependencies dependencies)
        {
            _dependencies = dependencies;
            _snapshotBuilder = new InventoryPickupSnapshotBuilder(dependencies);
        }

        public bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
        {
            InventoryPickupSnapshot snapshot = _snapshotBuilder.Build(groundItem, gameController);

            if (_dependencies.ShouldAllowPickupWhenPrimaryInventoryMissing(snapshot.Probe.HasPrimaryInventory, snapshot.Probe.Notes))
            {
                PublishInventoryDebug(
                    stage: "PrimaryInventoryMissingAllow",
                    snapshot,
                    groundItemPath: string.Empty,
                    isStackable: false,
                    matchingPathCount: 0,
                    partialMatchingStackCount: 0,
                    hasPartialMatchingStack: false,
                    allowPickup: true);

                return true;
            }

            if (_dependencies.ShouldAllowPickupWhenGroundItemEntityMissing(snapshot.InventoryFull, snapshot.GroundItemEntity))
            {
                PublishInventoryDebug(
                    stage: "InventoryNotFullUnknownItemAllow",
                    snapshot,
                    snapshot.GroundItemPath,
                    snapshot.IsStackable,
                    matchingPathCount: 0,
                    partialMatchingStackCount: 0,
                    hasPartialMatchingStack: false,
                    allowPickup: true);

                return true;
            }

            if (_dependencies.ShouldAllowPickupWhenGroundItemIdentityMissing(snapshot.InventoryFull, snapshot.GroundItemPath, snapshot.GroundItemName))
            {
                PublishInventoryDebug(
                    stage: "InventoryNotFullUnknownIdentityAllow",
                    snapshot,
                    snapshot.GroundItemPath,
                    snapshot.IsStackable,
                    matchingPathCount: 0,
                    partialMatchingStackCount: 0,
                    hasPartialMatchingStack: false,
                    allowPickup: true);

                return true;
            }

            if (!snapshot.InventoryFull)
            {
                bool allowPickupWhenNotFull = snapshot.HasSpaceForGroundItem || (snapshot.IsStackable && snapshot.HasPartialMatchingStack);
                string stage = allowPickupWhenNotFull ? "InventoryNotFullAllow" : "InventoryNotFullNoFit";

                PublishInventoryDebug(
                    stage,
                    snapshot,
                    snapshot.GroundItemPath,
                    snapshot.IsStackable,
                    snapshot.MatchingPathCount,
                    snapshot.PartialMatchingStackCount,
                    snapshot.HasPartialMatchingStack,
                    allowPickupWhenNotFull);

                return allowPickupWhenNotFull;
            }

            bool allowPickup = _dependencies.ShouldPickupWhenInventoryFull(
                true,
                snapshot.IsStackable,
                snapshot.HasPartialMatchingStack);

            PublishInventoryDebug(
                stage: "InventoryFullDecision",
                snapshot,
                snapshot.GroundItemPath,
                snapshot.IsStackable,
                snapshot.MatchingPathCount,
                snapshot.PartialMatchingStackCount,
                snapshot.HasPartialMatchingStack,
                allowPickup);

            return allowPickup;
        }

        private void PublishInventoryDebug(
            string stage,
            InventoryPickupSnapshot snapshot,
            string groundItemPath,
            bool isStackable,
            int matchingPathCount,
            int partialMatchingStackCount,
            bool hasPartialMatchingStack,
            bool allowPickup)
        {
            _dependencies.PublishInventoryDebug(_dependencies.CreateInventoryDebugSnapshot(
                stage,
                snapshot.Probe,
                groundItemPath,
                snapshot.GroundItemName,
                isStackable,
                matchingPathCount,
                partialMatchingStackCount,
                hasPartialMatchingStack,
                allowPickup));
        }
    }
}

