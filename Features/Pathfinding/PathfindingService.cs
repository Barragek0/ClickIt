namespace ClickIt.Features.Pathfinding
{
    public sealed class PathfindingService(ClickItSettings settings, ErrorHandler? errorHandler = null)
    {
        public const string AStarNoRouteFailureReason = "A* did not find a route.";

        private readonly ClickItSettings _settings = settings;
        private readonly ErrorHandler? _errorHandler = errorHandler;
        private readonly PathfindingRuntimeState _runtimeState = new();
        private readonly OffscreenMovementDiagnosticsChannel _offscreenMovementDiagnostics = new();

        internal PathfindingRuntimeState RuntimeState => _runtimeState;

        public readonly record struct GridPoint(int X, int Y);

        public PathfindingDebugSnapshot GetDebugSnapshot()
            => _runtimeState.GetDebugSnapshot();

        public IReadOnlyList<Vector2> GetLatestScreenPath()
            => _runtimeState.GetLatestScreenPath();

        public IReadOnlyList<GridPoint> GetLatestGridPath()
            => _runtimeState.GetLatestGridPath();

        public OffscreenMovementDebugSnapshot GetLatestOffscreenMovementDebug()
            => _runtimeState.GetLatestOffscreenMovementDebug();

        public void SetLatestOffscreenMovementDebug(OffscreenMovementDebugSnapshot snapshot)
            => _runtimeState.SetLatestOffscreenMovementDebug(snapshot);

        public void ClearLatestPath()
            => _runtimeState.ClearLatestPath();

        internal bool ClearPathIfStale(int staleTimeoutMs)
            => _runtimeState.ClearPathIfStale(staleTimeoutMs);

        private void MarkPathBuildAttempt()
            => _runtimeState.MarkPathBuildAttempt();

        private void SetFailedPathBuildSnapshot(int expandedNodes, long computeMs, string targetPath, string failureReason)
            => _runtimeState.SetFailedPathBuildSnapshot(expandedNodes, computeMs, targetPath, failureReason);

        private void SetSuccessfulPathBuildSnapshot(
            bool[][] walkable,
            GridPoint dims,
            int expandedNodes,
            long computeMs,
            string targetPath,
            IReadOnlyList<GridPoint> gridPath,
            IReadOnlyList<Vector2> screenPath)
            => _runtimeState.SetSuccessfulPathBuildSnapshot(walkable, dims, expandedNodes, computeMs, targetPath, gridPath, screenPath);

        private void SetGoalResolutionDebugSnapshot(
            GridPoint start,
            GridPoint requestedGoal,
            GridPoint resolvedGoal,
            bool usedFallback,
            string note)
            => _runtimeState.SetGoalResolutionDebugSnapshot(start, requestedGoal, resolvedGoal, usedFallback, note);

        private bool Fail(string reason)
        {
            _runtimeState.Fail(reason);

            _errorHandler?.LogMessage(localDebug: true, message: $"PathfindingService: {reason}", frame: 10);
            return false;
        }

        public IReadOnlyList<string> GetLatestOffscreenMovementDebugTrail()
            => _offscreenMovementDiagnostics.GetTrail();

        public void PublishOffscreenMovementDebugEvent(OffscreenMovementDebugEvent debugEvent)
        {
            _offscreenMovementDiagnostics.PublishEvent(debugEvent);
            SetLatestOffscreenMovementDebug(_offscreenMovementDiagnostics.GetLatest());
        }

        public bool TryBuildPathToTarget(GameController? gameController, Entity? target, int maxExpandedNodes)
        {
            MarkPathBuildAttempt();

            if (gameController == null || target == null)
                return Fail("GameController/target unavailable.");

            if (!PathTerrainSnapshotProvider.TryRefreshTerrainData(gameController, out bool[][] walkable, out GridPoint dims))
                return Fail("Terrain/pathfinding data unavailable.");

            _runtimeState.SetTerrainSnapshot(walkable, dims);

            if (!PathGridSearch.TryGetGridPos(gameController.Player, out GridPoint start))
                return Fail("Unable to resolve player grid position.");

            if (!PathGridSearch.TryGetGridPos(target, out GridPoint goal))
                return Fail("Unable to resolve target grid position.");

            if (!PathGridSearch.TryResolveBestEffortGoal(
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
            List<GridPoint>? gridPath = PathGridSearch.FindPathAStar(walkable, start, walkableGoal, SystemMath.Max(100, maxExpandedNodes), out int expandedNodes);
            sw.Stop();

            if (gridPath == null || gridPath.Count == 0)
            {
                SetFailedPathBuildSnapshot(expandedNodes, sw.ElapsedMilliseconds, target.Path ?? string.Empty, AStarNoRouteFailureReason);
                return false;
            }

            List<Vector2> screenPath = ScreenPathProjector.BuildScreenPathApproximation(gameController, gridPath, start, goal, target);

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
