namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenTraversalTargetResolverTests
    {
        [TestMethod]
        public void ResolveNearestOffscreenWalkTarget_ReturnsNull_AndDoesNotRefreshPriorities_WhenNoCandidateSourcesExist()
        {
            var settings = new ClickItSettings();
            var snapshotProvider = new CountingMechanicPrioritySnapshotProvider();
            var resolver = CreateResolver(settings, snapshotProvider);

            Entity? result = resolver.ResolveNearestOffscreenWalkTarget();

            result.Should().BeNull();
            snapshotProvider.RefreshCalls.Should().Be(0);
        }

        [TestMethod]
        public void ResolveNearestOffscreenWalkTarget_ReturnsNull_WhenShrinesEnabledButEntityGraphIsUnavailable()
        {
            var settings = new ClickItSettings();
            settings.ClickShrines.Value = true;
            var snapshotProvider = new CountingMechanicPrioritySnapshotProvider();
            var resolver = CreateResolver(settings, snapshotProvider);

            Entity? result = resolver.ResolveNearestOffscreenWalkTarget();

            result.Should().BeNull();
            snapshotProvider.RefreshCalls.Should().Be(0);
        }

        [TestMethod]
        public void ResolveNearestOffscreenWalkTarget_ReturnsNull_WhenAreaTransitionsEnabledButEntityGraphIsUnavailable()
        {
            var settings = new ClickItSettings();
            settings.ClickAreaTransitions.Value = true;
            var snapshotProvider = new CountingMechanicPrioritySnapshotProvider();
            var resolver = CreateResolver(settings, snapshotProvider);

            Entity? result = resolver.ResolveNearestOffscreenWalkTarget();

            result.Should().BeNull();
            snapshotProvider.RefreshCalls.Should().Be(0);
        }

        [TestMethod]
        public void ResolveNearestOffscreenWalkTarget_ReturnsNull_WhenEldritchAltarsEnabledButEntityGraphIsUnavailable()
        {
            var settings = new ClickItSettings();
            settings.ClickExarchAltars.Value = true;
            var snapshotProvider = new CountingMechanicPrioritySnapshotProvider();
            var resolver = CreateResolver(settings, snapshotProvider);

            Entity? result = resolver.ResolveNearestOffscreenWalkTarget();

            result.Should().BeNull();
            snapshotProvider.RefreshCalls.Should().Be(0);
        }

        private static OffscreenTraversalTargetResolver CreateResolver(ClickItSettings settings, CountingMechanicPrioritySnapshotProvider snapshotProvider)
        {
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));
            var runtimeState = new ClickRuntimeState();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState));

            return new OffscreenTraversalTargetResolver(new OffscreenTraversalTargetResolverDependencies(
                Settings: settings,
                GameController: gameController,
                MechanicPriorityContextProvider: new MechanicPriorityContextProvider(settings, snapshotProvider),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(gameController: gameController),
                LabelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                VisibleLabelSnapshots: new VisibleLabelSnapshotProvider(gameController, new TimeCache<List<LabelOnGround>>(() => [], 50)),
                IsClickableInEitherSpace: static (_, _) => false,
                IsInsideWindowInEitherSpace: static _ => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression));
        }

        private sealed class CountingMechanicPrioritySnapshotProvider : IMechanicPrioritySnapshotProvider
        {
            public int RefreshCalls { get; private set; }

            public MechanicPrioritySnapshot Snapshot { get; private set; } = new(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

            public MechanicPrioritySnapshot Refresh(
                IReadOnlyList<string> mechanicPriorities,
                IReadOnlyCollection<string> ignoreDistanceIds,
                IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
            {
                RefreshCalls++;
                Snapshot = new MechanicPrioritySnapshot(
                    mechanicPriorities.Select((mechanicId, index) => new KeyValuePair<string, int>(mechanicId, index))
                        .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(ignoreDistanceIds, StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, int>(ignoreDistanceWithinByMechanicId, StringComparer.OrdinalIgnoreCase));
                return Snapshot;
            }
        }
    }
}