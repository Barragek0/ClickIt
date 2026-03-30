using ClickIt.Services.Observability;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const int LabelDebugTrailCapacity = 24;
        private readonly DebugSnapshotChannel<LabelDebugSnapshot, LabelDebugEvent> _labelDebugChannel = new(
            LabelDebugSnapshot.Empty,
            LabelDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot => $"{snapshot.Sequence:00000} {snapshot.Stage} | {snapshot.Notes}",
            debugEvent => new LabelDebugSnapshot(
                HasData: true,
                Stage: debugEvent.Stage,
                StartIndex: debugEvent.StartIndex,
                EndExclusive: debugEvent.EndExclusive,
                TotalLabels: debugEvent.TotalLabels,
                ConsideredCandidates: debugEvent.ConsideredCandidates,
                NullOrDistanceRejected: debugEvent.NullOrDistanceRejected,
                UntargetableRejected: debugEvent.UntargetableRejected,
                NoMechanicRejected: debugEvent.NoMechanicRejected,
                IgnoredByDistanceCandidates: debugEvent.IgnoredByDistanceCandidates,
                SelectedMechanicId: debugEvent.SelectedMechanicId ?? string.Empty,
                SelectedEntityPath: debugEvent.SelectedEntityPath ?? string.Empty,
                SelectedDistance: debugEvent.SelectedDistance,
                Notes: debugEvent.Notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));

        public LabelDebugSnapshot GetLatestLabelDebug()
        {
            return _labelDebugChannel.GetLatest();
        }

        public IReadOnlyList<string> GetLatestLabelDebugTrail()
        {
            return _labelDebugChannel.GetTrail();
        }

        private void SetLatestLabelDebug(LabelDebugSnapshot snapshot)
        {
            if (!ShouldCaptureLabelDebug())
                return;

            _labelDebugChannel.PublishSnapshot(snapshot);
        }

        private bool ShouldCaptureLabelDebug()
        {
            return _settings.DebugMode.Value && _settings.DebugShowLabels.Value;
        }

        private void PublishLabelDebugStage(in LabelDebugEvent debugEvent)
        {
            if (!ShouldCaptureLabelDebug())
                return;

            _labelDebugChannel.PublishEvent(debugEvent);
        }
    }
}
