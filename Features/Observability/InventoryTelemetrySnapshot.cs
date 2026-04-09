namespace ClickIt.Features.Observability
{
    internal sealed record InventoryTelemetrySnapshot(
        InventoryDebugSnapshot Inventory,
        IReadOnlyList<string> InventoryTrail)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = [];

        public static readonly InventoryTelemetrySnapshot Empty = new(
            Inventory: InventoryDebugSnapshot.Empty,
            InventoryTrail: EmptyTrail);
    }
}

