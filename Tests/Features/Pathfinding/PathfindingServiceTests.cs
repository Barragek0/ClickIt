namespace ClickIt.Tests.Features.Pathfinding
{
    [TestClass]
    public class PathfindingServiceTests
    {
        [TestMethod]
        public void TryBuildPathToTarget_ReturnsFalse_WhenGameControllerIsNull()
        {
            var service = new PathfindingService();
            Entity target = ExileCoreOpaqueFactory.CreateOpaqueEntity();

            bool result = service.TryBuildPathToTarget(gameController: null, target, maxExpandedNodes: 500);

            result.Should().BeFalse();
            service.GetDebugSnapshot().LastFailureReason.Should().Be("GameController/target unavailable.");
            service.GetLatestGridPath().Should().BeEmpty();
            service.GetLatestScreenPath().Should().BeEmpty();
        }

        [TestMethod]
        public void TryBuildPathToTarget_ReturnsFalse_WhenTargetIsNull()
        {
            var service = new PathfindingService();
            GameController gameController = ExileCoreOpaqueFactory.CreateOpaqueGameController();

            bool result = service.TryBuildPathToTarget(gameController, target: null, maxExpandedNodes: 500);

            result.Should().BeFalse();
            service.GetDebugSnapshot().LastFailureReason.Should().Be("GameController/target unavailable.");
            service.GetLatestGridPath().Should().BeEmpty();
            service.GetLatestScreenPath().Should().BeEmpty();
        }

        [TestMethod]
        public void TryBuildPathToTarget_RecordsProcessingTiming_WhenHookProvided()
        {
            double recordedMs = -1;
            var service = new PathfindingService(errorHandler: null, recordProcessingMs: ms => recordedMs = ms);
            Entity target = ExileCoreOpaqueFactory.CreateOpaqueEntity();

            bool result = service.TryBuildPathToTarget(gameController: null, target, maxExpandedNodes: 500);

            result.Should().BeFalse();
            recordedMs.Should().BeGreaterThanOrEqualTo(0, "the processing hook fires even on the early-abort path");
        }

        [TestMethod]
        public void FindPathAStar_FindsPath_OnSimpleWalkableGrid()
        {
            bool[][] grid =
            [
                [true, true, true],
                [true, false, true],
                [true, true, true]
            ];

            var path = PathGridSearch.FindPathAStar(
                grid,
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(2, 2),
                500,
                out int expanded);

            path.Should().NotBeNull();
            path!.Count.Should().BeGreaterThan(0);
            path[0].Should().Be(new PathfindingService.GridPoint(0, 0));
            path[^1].Should().Be(new PathfindingService.GridPoint(2, 2));
            expanded.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void FindPathAStar_RespectsSearchBudget_WhenTooLow()
        {
            bool[][] grid =
            [
                [true, true, true, true, true],
                [true, true, true, true, true],
                [true, true, true, true, true],
                [true, true, true, true, true],
                [true, true, true, true, true]
            ];

            var path = PathGridSearch.FindPathAStar(
                grid,
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(4, 4),
                1,
                out int expanded);

            path.Should().BeNull();
            expanded.Should().Be(1);
        }

        [TestMethod]
        public void FindPathAStar_ConsecutiveCalls_OnDifferentGrids_DoNotLeakState()
        {
            // A* reuses per-thread search buffers with a generation stamp. Run several searches
            // back-to-back on different-sized grids (with obstacles) and verify each still returns
            // a valid path from its own start to its own goal.
            bool[][] small =
            [
                [true, true, true],
                [true, false, true],
                [true, true, true]
            ];
            bool[][] medium = CreateGrid(width: 12, height: 12, defaultValue: true);
            medium[5][5] = false;
            medium[5][6] = false;
            medium[6][5] = false;
            bool[][] large = CreateGrid(width: 40, height: 40, defaultValue: true);
            for (int i = 10; i < 30; i++)
                large[20][i] = false;

            (bool[][] Grid, PathfindingService.GridPoint Start, PathfindingService.GridPoint Goal)[] cases =
            [
                (small, new PathfindingService.GridPoint(0, 0), new PathfindingService.GridPoint(2, 2)),
                (medium, new PathfindingService.GridPoint(0, 0), new PathfindingService.GridPoint(11, 11)),
                (large, new PathfindingService.GridPoint(0, 0), new PathfindingService.GridPoint(39, 39)),
                (small, new PathfindingService.GridPoint(2, 0), new PathfindingService.GridPoint(0, 2)),
            ];

            for (int i = 0; i < cases.Length; i++)
            {
                (bool[][] grid, PathfindingService.GridPoint start, PathfindingService.GridPoint goal) = cases[i];
                var path = PathGridSearch.FindPathAStar(grid, start, goal, 5000, out int expanded);
                path.Should().NotBeNull($"search {i} should find a path");
                path![0].Should().Be(start);
                path[^1].Should().Be(goal);
                expanded.Should().BeGreaterThan(0);
            }
        }

        [TestMethod]
        public void FindPathAStar_AfterBudgetLimitedFailure_SubsequentSearchStillSucceeds()
        {
            bool[][] grid = CreateGrid(width: 20, height: 20, defaultValue: true);

            // First search is starved by a tiny budget and aborts partway through.
            var failed = PathGridSearch.FindPathAStar(
                grid,
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(19, 19),
                2,
                out int failedExpanded);
            failed.Should().BeNull();
            failedExpanded.Should().Be(2);

            // A subsequent search must not be corrupted by the aborted one.
            var path = PathGridSearch.FindPathAStar(
                grid,
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(19, 19),
                5000,
                out int expanded);
            path.Should().NotBeNull();
            path![0].Should().Be(new PathfindingService.GridPoint(0, 0));
            path[^1].Should().Be(new PathfindingService.GridPoint(19, 19));
            expanded.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void TryResolveWalkableGoal_ReturnsAdjacentWalkableTile_WhenDesiredGoalBlocked()
        {
            bool[][] grid =
            [
                [true, true, true],
                [true, false, true],
                [true, true, true]
            ];

            bool ok = PathGridSearch.TryResolveWalkableGoal(
                grid,
                new PathfindingService.GridPoint(1, 1),
                maxRadius: 2,
                out PathfindingService.GridPoint resolved);

            ok.Should().BeTrue();
            resolved.Should().NotBe(new PathfindingService.GridPoint(1, 1));
            grid[resolved.Y][resolved.X].Should().BeTrue();
        }

        [TestMethod]
        public void TryResolveWalkableGoal_ReturnsFalse_WhenNoWalkableTileInRadius()
        {
            bool[][] grid =
            [
                [false, false, false],
                [false, false, false],
                [false, false, false]
            ];

            bool ok = PathGridSearch.TryResolveWalkableGoal(
                grid,
                new PathfindingService.GridPoint(1, 1),
                maxRadius: 2,
                out _);

            ok.Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveBestEffortGoal_UsesDirectGoal_WhenTargetIsInGridAndWalkable()
        {
            bool[][] grid =
            [
                [true, true, true],
                [true, true, true],
                [true, true, true]
            ];

            bool ok = PathGridSearch.TryResolveBestEffortGoal(
                grid,
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(2, 2),
                out PathfindingService.GridPoint resolved,
                out bool usedFallback,
                out string failureReason);

            ok.Should().BeTrue();
            usedFallback.Should().BeFalse();
            failureReason.Should().BeEmpty();
            resolved.Should().Be(new PathfindingService.GridPoint(2, 2));
        }

        [TestMethod]
        public void TryResolveBestEffortGoal_UsesFallbackForFarOffGridTarget_AndProducesReachableGoal()
        {
            bool[][] grid =
            [
                [true, true, true, true, true],
                [true, true, true, true, true],
                [true, true, true, true, true],
                [true, true, true, true, true],
                [true, true, true, true, true]
            ];

            PathfindingService.GridPoint start = new PathfindingService.GridPoint(2, 2);
            bool ok = PathGridSearch.TryResolveBestEffortGoal(
                grid,
                start,
                new PathfindingService.GridPoint(250, 250),
                out PathfindingService.GridPoint resolved,
                out bool usedFallback,
                out string failureReason);

            ok.Should().BeTrue();
            usedFallback.Should().BeTrue();
            failureReason.Should().BeEmpty();
            resolved.Should().NotBe(start);

            var path = PathGridSearch.FindPathAStar(grid, start, resolved, 500, out int expandedNodes);
            path.Should().NotBeNull();
            path!.Count.Should().BeGreaterThan(1);
            expandedNodes.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void TryResolveBestEffortGoal_ReturnsFalse_WhenTerrainDataIsUnavailable()
        {
            bool ok = PathGridSearch.TryResolveBestEffortGoal(
                walkable: [],
                start: new PathfindingService.GridPoint(0, 0),
                desiredGoal: new PathfindingService.GridPoint(5, 5),
                out _,
                out bool usedFallback,
                out string failureReason);

            ok.Should().BeFalse();
            usedFallback.Should().BeFalse();
            failureReason.Should().Be("Terrain/pathfinding data unavailable.");
        }

        [TestMethod]
        public void TryResolveBestEffortGoal_ReturnsFalse_WhenStartIsOutsideGrid()
        {
            bool[][] grid =
            [
                [true, true, true],
                [true, true, true],
                [true, true, true]
            ];

            bool ok = PathGridSearch.TryResolveBestEffortGoal(
                grid,
                start: new PathfindingService.GridPoint(-1, 0),
                desiredGoal: new PathfindingService.GridPoint(2, 2),
                out _,
                out bool usedFallback,
                out string failureReason);

            ok.Should().BeFalse();
            usedFallback.Should().BeFalse();
            failureReason.Should().Be("Player grid position is outside walkable grid bounds.");
        }

        [TestMethod]
        public void TryResolveBestEffortGoal_UsesSteppedFallback_WhenClampedGoalHasNoNearbyWalkableTile()
        {
            bool[][] grid = CreateGrid(width: 60, height: 60, defaultValue: false);
            grid[10][10] = true;
            grid[12][12] = true;

            bool ok = PathGridSearch.TryResolveBestEffortGoal(
                grid,
                start: new PathfindingService.GridPoint(10, 10),
                desiredGoal: new PathfindingService.GridPoint(59, 59),
                out PathfindingService.GridPoint resolved,
                out bool usedFallback,
                out string failureReason);

            ok.Should().BeTrue();
            usedFallback.Should().BeTrue();
            failureReason.Should().BeEmpty();
            resolved.Should().Be(new PathfindingService.GridPoint(12, 12));
        }

        [TestMethod]
        public void TryResolveBestEffortGoal_ReturnsFalse_WhenNoReachableWalkableTileExists()
        {
            bool[][] grid = CreateGrid(width: 40, height: 40, defaultValue: false);

            bool ok = PathGridSearch.TryResolveBestEffortGoal(
                grid,
                start: new PathfindingService.GridPoint(10, 10),
                desiredGoal: new PathfindingService.GridPoint(39, 39),
                out _,
                out bool usedFallback,
                out string failureReason);

            ok.Should().BeFalse();
            usedFallback.Should().BeFalse();
            failureReason.Should().Be("No reachable walkable tile found toward target within current terrain coverage.");
        }

        [TestMethod]
        public void TryResolveWalkableSample_ReturnsFalse_WhenSampleIsOutsideGrid()
        {
            bool[][] grid =
            [
                [true, true],
                [true, true]
            ];
            object?[] args = [grid, new PathfindingService.GridPoint(-1, 0), default(PathfindingService.GridPoint)];

            bool ok = InvokePrivateGridMethod<bool>("TryResolveWalkableSample", args);

            ok.Should().BeFalse();
            ((PathfindingService.GridPoint)args[2]!).Should().Be(new PathfindingService.GridPoint(-1, 0));
        }

        [TestMethod]
        public void TryResolveWalkableSample_UsesNearbyWalkableFallback_WhenSampleIsBlocked()
        {
            bool[][] grid =
            [
                [true, true, true],
                [true, false, true],
                [true, true, true]
            ];
            object?[] args = [grid, new PathfindingService.GridPoint(1, 1), default(PathfindingService.GridPoint)];

            bool ok = InvokePrivateGridMethod<bool>("TryResolveWalkableSample", args);

            ok.Should().BeTrue();
            PathfindingService.GridPoint resolved = (PathfindingService.GridPoint)args[2]!;
            resolved.Should().NotBe(new PathfindingService.GridPoint(1, 1));
            grid[resolved.Y][resolved.X].Should().BeTrue();
        }

        [TestMethod]
        public void TryFindFurthestReachableGoalTowardTarget_ReturnsFurthestProgressingWalkableCandidate()
        {
            bool[][] grid = CreateGrid(width: 10, height: 10, defaultValue: false);
            grid[0][0] = true;
            grid[2][2] = true;
            grid[6][6] = true;
            object?[] args = [grid, new PathfindingService.GridPoint(0, 0), new PathfindingService.GridPoint(9, 9), default(PathfindingService.GridPoint)];

            bool ok = InvokePrivateGridMethod<bool>("TryFindFurthestReachableGoalTowardTarget", args);

            ok.Should().BeTrue();
            ((PathfindingService.GridPoint)args[3]!).Should().Be(new PathfindingService.GridPoint(6, 6));
        }

        [TestMethod]
        public void TryFindFurthestReachableGoalTowardTarget_ReturnsFalse_WhenNoProgressingCandidateExists()
        {
            bool[][] grid = CreateGrid(width: 8, height: 8, defaultValue: false);
            grid[0][0] = true;
            object?[] args = [grid, new PathfindingService.GridPoint(0, 0), new PathfindingService.GridPoint(7, 7), default(PathfindingService.GridPoint)];

            bool ok = InvokePrivateGridMethod<bool>("TryFindFurthestReachableGoalTowardTarget", args);

            ok.Should().BeFalse();
            ((PathfindingService.GridPoint)args[3]!).Should().Be(new PathfindingService.GridPoint(0, 0));
        }

        [TestMethod]
        public void ClearLatestPath_ResetsPathSnapshot()
        {
            var service = new PathfindingService();

            service.RuntimeState.SetLatestPathState(
                new System.Collections.Generic.List<PathfindingService.GridPoint>
            {
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 1)
            },
                new System.Collections.Generic.List<SharpDX.Vector2>
            {
                new SharpDX.Vector2(10, 10),
                new SharpDX.Vector2(20, 20)
            },
                "Metadata/SomeTarget");

            service.ClearLatestPath();

            service.GetLatestGridPath().Count.Should().Be(0);
            service.GetLatestScreenPath().Count.Should().Be(0);
            service.GetDebugSnapshot().LastTargetPath.Should().BeEmpty();
        }

        [TestMethod]
        public void ClearPathIfStale_ClearsStoredPath_WhenTimeoutExceeded()
        {
            var service = new PathfindingService();

            service.RuntimeState.SetLatestPathState(
                new System.Collections.Generic.List<PathfindingService.GridPoint>
            {
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            },
                screenPath: null,
                targetPath: "Metadata/SomeTarget",
                lastPathBuildAttemptTickMs: Environment.TickCount64 - 5000);

            bool cleared = service.ClearPathIfStale(1000);

            cleared.Should().BeTrue();
            service.GetLatestGridPath().Count.Should().Be(0);
        }

        [TestMethod]
        public void ClearPathIfStale_DoesNotClearStoredPath_WhenStillRecent()
        {
            var service = new PathfindingService();

            service.RuntimeState.SetLatestPathState(
                new System.Collections.Generic.List<PathfindingService.GridPoint>
            {
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            },
                screenPath: null,
                targetPath: "Metadata/SomeTarget",
                lastPathBuildAttemptTickMs: Environment.TickCount64);

            bool cleared = service.ClearPathIfStale(1000);

            cleared.Should().BeFalse();
            service.GetLatestGridPath().Count.Should().Be(2);
        }

        [TestMethod]
        public void PublishOffscreenMovementDebugEvent_UpdatesLatestSnapshotAndTrail()
        {
            var service = new PathfindingService();

            service.PublishOffscreenMovementDebugEvent(new OffscreenMovementDebugEvent(
                Stage: "Traverse",
                TargetPath: "Metadata/OffscreenTarget",
                BuiltPath: true,
                ResolvedFromPath: true,
                ResolvedClickPoint: true,
                WindowCenter: new SharpDX.Vector2(100, 100),
                TargetScreen: new SharpDX.Vector2(150, 140),
                ClickScreen: new SharpDX.Vector2(120, 118),
                PlayerGrid: new SharpDX.Vector2(10, 20),
                TargetGrid: new SharpDX.Vector2(30, 40),
                MovementSkillDebug: "ShieldCharge"));

            OffscreenMovementDebugSnapshot snapshot = service.GetLatestOffscreenMovementDebug();

            snapshot.HasData.Should().BeTrue();
            snapshot.Stage.Should().Be("Traverse");
            snapshot.TargetPath.Should().Be("Metadata/OffscreenTarget");
            snapshot.MovementSkillDebug.Should().Be("ShieldCharge");
            service.GetLatestOffscreenMovementDebugTrail().Should().ContainSingle();
        }

        private static bool[][] CreateGrid(int width, int height, bool defaultValue)
        {
            bool[][] grid = new bool[height][];
            for (int y = 0; y < height; y++)
            {
                grid[y] = new bool[width];
                if (!defaultValue)
                    continue;

                Array.Fill(grid[y], true);
            }

            return grid;
        }

        private static T InvokePrivateGridMethod<T>(string methodName, object?[] arguments)
            => (T)typeof(PathGridSearch)
                .GetMethod(methodName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(null, arguments)!;
    }
}
