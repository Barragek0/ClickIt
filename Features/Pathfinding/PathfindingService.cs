namespace ClickIt.Features.Pathfinding
{
    public sealed class PathfindingService(
        ErrorHandler? errorHandler = null,
        Action<double>? recordProcessingMs = null,
        Action<long>? recordAllocationBytes = null,
        BreakdownRecorder? recordBreakdown = null)
    {
        public const string AStarNoRouteFailureReason = "A* did not find a route.";

        private readonly ErrorHandler? _errorHandler = errorHandler;
        private readonly Action<double>? _recordProcessingMs = recordProcessingMs;
        private readonly Action<long>? _recordAllocationBytes = recordAllocationBytes;
        private readonly BreakdownRecorder? _recordBreakdown = recordBreakdown;
        private readonly OffscreenMovementDiagnosticsChannel _offscreenMovementDiagnostics = new();
        private readonly PathfindingTerrainCache _terrainCache = new();
        private readonly DedupEventBuffer _debugEvents = new();

        internal PathfindingRuntimeState RuntimeState { get; } = new();

        public readonly record struct GridPoint(int X, int Y);

        public PathfindingDebugSnapshot GetDebugSnapshot()
            => RuntimeState.GetDebugSnapshot();

        internal void AddDebugStage(string message) => _debugEvents.Add(message);

        internal IReadOnlyList<string> GetDebugEvents() => _debugEvents.Events;

        public IReadOnlyList<Vector2> GetLatestScreenPath()
            => RuntimeState.GetLatestScreenPath();

        public IReadOnlyList<GridPoint> GetLatestGridPath()
            => RuntimeState.GetLatestGridPath();

        public OffscreenMovementDebugSnapshot GetLatestOffscreenMovementDebug()
            => RuntimeState.GetLatestOffscreenMovementDebug();

        public void SetLatestOffscreenMovementDebug(OffscreenMovementDebugSnapshot snapshot)
            => RuntimeState.SetLatestOffscreenMovementDebug(snapshot);

        public void ClearLatestPath()
            => RuntimeState.ClearLatestPath();

        internal bool ClearPathIfStale(int staleTimeoutMs)
            => RuntimeState.ClearPathIfStale(staleTimeoutMs);

        private void MarkPathBuildAttempt()
            => RuntimeState.MarkPathBuildAttempt();

        private void SetFailedPathBuildSnapshot(int expandedNodes, long computeMs, string targetPath, string failureReason)
            => RuntimeState.SetFailedPathBuildSnapshot(expandedNodes, computeMs, targetPath, failureReason);

        private void SetSuccessfulPathBuildSnapshot(
            bool[][] walkable,
            GridPoint dims,
            int expandedNodes,
            long computeMs,
            string targetPath,
            IReadOnlyList<GridPoint> gridPath,
            IReadOnlyList<Vector2> screenPath)
            => RuntimeState.SetSuccessfulPathBuildSnapshot(walkable, dims, expandedNodes, computeMs, targetPath, gridPath, screenPath);

        private void SetGoalResolutionDebugSnapshot(
            GridPoint start,
            GridPoint requestedGoal,
            GridPoint resolvedGoal,
            bool usedFallback,
            string note)
            => RuntimeState.SetGoalResolutionDebugSnapshot(start, requestedGoal, resolvedGoal, usedFallback, note);

        private bool Fail(string reason)
        {
            RuntimeState.Fail(reason);

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
            long start = Stopwatch.GetTimestamp();
            long allocStart = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                return TryBuildPathToTargetCore(gameController, target, maxExpandedNodes);
            }
            finally
            {
                _recordProcessingMs?.Invoke((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                _recordAllocationBytes?.Invoke(GC.GetAllocatedBytesForCurrentThread() - allocStart);
            }
        }

        private bool TryBuildPathToTargetCore(GameController? gameController, Entity? target, int maxExpandedNodes)
        {
            MarkPathBuildAttempt();

            if (gameController == null || target == null)
            {
                AddDebugStage("Pathfind: aborted - GameController/target unavailable");
                return Fail("GameController/target unavailable.");
            }

            long terrainStart = Stopwatch.GetTimestamp();
            long terrainAllocStart = GC.GetAllocatedBytesForCurrentThread();
            if (!PathTerrainSnapshotProvider.TryRefreshTerrainData(gameController, _terrainCache, out bool[][] walkable, out GridPoint dims, out bool terrainFromCache))
            {
                AddDebugStage("Pathfind: aborted - terrain/pathfinding data unavailable");
                return Fail("Terrain/pathfinding data unavailable.");
            }

            AddDebugStage(terrainFromCache
                ? $"Pathfind: terrain cache hit grid={dims.X}x{dims.Y}"
                : $"Pathfind: terrain REBUILT grid={dims.X}x{dims.Y} churn={_terrainCache.ChurnRebuildCount}");

            RuntimeState.SetTerrainSnapshot(walkable, dims);
            double terrainMs = (Stopwatch.GetTimestamp() - terrainStart) * 1000.0 / Stopwatch.Frequency;
            long terrainBytes = GC.GetAllocatedBytesForCurrentThread() - terrainAllocStart;

            long goalStart = Stopwatch.GetTimestamp();
            long goalAllocStart = GC.GetAllocatedBytesForCurrentThread();

            if (!PathGridSearch.TryGetGridPos(gameController.Player, out GridPoint start))
            {
                AddDebugStage("Pathfind: aborted - player grid position unavailable");
                return Fail("Unable to resolve player grid position.");
            }

            if (!PathGridSearch.TryGetGridPos(target, out GridPoint goal))
            {
                AddDebugStage("Pathfind: aborted - target grid position unavailable");
                return Fail("Unable to resolve target grid position.");
            }

            if (!PathGridSearch.TryResolveBestEffortGoal(
                    walkable,
                    start,
                    goal,
                    out GridPoint walkableGoal,
                    out bool usedGoalFallback,
                    out string goalResolutionFailureReason))
            {
                AddDebugStage($"Pathfind: goal resolution FAILED start=({start.X},{start.Y}) goal=({goal.X},{goal.Y}) - {goalResolutionFailureReason}");
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

            AddDebugStage($"Pathfind: goal start=({start.X},{start.Y}) goal=({goal.X},{goal.Y}) res=({walkableGoal.X},{walkableGoal.Y}) {(usedGoalFallback ? "fallback" : "direct")}");
            double goalMs = (Stopwatch.GetTimestamp() - goalStart) * 1000.0 / Stopwatch.Frequency;
            long goalBytes = GC.GetAllocatedBytesForCurrentThread() - goalAllocStart;

            long astarStart = Stopwatch.GetTimestamp();
            long astarAllocStart = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch sw = Stopwatch.StartNew();
            List<GridPoint>? gridPath = PathGridSearch.FindPathAStar(walkable, start, walkableGoal, SystemMath.Max(100, maxExpandedNodes), out int expandedNodes);
            sw.Stop();
            double astarMs = (Stopwatch.GetTimestamp() - astarStart) * 1000.0 / Stopwatch.Frequency;
            long astarBytes = GC.GetAllocatedBytesForCurrentThread() - astarAllocStart;

            if (gridPath == null || gridPath.Count == 0)
            {
                RecordBreakdown(terrainBytes, terrainMs, goalBytes, goalMs, astarBytes, astarMs, 0, 0);
                AddDebugStage($"Pathfind: A* NO ROUTE start=({start.X},{start.Y}) goal=({walkableGoal.X},{walkableGoal.Y}) nodes={expandedNodes} ms={sw.ElapsedMilliseconds}");
                SetFailedPathBuildSnapshot(expandedNodes, sw.ElapsedMilliseconds, target.Path ?? string.Empty, AStarNoRouteFailureReason);
                return false;
            }

            AddDebugStage($"Pathfind: route OK start=({start.X},{start.Y}) goal=({walkableGoal.X},{walkableGoal.Y}) nodes={expandedNodes} len={gridPath.Count} ms={sw.ElapsedMilliseconds}");

            long projStart = Stopwatch.GetTimestamp();
            long projAllocStart = GC.GetAllocatedBytesForCurrentThread();
            List<Vector2> screenPath = ScreenPathProjector.BuildScreenPathApproximation(gameController, gridPath, start, goal, target);

            SetSuccessfulPathBuildSnapshot(
                walkable,
                dims,
                expandedNodes,
                sw.ElapsedMilliseconds,
                target.Path ?? string.Empty,
                gridPath,
                screenPath);
            double projMs = (Stopwatch.GetTimestamp() - projStart) * 1000.0 / Stopwatch.Frequency;
            long projBytes = GC.GetAllocatedBytesForCurrentThread() - projAllocStart;

            RecordBreakdown(terrainBytes, terrainMs, goalBytes, goalMs, astarBytes, astarMs, projBytes, projMs);
            return true;
        }

        private void RecordBreakdown(
            long terrainBytes, double terrainMs,
            long goalBytes, double goalMs,
            long astarBytes, double astarMs,
            long projBytes, double projMs)
        {
            if (_recordBreakdown == null)
                return;
            Span<long> bytes = stackalloc long[4];
            Span<double> ms = stackalloc double[4];
            bytes[0] = terrainBytes; ms[0] = terrainMs;
            bytes[1] = goalBytes; ms[1] = goalMs;
            bytes[2] = astarBytes; ms[2] = astarMs;
            bytes[3] = projBytes; ms[3] = projMs;
            _recordBreakdown.Invoke(bytes, ms);
        }
    }
}
