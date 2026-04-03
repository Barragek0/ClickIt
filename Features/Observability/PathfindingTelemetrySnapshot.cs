using ClickIt.Features.Pathfinding.Diagnostics;

namespace ClickIt.Features.Observability
{
    internal sealed record PathfindingTelemetrySnapshot(
        bool ServiceAvailable,
        PathfindingDebugSnapshot Pathfinding,
        OffscreenMovementDebugSnapshot OffscreenMovement,
        IReadOnlyList<string> OffscreenMovementTrail)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = Array.Empty<string>();

        public static readonly PathfindingTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            Pathfinding: new PathfindingDebugSnapshot(
                TerrainLoaded: false,
                AreaWidth: 0,
                AreaHeight: 0,
                LastExpandedNodes: 0,
                LastPathLength: 0,
                LastComputeMs: 0,
                LastFailureReason: string.Empty,
                LastTargetPath: string.Empty,
                LastStart: default,
                LastRequestedGoal: default,
                LastResolvedGoal: default,
                LastGoalResolutionUsedFallback: false,
                LastGoalResolutionNote: string.Empty),
            OffscreenMovement: OffscreenMovementDebugSnapshot.Empty,
            OffscreenMovementTrail: EmptyTrail);
    }
}