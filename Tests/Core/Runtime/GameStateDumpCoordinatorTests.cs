namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class GameStateDumpCoordinatorTests
    {
        [TestMethod]
        public void ResolveAreaProgressPercent_SingleArea_TracksAreaProgress()
        {
            GameStateDumpCoordinator.ResolveAreaProgressPercent(areaIndex: 0, areaCount: 1, areaPct: 40).Should().Be(40);
            GameStateDumpCoordinator.ResolveAreaProgressPercent(areaIndex: 0, areaCount: 1, areaPct: 100).Should().Be(100);
        }

        [TestMethod]
        public void ResolveAreaProgressPercent_MultiArea_WeightsEarlierAreasFully()
        {
            // Two areas: first area at 50% -> overall 25%; second area at 50% -> overall 75%.
            GameStateDumpCoordinator.ResolveAreaProgressPercent(areaIndex: 0, areaCount: 2, areaPct: 50).Should().Be(25);
            GameStateDumpCoordinator.ResolveAreaProgressPercent(areaIndex: 1, areaCount: 2, areaPct: 50).Should().Be(75);
        }

        [TestMethod]
        public void ResolveProgressBucket_GroupsByQuarter()
        {
            GameStateDumpCoordinator.ResolveProgressBucket(0).Should().Be(0);
            GameStateDumpCoordinator.ResolveProgressBucket(24).Should().Be(0);
            GameStateDumpCoordinator.ResolveProgressBucket(25).Should().Be(1);
            GameStateDumpCoordinator.ResolveProgressBucket(99).Should().Be(3);
            GameStateDumpCoordinator.ResolveProgressBucket(100).Should().Be(4);
        }

        [TestMethod]
        public void ResolveRoot_ReturnsGameController_ForGameControllerTarget()
        {
            GameController gameController = ExileCoreOpaqueFactory.CreateOpaqueGameController();
            GameStateDumpCoordinator coordinator = CreateCoordinator(() => gameController);

            InvokeResolveRoot(coordinator, GameStateDumpTarget.GameController).Should().BeSameAs(gameController);
        }

        [TestMethod]
        public void ResolveRoot_ReturnsServices_ForPluginWrappersTarget()
        {
            var context = new PluginContext();
            GameStateDumpCoordinator coordinator = CreateCoordinator(() => null, context);

            InvokeResolveRoot(coordinator, GameStateDumpTarget.PluginWrappers).Should().BeSameAs(context.Services);
        }

        [TestMethod]
        public void ResolveRoot_ReturnsNull_WhenGameControllerMissing()
        {
            GameStateDumpCoordinator coordinator = CreateCoordinator(() => null);

            InvokeResolveRoot(coordinator, GameStateDumpTarget.Cache).Should().BeNull();
            InvokeResolveRoot(coordinator, GameStateDumpTarget.Player).Should().BeNull();
            InvokeResolveRoot(coordinator, GameStateDumpTarget.IngameState).Should().BeNull();
            InvokeResolveRoot(coordinator, GameStateDumpTarget.UiHover).Should().BeNull();
        }

        private static GameStateDumpCoordinator CreateCoordinator(Func<GameController?> getGameController, PluginContext? context = null)
            => new(new DebugClipboardServiceDependencies(
                context ?? new PluginContext(),
                new ClickIt(),
                getGameController));

        private static object? InvokeResolveRoot(GameStateDumpCoordinator coordinator, GameStateDumpTarget target)
        {
            return typeof(GameStateDumpCoordinator)
                .GetMethod("ResolveRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(coordinator, [target]);
        }
    }
}
