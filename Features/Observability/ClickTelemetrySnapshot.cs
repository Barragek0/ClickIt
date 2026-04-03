namespace ClickIt.Features.Observability
{
    internal sealed record UltimatumOptionPreviewSnapshot(
        RectangleF Rect,
        string ModifierName,
        int PriorityIndex,
        bool IsSelected);

    internal sealed record ClickTelemetrySnapshot(
        bool ServiceAvailable,
        ClickDebugSnapshot Click,
        IReadOnlyList<string> ClickTrail,
        RuntimeDebugLogSnapshot RuntimeLog,
        IReadOnlyList<string> RuntimeLogTrail,
        UltimatumDebugSnapshot Ultimatum,
        IReadOnlyList<string> UltimatumTrail,
        IReadOnlyList<UltimatumOptionPreviewSnapshot> UltimatumOptionPreview)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = Array.Empty<string>();
        private static readonly IReadOnlyList<UltimatumOptionPreviewSnapshot> EmptyPreview = Array.Empty<UltimatumOptionPreviewSnapshot>();

        public static readonly ClickTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            Click: ClickDebugSnapshot.Empty,
            ClickTrail: EmptyTrail,
            RuntimeLog: RuntimeDebugLogSnapshot.Empty,
            RuntimeLogTrail: EmptyTrail,
            Ultimatum: UltimatumDebugSnapshot.Empty,
            UltimatumTrail: EmptyTrail,
            UltimatumOptionPreview: EmptyPreview);
    }
}