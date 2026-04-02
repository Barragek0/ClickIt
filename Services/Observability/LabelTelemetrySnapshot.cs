using ClickIt.Services.Label.Diagnostics;

namespace ClickIt.Services.Observability
{
    internal sealed record LabelTelemetrySnapshot(
        bool ServiceAvailable,
        LabelDebugSnapshot Label,
        IReadOnlyList<string> LabelTrail)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = Array.Empty<string>();

        public static readonly LabelTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            Label: LabelDebugSnapshot.Empty,
            LabelTrail: EmptyTrail);
    }
}