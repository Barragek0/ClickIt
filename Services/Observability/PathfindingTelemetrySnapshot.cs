namespace ClickIt.Services.Observability
{
    internal sealed record PathfindingTelemetrySnapshot(
        bool ServiceAvailable,
        PathfindingService.PathfindingDebugSnapshot Pathfinding,
        PathfindingService.OffscreenMovementDebugSnapshot OffscreenMovement,
        IReadOnlyList<string> OffscreenMovementTrail)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = Array.Empty<string>();

        public static readonly PathfindingTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            Pathfinding: new PathfindingService.PathfindingDebugSnapshot(
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
            OffscreenMovement: PathfindingService.OffscreenMovementDebugSnapshot.Empty,
            OffscreenMovementTrail: EmptyTrail);
    }
}