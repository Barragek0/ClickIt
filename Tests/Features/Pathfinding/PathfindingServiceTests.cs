using ClickIt.Features.Pathfinding.Diagnostics;
using ClickIt.Features.Pathfinding.Grid;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ClickIt.Tests.Features.Pathfinding
{
    [TestClass]
    public class PathfindingServiceTests
    {
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
        public void ClearLatestPath_ResetsPathSnapshot()
        {
            var service = new PathfindingService(new ClickItSettings());

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
            var service = new PathfindingService(new ClickItSettings());

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
            var service = new PathfindingService(new ClickItSettings());

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
            var service = new PathfindingService(new ClickItSettings());

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
    }
}
