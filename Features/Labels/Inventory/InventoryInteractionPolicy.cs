namespace ClickIt.Features.Labels.Inventory
{
    internal sealed class InventoryInteractionPolicy(
        InventoryProbeService probeService,
        InventoryItemEntityService itemEntityService,
        InventoryPickupPolicyEngine pickupPolicy,
        string stoneOfPassageMetadataIdentifier)
    {
        private readonly InventoryProbeService _probeService = probeService;
        private readonly InventoryItemEntityService _itemEntityService = itemEntityService;
        private readonly InventoryPickupPolicyEngine _pickupPolicy = pickupPolicy;
        private readonly string _stoneOfPassageMetadataIdentifier = stoneOfPassageMetadataIdentifier;

        public InventoryDebugSnapshot GetLatestDebug() => _probeService.GetLatestDebug();

        public IReadOnlyList<string> GetLatestDebugTrail() => _probeService.GetLatestDebugTrail();

        public void PublishDebug(InventoryDebugSnapshot snapshot) => _probeService.PublishDebug(snapshot);

        public bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            => _pickupPolicy.ShouldAllowWorldItemWhenInventoryFull(groundItem, gameController);

        public bool ShouldAllowClosedDoorPastMechanic(GameController? gameController)
        {
            bool hasStoneOfPassageInInventory = HasMetadataPathInInventory(gameController);
            if (hasStoneOfPassageInInventory)
                return true;

            _ = _probeService.IsInventoryFull(gameController, out InventoryFullProbe probe);
            return InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(hasStoneOfPassageInInventory, probe.Notes);
        }

        public void ClearForShutdown()
        {
            _probeService.ClearForShutdown();
            _itemEntityService.ClearForShutdown();
        }

        private bool HasMetadataPathInInventory(GameController? gameController)
        {
            if (string.IsNullOrWhiteSpace(_stoneOfPassageMetadataIdentifier))
                return false;

            if (!_itemEntityService.TryEnumerateInventoryItemEntities(gameController, out IReadOnlyList<Entity> inventoryItems))
                return false;

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity inventoryItem = inventoryItems[i];
                string metadataPath = inventoryItem?.Path ?? string.Empty;
                if (metadataPath.Contains(_stoneOfPassageMetadataIdentifier, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}