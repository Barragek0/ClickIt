namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const int LabelDebugTrailCapacity = 24;
        private readonly Utils.DebugSnapshotStore<LabelDebugSnapshot> _labelDebugStore = new(
            LabelDebugSnapshot.Empty,
            LabelDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot => $"{snapshot.Sequence:00000} {snapshot.Stage} | {snapshot.Notes}");

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

        public LabelDebugSnapshot GetLatestLabelDebug()
        {
            return _labelDebugStore.GetLatest();
        }

        public IReadOnlyList<string> GetLatestLabelDebugTrail()
        {
            return _labelDebugStore.GetTrail();
        }

        private void SetLatestLabelDebug(LabelDebugSnapshot snapshot)
        {
            if (!ShouldCaptureLabelDebug())
                return;

            _labelDebugStore.SetLatest(snapshot);
        }

        private bool ShouldCaptureLabelDebug()
        {
            return _settings.DebugMode.Value && _settings.DebugShowLabels.Value;
        }

        private void PublishLabelDebugStage(
            string stage,
            int startIndex,
            int endExclusive,
            int totalLabels,
            int consideredCandidates,
            int nullOrDistanceRejected,
            int untargetableRejected,
            int noMechanicRejected,
            int ignoredByDistanceCandidates,
            string? selectedMechanicId,
            string? selectedEntityPath,
            float selectedDistance,
            string notes)
        {
            if (!ShouldCaptureLabelDebug())
                return;

            SetLatestLabelDebug(new LabelDebugSnapshot(
                HasData: true,
                Stage: stage,
                StartIndex: startIndex,
                EndExclusive: endExclusive,
                TotalLabels: totalLabels,
                ConsideredCandidates: consideredCandidates,
                NullOrDistanceRejected: nullOrDistanceRejected,
                UntargetableRejected: untargetableRejected,
                NoMechanicRejected: noMechanicRejected,
                IgnoredByDistanceCandidates: ignoredByDistanceCandidates,
                SelectedMechanicId: selectedMechanicId ?? string.Empty,
                SelectedEntityPath: selectedEntityPath ?? string.Empty,
                SelectedDistance: selectedDistance,
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }
    }
}
