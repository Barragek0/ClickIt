namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenPathingMathTests
    {
        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_ReturnsFalse_WhenWindowIsInvalid()
        {
            bool result = OffscreenPathingMath.TryResolveDirectionalWalkClickPosition(
                new RectangleF(10f, 20f, 0f, 100f),
                new Vector2(200f, 50f),
                "Metadata/TestTarget",
                static (_, _) => true,
                out Vector2 clickPos);

            result.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_ReturnsFalse_WhenTargetIsAtWindowCenter()
        {
            RectangleF windowRect = new RectangleF(10f, 20f, 100f, 80f);
            Vector2 center = new Vector2(windowRect.X + (windowRect.Width * 0.5f), windowRect.Y + (windowRect.Height * 0.5f));

            bool result = OffscreenPathingMath.TryResolveDirectionalWalkClickPosition(
                windowRect,
                center,
                "Metadata/TestTarget",
                static (_, _) => true,
                out Vector2 clickPos);

            result.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_ReturnsFirstSafeClickableCandidate()
        {
            RectangleF windowRect = new RectangleF(0f, 0f, 100f, 100f);
            List<(Vector2 Point, string Path)> calls = [];

            bool result = OffscreenPathingMath.TryResolveDirectionalWalkClickPosition(
                windowRect,
                new Vector2(80f, 50f),
                "Metadata/TestTarget",
                (point, path) =>
                {
                    calls.Add((point, path));
                    return true;
                },
                out Vector2 clickPos);

            result.Should().BeTrue();
            clickPos.X.Should().BeApproximately(69.5f, 0.01f);
            clickPos.Y.Should().BeApproximately(50f, 0.01f);
            calls.Should().ContainSingle();
            calls[0].Path.Should().Be("Metadata/TestTarget");
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_FallsBackToClampedPoint_WhenLoopCandidatesAreRejected()
        {
            RectangleF windowRect = new RectangleF(0f, 0f, 100f, 100f);
            Vector2 expectedClamped = new Vector2(72f, 50f);

            bool result = OffscreenPathingMath.TryResolveDirectionalWalkClickPosition(
                windowRect,
                new Vector2(150f, 50f),
                "Metadata/TestTarget",
                (point, _) => Math.Abs(point.X - expectedClamped.X) < 0.01f && Math.Abs(point.Y - expectedClamped.Y) < 0.01f,
                out Vector2 clickPos);

            result.Should().BeTrue();
            clickPos.Should().Be(expectedClamped);
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_ReturnsFalse_WhenNoCandidateOrClampedPointIsClickable()
        {
            bool result = OffscreenPathingMath.TryResolveDirectionalWalkClickPosition(
                new RectangleF(0f, 0f, 100f, 100f),
                new Vector2(150f, 50f),
                "Metadata/TestTarget",
                static (_, _) => false,
                out Vector2 clickPos);

            result.Should().BeFalse();
            clickPos.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryResolveDirectionalWalkClickPosition_FindsNearCenterCandidate_WhenEdgeCandidatesAndClampAreRejected()
        {
            // Target far above the safe region (e.g. under the buff bar): every candidate at t >= 0.30 and the clamped point fall in the rejected strip, but a point just off-center toward it is clickable — that keeps the walk clickable instead of returning false (which stalled the executor's walk toward a foundation near the top HUD edge).
            bool result = OffscreenPathingMath.TryResolveDirectionalWalkClickPosition(
                new RectangleF(0f, 0f, 100f, 100f),
                new Vector2(50f, -30f),
                "Metadata/TestTarget",
                static (point, _) => point.Y >= 40f,
                out Vector2 clickPos);

            result.Should().BeTrue();
            clickPos.X.Should().BeApproximately(50f, 0.01f);
            // First candidate with Y >= 40: t = 0.05 → Y = 50 + (-80) * 0.05 = 46.
            clickPos.Y.Should().BeApproximately(46f, 0.01f);
        }

        [TestMethod]
        public void FindClosestPathIndexToPlayer_ReturnsNearestManhattanNode()
        {
            var path = new[]
            {
                new PathfindingService.GridPoint(10, 10),
                new PathfindingService.GridPoint(4, 4),
                new PathfindingService.GridPoint(7, 7)
            };

            int index = OffscreenPathingMath.FindClosestPathIndexToPlayer(path, new PathfindingService.GridPoint(5, 5));

            index.Should().Be(1);
        }

        [TestMethod]
        public void FindClosestPathIndexToPlayer_ReturnsMinusOne_WhenPathIsEmpty()
        {
            OffscreenPathingMath.FindClosestPathIndexToPlayer([], new PathfindingService.GridPoint(5, 5)).Should().Be(-1);
        }

        [TestMethod]
        public void TryGetSmoothedPathDirection_ReturnsWeightedAverage_ForUpcomingPathNodes()
        {
            var path = new[]
            {
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(2, 0),
                new PathfindingService.GridPoint(4, 0),
                new PathfindingService.GridPoint(8, 0)
            };

            bool ok = OffscreenPathingMath.TryGetSmoothedPathDirection(
                path,
                new PathfindingService.GridPoint(0, 0),
                nearestIndex: 0,
                out float deltaX,
                out float deltaY);

            ok.Should().BeTrue();
            deltaX.Should().BeApproximately(5.666667f, 0.01f);
            deltaY.Should().BeApproximately(0f, 0.01f);
        }

        [TestMethod]
        public void TryGetSmoothedPathDirection_ReturnsFalse_WhenPathCannotProduceDirection()
        {
            var path = new[]
            {
                new PathfindingService.GridPoint(3, 3),
                new PathfindingService.GridPoint(3, 3)
            };

            OffscreenPathingMath.TryGetSmoothedPathDirection(
                path,
                new PathfindingService.GridPoint(3, 3),
                nearestIndex: 0,
                out _,
                out _).Should().BeFalse();

            OffscreenPathingMath.TryGetSmoothedPathDirection(
                path,
                new PathfindingService.GridPoint(3, 3),
                nearestIndex: -1,
                out _,
                out _).Should().BeFalse();
        }

        [TestMethod]
        public void CountRemainingPathNodes_ReturnsExpectedCount_ForNearestIndex()
        {
            var path = new[]
            {
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0),
                new PathfindingService.GridPoint(2, 0),
                new PathfindingService.GridPoint(3, 0)
            };

            OffscreenPathingMath.CountRemainingPathNodes(path, nearestIndex: 1).Should().Be(2);
            OffscreenPathingMath.CountRemainingPathNodes(path, nearestIndex: 99).Should().Be(0);
            OffscreenPathingMath.CountRemainingPathNodes(path, nearestIndex: -1).Should().Be(0);
            OffscreenPathingMath.CountRemainingPathNodes(path: null, nearestIndex: 0).Should().Be(0);
        }

        [TestMethod]
        public void TryComputeGridDirectionPoint_ReturnsFalse_WhenRadiusOrDirectionIsInvalid()
        {
            OffscreenPathingMath.TryComputeGridDirectionPoint(new Vector2(100f, 100f), 1f, 1f, 0f, out _).Should().BeFalse();
            OffscreenPathingMath.TryComputeGridDirectionPoint(new Vector2(100f, 100f), 0f, 0f, 25f, out _).Should().BeFalse();
        }

        [TestMethod]
        public void TryComputeGridDirectionPoint_ReturnsProjectedPoint_ForValidDirection()
        {
            bool ok = OffscreenPathingMath.TryComputeGridDirectionPoint(
                new Vector2(100f, 100f),
                deltaGridX: 4f,
                deltaGridY: 1f,
                radius: 30f,
                out Vector2 point);

            ok.Should().BeTrue();
            point.Should().NotBe(new Vector2(100f, 100f));
            Vector2.Distance(point, new Vector2(100f, 100f)).Should().BeApproximately(30f, 0.01f);
        }

        [TestMethod]
        public void IsInsideWindow_ReturnsTrue_ForEdges_AndFalse_OutsideBounds()
        {
            RectangleF window = new(100f, 200f, 1280f, 720f);

            OffscreenPathingMath.IsInsideWindow(window, new Vector2(100f, 200f)).Should().BeTrue();
            OffscreenPathingMath.IsInsideWindow(window, new Vector2(1380f, 920f)).Should().BeTrue();
            OffscreenPathingMath.IsInsideWindow(window, new Vector2(99.9f, 200f)).Should().BeFalse();
            OffscreenPathingMath.IsInsideWindow(window, new Vector2(1380.1f, 920f)).Should().BeFalse();
        }

        [TestMethod]
        public void TryGetSmoothedPathDirection_UsesUpToEightUpcomingNodes()
        {
            var path = new[]
            {
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0),
                new PathfindingService.GridPoint(2, 0),
                new PathfindingService.GridPoint(3, 0),
                new PathfindingService.GridPoint(4, 0),
                new PathfindingService.GridPoint(5, 0),
                new PathfindingService.GridPoint(6, 0),
                new PathfindingService.GridPoint(7, 0),
                new PathfindingService.GridPoint(8, 0),
                new PathfindingService.GridPoint(100, 0)
            };

            bool ok = OffscreenPathingMath.TryGetSmoothedPathDirection(
                path,
                new PathfindingService.GridPoint(0, 0),
                nearestIndex: 0,
                out float deltaX,
                out float deltaY);

            ok.Should().BeTrue();
            deltaX.Should().BeApproximately(5.666667f, 0.01f);
            deltaY.Should().BeApproximately(0f, 0.01f);
        }

        [TestMethod]
        public void GetWindowCenter_ReturnsMidpointOfRectangle()
        {
            Vector2 center = OffscreenPathingMath.GetWindowCenter(new RectangleF(100f, 200f, 1280f, 720f));

            center.Should().Be(new Vector2(740f, 560f));
        }

        [TestMethod]
        public void IsFinite_ReturnsFalse_ForNaNAndInfinityCoordinates()
        {
            OffscreenPathingMath.IsFinite(new Vector2(10f, 20f)).Should().BeTrue();
            OffscreenPathingMath.IsFinite(new Vector2(float.NaN, 20f)).Should().BeFalse();
            OffscreenPathingMath.IsFinite(new Vector2(10f, float.PositiveInfinity)).Should().BeFalse();
        }

        [TestMethod]
        public void IsNearCorner_ReturnsTrue_OnlyWhenPointIsNearBothWindowEdges()
        {
            RectangleF window = new(100f, 200f, 1280f, 720f);

            OffscreenPathingMath.IsNearCorner(new Vector2(120f, 220f), window).Should().BeTrue();
            OffscreenPathingMath.IsNearCorner(new Vector2(120f, 400f), window).Should().BeFalse();
            OffscreenPathingMath.IsNearCorner(new Vector2(500f, 220f), window).Should().BeFalse();
        }
    }
}
