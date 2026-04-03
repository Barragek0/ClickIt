namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenTargetResolverTests
    {
        [TestMethod]
        public void CountRemainingPathNodes_HandlesEmptyAndBounds()
        {
            OffscreenTargetResolver.CountRemainingPathNodes(null, 0).Should().Be(0);

            var path = new[]
            {
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0),
                new PathfindingService.GridPoint(2, 0)
            };

            OffscreenTargetResolver.CountRemainingPathNodes(path, -1).Should().Be(0);
            OffscreenTargetResolver.CountRemainingPathNodes(path, 0).Should().Be(2);
            OffscreenTargetResolver.CountRemainingPathNodes(path, 2).Should().Be(0);
            OffscreenTargetResolver.CountRemainingPathNodes(path, 100).Should().Be(0);
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

            int index = OffscreenTargetResolver.FindClosestPathIndexToPlayer(path, new PathfindingService.GridPoint(5, 5));

            index.Should().Be(1);
        }

        [TestMethod]
        public void TryComputeGridDirectionPoint_ReturnsNormalizedProjectedPoint_WhenInputValid()
        {
            bool ok = OffscreenTargetResolver.TryComputeGridDirectionPoint(
                new Vector2(100, 100),
                deltaGridX: 5,
                deltaGridY: 2,
                radius: 30,
                out Vector2 point);

            ok.Should().BeTrue();
            point.X.Should().NotBe(100);
            point.Y.Should().NotBe(100);
        }

        [TestMethod]
        public void IsInsideWindow_RecognizesInsideAndOutsidePoints()
        {
            RectangleF window = new RectangleF(10, 20, 100, 80);

            OffscreenTargetResolver.IsInsideWindow(window, new Vector2(40, 50)).Should().BeTrue();
            OffscreenTargetResolver.IsInsideWindow(window, new Vector2(9, 50)).Should().BeFalse();
            OffscreenTargetResolver.IsInsideWindow(window, new Vector2(40, 101)).Should().BeFalse();
        }
    }
}