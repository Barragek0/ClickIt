using ClickIt.Services.Observability;

namespace ClickIt.Services.Label.Diagnostics
{
    internal sealed class LabelSelectionDiagnostics
    {
        private readonly DebugSnapshotChannel<LabelFilterService.LabelDebugSnapshot, LabelFilterService.LabelDebugEvent> _channel;

        public LabelSelectionDiagnostics(int trailCapacity)
        {
            _channel = new DebugSnapshotChannel<LabelFilterService.LabelDebugSnapshot, LabelFilterService.LabelDebugEvent>(
                LabelFilterService.LabelDebugSnapshot.Empty,
                trailCapacity,
                static (snapshot, sequence) => snapshot with { Sequence = sequence },
                static snapshot => $"{snapshot.Sequence:00000} {snapshot.Stage} | {snapshot.Notes}",
                debugEvent => new LabelFilterService.LabelDebugSnapshot(
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
        }

        public LabelFilterService.LabelDebugSnapshot GetLatest()
            => _channel.GetLatest();

        public IReadOnlyList<string> GetTrail()
            => _channel.GetTrail();

        public void PublishSnapshot(LabelFilterService.LabelDebugSnapshot snapshot)
            => _channel.PublishSnapshot(snapshot);

        public void PublishEvent(LabelFilterService.LabelDebugEvent debugEvent)
            => _channel.PublishEvent(debugEvent);
    }
}