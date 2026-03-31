namespace ClickIt.Services.Label.Inventory.Composition
{
    internal readonly record struct InventoryDomainServices(
        InventoryDynamicAdapter DynamicAdapter,
        InventoryProbeService ProbeService,
        InventoryReadModelService SnapshotProvider,
        InventoryLayoutParser LayoutParser,
        InventoryPickupPolicyEngine PickupPolicy);

    internal static class InventoryCompositionRoot
    {
        public static InventoryDomainServices Compose(
            InventoryDynamicAdapterDependencies dynamicAdapterDependencies,
            InventoryProbeServiceDependencies probeServiceDependencies,
            InventorySnapshotProviderDependencies snapshotProviderDependencies,
            InventoryLayoutParserDependencies layoutParserDependencies,
            InventoryPickupPolicyDependencies pickupPolicyDependencies)
        {
            return new InventoryDomainServices(
                new InventoryDynamicAdapter(dynamicAdapterDependencies),
                new InventoryProbeService(probeServiceDependencies),
                new InventoryReadModelService(snapshotProviderDependencies),
                new InventoryLayoutParser(layoutParserDependencies),
                new InventoryPickupPolicyEngine(pickupPolicyDependencies));
        }
    }
}