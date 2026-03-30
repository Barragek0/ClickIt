using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using ClickIt.Services.Observability;

namespace ClickIt.Services
{
    public sealed partial class PathfindingService(ClickItSettings settings, Utils.ErrorHandler? errorHandler = null)
    {
        private const int OffscreenMovementDebugTrailCapacity = 24;
        private readonly ClickItSettings _settings = settings;
        private readonly Utils.ErrorHandler? _errorHandler = errorHandler;
        private readonly DebugSnapshotChannel<OffscreenMovementDebugSnapshot, OffscreenMovementDebugEvent> _offscreenMovementDebugChannel = new(
            OffscreenMovementDebugSnapshot.Empty,
            OffscreenMovementDebugTrailCapacity,
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

        public readonly record struct GridPoint(int X, int Y);

        public sealed record PathfindingDebugSnapshot(
            bool TerrainLoaded,
            int AreaWidth,
            int AreaHeight,
            int LastExpandedNodes,
            int LastPathLength,
            long LastComputeMs,
            string LastFailureReason,
            string LastTargetPath,
            GridPoint LastStart,
            GridPoint LastRequestedGoal,
            GridPoint LastResolvedGoal,
            bool LastGoalResolutionUsedFallback,
            string LastGoalResolutionNote);

        public sealed record OffscreenMovementDebugSnapshot(
            bool HasData,
            string Stage,
            string TargetPath,
            bool BuiltPath,
            bool ResolvedFromPath,
            bool ResolvedClickPoint,
            Vector2 WindowCenter,
            Vector2 TargetScreen,
            Vector2 ClickScreen,
            Vector2 PlayerGrid,
            Vector2 TargetGrid,
            string MovementSkillDebug,
            long TimestampMs)
        {
            public static readonly OffscreenMovementDebugSnapshot Empty = new(
                HasData: false,
                Stage: string.Empty,
                TargetPath: string.Empty,
                BuiltPath: false,
                ResolvedFromPath: false,
                ResolvedClickPoint: false,
                WindowCenter: default,
                TargetScreen: default,
                ClickScreen: default,
                PlayerGrid: default,
                TargetGrid: default,
                MovementSkillDebug: string.Empty,
                TimestampMs: 0);
        }

        public sealed record OffscreenMovementDebugEvent(
            string Stage,
            string TargetPath,
            bool BuiltPath,
            bool ResolvedFromPath,
            bool ResolvedClickPoint,
            Vector2 WindowCenter,
            Vector2 TargetScreen,
            Vector2 ClickScreen,
            Vector2 PlayerGrid,
            Vector2 TargetGrid,
            string MovementSkillDebug);

        public IReadOnlyList<string> GetLatestOffscreenMovementDebugTrail()
            => _offscreenMovementDebugChannel.GetTrail();

        public void PublishOffscreenMovementDebugEvent(OffscreenMovementDebugEvent debugEvent)
        {
            _offscreenMovementDebugChannel.PublishEvent(debugEvent);
            SetLatestOffscreenMovementDebug(_offscreenMovementDebugChannel.GetLatest());
        }

        public bool TryBuildPathToTarget(GameController? gameController, Entity? target, int maxExpandedNodes)
        {
            MarkPathBuildAttempt();

            if (gameController == null || target == null)
                return Fail("GameController/target unavailable.");

            if (!TryRefreshTerrainData(gameController, out bool[][] walkable, out GridPoint dims))
                return Fail("Terrain/pathfinding data unavailable.");

            if (!TryGetGridPos(gameController.Player, out GridPoint start))
                return Fail("Unable to resolve player grid position.");

            if (!TryGetGridPos(target, out GridPoint goal))
                return Fail("Unable to resolve target grid position.");

            if (!TryResolveBestEffortGoal(
                    walkable,
                    start,
                    goal,
                    out GridPoint walkableGoal,
                    out bool usedGoalFallback,
                    out string goalResolutionFailureReason))
            {
                SetGoalResolutionDebugSnapshot(
                    start,
                    goal,
                    resolvedGoal: default,
                    usedFallback: false,
                    note: goalResolutionFailureReason);
                return Fail(goalResolutionFailureReason);
            }

            string goalResolutionNote = usedGoalFallback
                ? "Using best-effort intermediate goal toward target."
                : "Using direct walkable goal near target.";
            SetGoalResolutionDebugSnapshot(start, goal, walkableGoal, usedGoalFallback, goalResolutionNote);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            List<GridPoint>? gridPath = FindPathAStar(walkable, start, walkableGoal, Math.Max(100, maxExpandedNodes), out int expandedNodes);
            sw.Stop();

            if (gridPath == null || gridPath.Count == 0)
            {
                SetFailedPathBuildSnapshot(expandedNodes, sw.ElapsedMilliseconds, target.Path ?? string.Empty, "A* did not find a route.");
                return false;
            }

            List<Vector2> screenPath = BuildScreenPathApproximation(gameController, gridPath, start, goal, target);

            SetSuccessfulPathBuildSnapshot(
                walkable,
                dims,
                expandedNodes,
                sw.ElapsedMilliseconds,
                target.Path ?? string.Empty,
                gridPath,
                screenPath);

            return true;
        }
    }
}
