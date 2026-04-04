namespace ClickIt.Features.Observability
{
    internal sealed record HoveredItemMetadataTelemetrySnapshot(
        bool LabelsAvailable,
        bool CursorInsideWindow,
        bool HasHoveredItem,
        string GroundItemName,
        string EntityPath,
        string MetadataPath)
    {
        public static readonly HoveredItemMetadataTelemetrySnapshot Empty = new(
            LabelsAvailable: false,
            CursorInsideWindow: false,
            HasHoveredItem: false,
            GroundItemName: string.Empty,
            EntityPath: string.Empty,
            MetadataPath: string.Empty);
    }
}