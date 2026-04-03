namespace ClickIt.Features.Pathfinding.Diagnostics
{
    public sealed record PathfindingDebugSnapshot(
        bool TerrainLoaded,
        int AreaWidth,
        int AreaHeight,
        int LastExpandedNodes,
        int LastPathLength,
        long LastComputeMs,
        string LastFailureReason,
        string LastTargetPath,
        PathfindingService.GridPoint LastStart,
        PathfindingService.GridPoint LastRequestedGoal,
        PathfindingService.GridPoint LastResolvedGoal,
        bool LastGoalResolutionUsedFallback,
        string LastGoalResolutionNote);
}