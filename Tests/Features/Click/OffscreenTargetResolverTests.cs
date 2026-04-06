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

    }
}