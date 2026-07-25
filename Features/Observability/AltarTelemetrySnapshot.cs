namespace ClickIt.Features.Observability
{
    internal sealed record AltarTelemetrySnapshot(
        bool ServiceAvailable,
        int ComponentCount,
        IReadOnlyList<AltarComponentTelemetrySnapshot> Components,
        AltarServiceDebugTelemetrySnapshot ServiceDebug)
    {
        private static readonly IReadOnlyList<AltarComponentTelemetrySnapshot> EmptyComponents = [];

        public static readonly AltarTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            ComponentCount: 0,
            Components: EmptyComponents,
            ServiceDebug: AltarServiceDebugTelemetrySnapshot.Empty);
    }

    internal sealed record AltarComponentTelemetrySnapshot(
        AltarModSectionTelemetrySnapshot Top,
        AltarModSectionTelemetrySnapshot Bottom);

    internal sealed record AltarModSectionTelemetrySnapshot(
        string SectionName,
        int UpsideCount,
        int DownsideCount,
        IReadOnlyList<AltarWeightedModTelemetrySnapshot> Upsides,
        IReadOnlyList<AltarWeightedModTelemetrySnapshot> Downsides)
    {
        private static readonly IReadOnlyList<AltarWeightedModTelemetrySnapshot> EmptyMods = [];

        public static AltarModSectionTelemetrySnapshot Empty(string sectionName)
            => new(
                SectionName: sectionName,
                UpsideCount: 0,
                DownsideCount: 0,
                Upsides: EmptyMods,
                Downsides: EmptyMods);
    }

    internal sealed record AltarWeightedModTelemetrySnapshot(
        string Text,
        decimal? Weight);

    internal sealed record AltarServiceDebugTelemetrySnapshot(
        int LastScanExarchLabels,
        int LastScanEaterLabels,
        int ElementsFound,
        int ComponentsProcessed,
        int ComponentsAdded,
        int ComponentsDuplicated,
        int ModsMatched,
        int ModsUnmatched,
        string LastProcessedAltarType,
        string LastError,
        DateTime LastScanTime)
    {
        public static readonly AltarServiceDebugTelemetrySnapshot Empty = new(
            LastScanExarchLabels: 0,
            LastScanEaterLabels: 0,
            ElementsFound: 0,
            ComponentsProcessed: 0,
            ComponentsAdded: 0,
            ComponentsDuplicated: 0,
            ModsMatched: 0,
            ModsUnmatched: 0,
            LastProcessedAltarType: string.Empty,
            LastError: string.Empty,
            LastScanTime: DateTime.MinValue);
    }
}