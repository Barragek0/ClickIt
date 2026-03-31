namespace ClickIt.Services.Observability
{
    internal sealed record InventoryTelemetrySnapshot(
        LabelFilterService.InventoryDebugSnapshot Inventory,
        IReadOnlyList<string> InventoryTrail)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = Array.Empty<string>();

        public static readonly InventoryTelemetrySnapshot Empty = new(
            Inventory: LabelFilterService.InventoryDebugSnapshot.Empty,
            InventoryTrail: EmptyTrail);
    }
}