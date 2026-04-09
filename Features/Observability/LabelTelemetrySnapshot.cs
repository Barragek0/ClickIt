namespace ClickIt.Features.Observability
{
    internal sealed record LabelTelemetrySnapshot(
        bool ServiceAvailable,
        LabelDebugSnapshot Label,
        IReadOnlyList<string> LabelTrail,
        bool LabelsAvailable,
        int TotalVisibleLabels,
        int ValidVisibleLabels)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = [];

        public static readonly LabelTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            Label: LabelDebugSnapshot.Empty,
            LabelTrail: EmptyTrail,
            LabelsAvailable: false,
            TotalVisibleLabels: 0,
            ValidVisibleLabels: 0);
    }
}