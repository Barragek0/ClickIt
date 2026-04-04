namespace ClickIt.Features.Observability
{
    internal sealed record DebugTelemetrySnapshot(
        ClickTelemetrySnapshot Click,
        LabelTelemetrySnapshot Label,
        PathfindingTelemetrySnapshot Pathfinding,
        RenderingTelemetrySnapshot Rendering,
        StatusTelemetrySnapshot Status,
        ErrorTelemetrySnapshot Errors,
        InventoryTelemetrySnapshot Inventory,
        AltarTelemetrySnapshot Altar,
        HoveredItemMetadataTelemetrySnapshot HoveredItem)
    {
        public static readonly DebugTelemetrySnapshot Empty = new(
            Click: ClickTelemetrySnapshot.Empty,
            Label: LabelTelemetrySnapshot.Empty,
            Pathfinding: PathfindingTelemetrySnapshot.Empty,
            Rendering: RenderingTelemetrySnapshot.Empty,
            Status: StatusTelemetrySnapshot.Empty,
            Errors: ErrorTelemetrySnapshot.Empty,
            Inventory: InventoryTelemetrySnapshot.Empty,
            Altar: AltarTelemetrySnapshot.Empty,
            HoveredItem: HoveredItemMetadataTelemetrySnapshot.Empty);
    }
}