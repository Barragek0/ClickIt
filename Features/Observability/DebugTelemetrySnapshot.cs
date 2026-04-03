namespace ClickIt.Features.Observability
{
    internal sealed record DebugTelemetrySnapshot(
        ClickTelemetrySnapshot Click,
        LabelTelemetrySnapshot Label,
        PathfindingTelemetrySnapshot Pathfinding,
        InventoryTelemetrySnapshot Inventory)
    {
        public static readonly DebugTelemetrySnapshot Empty = new(
            Click: ClickTelemetrySnapshot.Empty,
            Label: LabelTelemetrySnapshot.Empty,
            Pathfinding: PathfindingTelemetrySnapshot.Empty,
            Inventory: InventoryTelemetrySnapshot.Empty);
    }
}