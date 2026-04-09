namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenTargetResolverTests
    {
        [TestMethod]
        public void FindClosestPathIndexToPlayer_ReturnsNearestManhattanNode()
        {
            var path = new[]
            {
                new PathfindingService.GridPoint(10, 10),
                new PathfindingService.GridPoint(4, 4),
                new PathfindingService.GridPoint(7, 7)
            };

            int index = OffscreenTargetResolver.FindClosestPathIndexToPlayer(path, new PathfindingService.GridPoint(5, 5));

            index.Should().Be(1);
        }

        [TestMethod]
        public void FindClosestPathIndexToPlayer_ReturnsMinusOne_WhenPathIsEmpty()
        {
            OffscreenTargetResolver.FindClosestPathIndexToPlayer([], new PathfindingService.GridPoint(5, 5)).Should().Be(-1);
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

            bool ok = OffscreenTargetResolver.TryGetSmoothedPathDirection(
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

            OffscreenTargetResolver.TryGetSmoothedPathDirection(
                path,
                new PathfindingService.GridPoint(3, 3),
                nearestIndex: 0,
                out _,
                out _).Should().BeFalse();

            OffscreenTargetResolver.TryGetSmoothedPathDirection(
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

            OffscreenTargetResolver.CountRemainingPathNodes(path, nearestIndex: 1).Should().Be(2);
            OffscreenTargetResolver.CountRemainingPathNodes(path, nearestIndex: 99).Should().Be(0);
            OffscreenTargetResolver.CountRemainingPathNodes(path, nearestIndex: -1).Should().Be(0);
            OffscreenTargetResolver.CountRemainingPathNodes(path: null, nearestIndex: 0).Should().Be(0);
        }

        [TestMethod]
        public void TryComputeGridDirectionPoint_ReturnsFalse_WhenRadiusOrDirectionIsInvalid()
        {
            OffscreenTargetResolver.TryComputeGridDirectionPoint(new Vector2(100f, 100f), 1f, 1f, 0f, out _).Should().BeFalse();
            OffscreenTargetResolver.TryComputeGridDirectionPoint(new Vector2(100f, 100f), 0f, 0f, 25f, out _).Should().BeFalse();
        }

        [TestMethod]
        public void TryComputeGridDirectionPoint_ReturnsProjectedPoint_ForValidDirection()
        {
            bool ok = OffscreenTargetResolver.TryComputeGridDirectionPoint(
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

            OffscreenTargetResolver.IsInsideWindow(window, new Vector2(100f, 200f)).Should().BeTrue();
            OffscreenTargetResolver.IsInsideWindow(window, new Vector2(1380f, 920f)).Should().BeTrue();
            OffscreenTargetResolver.IsInsideWindow(window, new Vector2(99.9f, 200f)).Should().BeFalse();
            OffscreenTargetResolver.IsInsideWindow(window, new Vector2(1380.1f, 920f)).Should().BeFalse();
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

            bool ok = OffscreenTargetResolver.TryGetSmoothedPathDirection(
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
            Vector2 center = InvokePrivateStatic<Vector2>(
                nameof(OffscreenTargetResolver),
                "GetWindowCenter",
                new RectangleF(100f, 200f, 1280f, 720f));

            center.Should().Be(new Vector2(740f, 560f));
        }

        [TestMethod]
        public void IsFinite_ReturnsFalse_ForNaNAndInfinityCoordinates()
        {
            InvokePrivateStatic<bool>(nameof(OffscreenTargetResolver), "IsFinite", new Vector2(10f, 20f)).Should().BeTrue();
            InvokePrivateStatic<bool>(nameof(OffscreenTargetResolver), "IsFinite", new Vector2(float.NaN, 20f)).Should().BeFalse();
            InvokePrivateStatic<bool>(nameof(OffscreenTargetResolver), "IsFinite", new Vector2(10f, float.PositiveInfinity)).Should().BeFalse();
        }

        [TestMethod]
        public void IsNearCorner_ReturnsTrue_OnlyWhenPointIsNearBothWindowEdges()
        {
            RectangleF window = new(100f, 200f, 1280f, 720f);

            InvokePrivateStatic<bool>(nameof(OffscreenTargetResolver), "IsNearCorner", new Vector2(120f, 220f), window).Should().BeTrue();
            InvokePrivateStatic<bool>(nameof(OffscreenTargetResolver), "IsNearCorner", new Vector2(120f, 400f), window).Should().BeFalse();
            InvokePrivateStatic<bool>(nameof(OffscreenTargetResolver), "IsNearCorner", new Vector2(500f, 220f), window).Should().BeFalse();
        }

        private static T InvokePrivateStatic<T>(string typeName, string methodName, params object[] args)
        {
            MethodInfo method = typeof(OffscreenTargetResolver).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Unable to find {typeName}.{methodName}.");

            object? result = method.Invoke(null, args);
            return result is T typed
                ? typed
                : throw new InvalidOperationException($"Unexpected return type from {typeName}.{methodName}.");
        }

    }
}