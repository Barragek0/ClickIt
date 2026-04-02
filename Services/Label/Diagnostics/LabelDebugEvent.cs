namespace ClickIt.Services.Label.Diagnostics
{
    internal sealed record LabelDebugEvent(
        string Stage,
        int StartIndex,
        int EndExclusive,
        int TotalLabels)
    {
        public int ConsideredCandidates { get; init; }
        public int NullOrDistanceRejected { get; init; }
        public int UntargetableRejected { get; init; }
        public int NoMechanicRejected { get; init; }
        public int IgnoredByDistanceCandidates { get; init; }
        public string? SelectedMechanicId { get; init; }
        public string? SelectedEntityPath { get; init; }
        public float SelectedDistance { get; init; }
        public string Notes { get; init; } = string.Empty;
    }
}