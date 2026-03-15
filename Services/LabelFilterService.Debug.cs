namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private readonly object _labelDebugLock = new();
        private LabelDebugSnapshot _lastLabelDebug = LabelDebugSnapshot.Empty;
        private readonly Queue<string> _labelDebugTrail = new();
        private long _labelDebugSequence;
        private const int LabelDebugTrailCapacity = 24;

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
            lock (_labelDebugLock)
            {
                return _lastLabelDebug;
            }
        }

        public IReadOnlyList<string> GetLatestLabelDebugTrail()
        {
            lock (_labelDebugLock)
            {
                return _labelDebugTrail.ToArray();
            }
        }

        private void SetLatestLabelDebug(LabelDebugSnapshot snapshot)
        {
            lock (_labelDebugLock)
            {
                long nextSequence = _labelDebugSequence + 1;
                _labelDebugSequence = nextSequence;

                LabelDebugSnapshot sequenced = snapshot with { Sequence = nextSequence };
                _lastLabelDebug = sequenced;

                string trailEntry = $"{sequenced.Sequence:00000} {sequenced.Stage} | {sequenced.Notes}";
                _labelDebugTrail.Enqueue(trailEntry);
                while (_labelDebugTrail.Count > LabelDebugTrailCapacity)
                {
                    _labelDebugTrail.Dequeue();
                }
            }
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
