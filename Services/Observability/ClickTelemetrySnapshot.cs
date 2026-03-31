using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services.Observability
{
    internal sealed record UltimatumOptionPreviewSnapshot(
        RectangleF Rect,
        string ModifierName,
        int PriorityIndex,
        bool IsSelected);

    internal sealed record ClickTelemetrySnapshot(
        bool ServiceAvailable,
        ClickService.ClickDebugSnapshot Click,
        IReadOnlyList<string> ClickTrail,
        ClickService.RuntimeDebugLogSnapshot RuntimeLog,
        IReadOnlyList<string> RuntimeLogTrail,
        ClickService.UltimatumDebugSnapshot Ultimatum,
        IReadOnlyList<string> UltimatumTrail,
        IReadOnlyList<UltimatumOptionPreviewSnapshot> UltimatumOptionPreview)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = Array.Empty<string>();
        private static readonly IReadOnlyList<UltimatumOptionPreviewSnapshot> EmptyPreview = Array.Empty<UltimatumOptionPreviewSnapshot>();

        public static readonly ClickTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            Click: ClickService.ClickDebugSnapshot.Empty,
            ClickTrail: EmptyTrail,
            RuntimeLog: ClickService.RuntimeDebugLogSnapshot.Empty,
            RuntimeLogTrail: EmptyTrail,
            Ultimatum: ClickService.UltimatumDebugSnapshot.Empty,
            UltimatumTrail: EmptyTrail,
            UltimatumOptionPreview: EmptyPreview);
    }
}