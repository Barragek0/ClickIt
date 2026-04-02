using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Label.Inventory
{
    internal sealed class InventoryDomainFacade(
        InventoryDynamicAdapter dynamicAdapter,
        InventoryProbeService probeService,
        InventoryReadModelService snapshotProvider,
        InventoryLayoutParser layoutParser,
        InventoryPickupPolicyEngine pickupPolicy,
        InventoryStackMatchService stackMatchService)
    {
        public InventoryDynamicAdapter DynamicAdapter { get; } = dynamicAdapter;

        public InventoryProbeService ProbeService { get; } = probeService;

        public InventoryReadModelService SnapshotProvider { get; } = snapshotProvider;

        public InventoryLayoutParser LayoutParser { get; } = layoutParser;

        public InventoryPickupPolicyEngine PickupPolicy { get; } = pickupPolicy;

        public InventoryStackMatchService StackMatchService { get; } = stackMatchService;

        public InventoryDebugSnapshot GetLatestDebug() => ProbeService.GetLatestDebug();

        public IReadOnlyList<string> GetLatestDebugTrail() => ProbeService.GetLatestDebugTrail();

        public void PublishDebug(InventoryDebugSnapshot snapshot) => ProbeService.PublishDebug(snapshot);

        public bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            => PickupPolicy.ShouldAllowWorldItemWhenInventoryFull(groundItem, gameController);

        public bool ShouldAllowClosedDoorPastMechanic(GameController? gameController, string stoneOfPassageMetadataIdentifier)
        {
            bool hasStoneOfPassageInInventory = HasMetadataPathInInventory(gameController, stoneOfPassageMetadataIdentifier);
            if (hasStoneOfPassageInInventory)
                return true;

            _ = ProbeService.IsInventoryFull(gameController, out InventoryFullProbe probe);
            return InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(hasStoneOfPassageInInventory, probe.Notes);
        }

        public void ClearForShutdown() => ProbeService.ClearForShutdown();

        private bool HasMetadataPathInInventory(GameController? gameController, string metadataIdentifier)
        {
            if (string.IsNullOrWhiteSpace(metadataIdentifier))
                return false;

            if (!ProbeService.TryEnumerateInventoryItemEntities(gameController, out IReadOnlyList<Entity> inventoryItems))
                return false;

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity inventoryItem = inventoryItems[i];
                string metadataPath = inventoryItem?.Path ?? string.Empty;
                if (metadataPath.Contains(metadataIdentifier, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}

