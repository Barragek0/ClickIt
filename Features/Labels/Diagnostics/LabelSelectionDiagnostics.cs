namespace ClickIt.Features.Labels.Diagnostics
{
    internal sealed class LabelSelectionDiagnostics
    {
        private readonly DebugSnapshotChannel<LabelDebugSnapshot, LabelDebugEvent> _channel;

        public LabelSelectionDiagnostics(int trailCapacity)
        {
            _channel = new DebugSnapshotChannel<LabelDebugSnapshot, LabelDebugEvent>(
                LabelDebugSnapshot.Empty,
                trailCapacity,
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
        }

        public LabelDebugSnapshot GetLatest()
            => _channel.GetLatest();

        public IReadOnlyList<string> GetTrail()
            => _channel.GetTrail();

        public void PublishSnapshot(LabelDebugSnapshot snapshot)
            => _channel.PublishSnapshot(snapshot);

        public void PublishEvent(LabelDebugEvent debugEvent)
            => _channel.PublishEvent(debugEvent);
    }
}