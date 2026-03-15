using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    public sealed partial class PathfindingService(ClickItSettings settings, Utils.ErrorHandler? errorHandler = null)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Utils.ErrorHandler? _errorHandler = errorHandler;

        public readonly record struct GridPoint(int X, int Y);

        public sealed record PathfindingDebugSnapshot(
            bool TerrainLoaded,
            int AreaWidth,
            int AreaHeight,
            int LastExpandedNodes,
            int LastPathLength,
            long LastComputeMs,
            string LastFailureReason,
            string LastTargetPath);

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

            if (!IsInside(start, dims) || !IsInside(goal, dims))
                return Fail("Grid positions are outside area dimensions.");

            if (!TryResolveWalkableGoal(walkable, goal, maxRadius: 18, out GridPoint walkableGoal))
                return Fail("No walkable tile found near target grid position.");

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
