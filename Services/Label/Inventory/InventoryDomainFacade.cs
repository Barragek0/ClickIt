namespace ClickIt.Services.Label.Inventory
{
    internal sealed class InventoryDomainFacade(
        InventoryDynamicAdapter dynamicAdapter,
        InventoryProbeService probeService,
        InventoryReadModelService snapshotProvider,
        InventoryLayoutParser layoutParser,
        InventoryPickupPolicyEngine pickupPolicy)
    {
        public InventoryDynamicAdapter DynamicAdapter { get; } = dynamicAdapter;

        public InventoryProbeService ProbeService { get; } = probeService;

        public InventoryReadModelService SnapshotProvider { get; } = snapshotProvider;

        public InventoryLayoutParser LayoutParser { get; } = layoutParser;

        public InventoryPickupPolicyEngine PickupPolicy { get; } = pickupPolicy;

        public LabelFilterService.InventoryDebugSnapshot GetLatestDebug() => ProbeService.GetLatestDebug();

        public IReadOnlyList<string> GetLatestDebugTrail() => ProbeService.GetLatestDebugTrail();

        public void PublishDebug(LabelFilterService.InventoryDebugSnapshot snapshot) => ProbeService.PublishDebug(snapshot);

        public void ClearForShutdown() => ProbeService.ClearForShutdown();
    }
}