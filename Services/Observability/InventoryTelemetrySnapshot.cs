using ClickIt.Services.Label.Inventory;

namespace ClickIt.Services.Observability
{
    internal sealed record InventoryTelemetrySnapshot(
        InventoryDebugSnapshot Inventory,
        IReadOnlyList<string> InventoryTrail)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = Array.Empty<string>();

        public static readonly InventoryTelemetrySnapshot Empty = new(
            Inventory: InventoryDebugSnapshot.Empty,
            InventoryTrail: EmptyTrail);
    }
}

