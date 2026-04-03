using ClickIt.Features.Observability;

namespace ClickIt.Features.Pathfinding.Diagnostics
{
    internal sealed class OffscreenMovementDiagnosticsChannel
    {
        private const int TrailCapacity = 24;

        private readonly DebugSnapshotChannel<OffscreenMovementDebugSnapshot, OffscreenMovementDebugEvent> _channel = new(
            OffscreenMovementDebugSnapshot.Empty,
            TrailCapacity,
            static (snapshot, _) => snapshot,
            static snapshot =>
                $"{snapshot.Stage} Path={snapshot.TargetPath} Built={snapshot.BuiltPath} Resolved={snapshot.ResolvedClickPoint} | {snapshot.MovementSkillDebug}",
            static debugEvent => new OffscreenMovementDebugSnapshot(
                HasData: true,
                Stage: debugEvent.Stage,
                TargetPath: debugEvent.TargetPath,
                BuiltPath: debugEvent.BuiltPath,
                ResolvedFromPath: debugEvent.ResolvedFromPath,
                ResolvedClickPoint: debugEvent.ResolvedClickPoint,
                WindowCenter: debugEvent.WindowCenter,
                TargetScreen: debugEvent.TargetScreen,
                ClickScreen: debugEvent.ClickScreen,
                PlayerGrid: debugEvent.PlayerGrid,
                TargetGrid: debugEvent.TargetGrid,
                MovementSkillDebug: debugEvent.MovementSkillDebug,
                TimestampMs: Environment.TickCount64));

        public IReadOnlyList<string> GetTrail()
            => _channel.GetTrail();

        public OffscreenMovementDebugSnapshot GetLatest()
            => _channel.GetLatest();

        public void PublishEvent(OffscreenMovementDebugEvent debugEvent)
            => _channel.PublishEvent(debugEvent);
    }
}