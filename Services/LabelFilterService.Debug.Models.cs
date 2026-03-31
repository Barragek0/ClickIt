namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        public sealed record LabelDebugSnapshot(
            bool HasData,
            string Stage,
            int StartIndex,
            int EndExclusive,
            int TotalLabels,
            int ConsideredCandidates,
            int NullOrDistanceRejected,
            int UntargetableRejected,
            int NoMechanicRejected,
            int IgnoredByDistanceCandidates,
            string SelectedMechanicId,
            string SelectedEntityPath,
            float SelectedDistance,
            string Notes,
            long Sequence,
            long TimestampMs)
        {
            public static readonly LabelDebugSnapshot Empty = new(
                HasData: false,
                Stage: string.Empty,
                StartIndex: 0,
                EndExclusive: 0,
                TotalLabels: 0,
                ConsideredCandidates: 0,
                NullOrDistanceRejected: 0,
                UntargetableRejected: 0,
                NoMechanicRejected: 0,
                IgnoredByDistanceCandidates: 0,
                SelectedMechanicId: string.Empty,
                SelectedEntityPath: string.Empty,
                SelectedDistance: 0f,
                Notes: string.Empty,
                Sequence: 0,
                TimestampMs: 0);
        }

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
}