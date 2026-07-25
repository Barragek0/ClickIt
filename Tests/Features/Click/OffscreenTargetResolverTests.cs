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

        [TestMethod]
        public void GetRemainingOffscreenPathNodeCount_ReturnsRemainingCount_WhenRuntimeSeamProvidesPlayer()
        {
            PathfindingService pathfindingService = new();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0),
                new PathfindingService.GridPoint(2, 0),
                new PathfindingService.GridPoint(3, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            Entity player = EntityProbeFactory.Create(address: 1, gridX: 1, gridY: 0, type: EntityType.Monster);
            OffscreenTargetResolver resolver = new(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f)),
                pathfindingService,
                new StubOffscreenRuntimeSeam
                {
                    Player = player,
                    PlayerGrid = new Vector2(1f, 0f)
                });

            int remaining = resolver.GetRemainingOffscreenPathNodeCount();

            remaining.Should().Be(2);
        }

        [TestMethod]
        public void GetRemainingOffscreenPathNodeCount_ReturnsZero_WhenRuntimeSeamProvidesNoPlayer()
        {
            PathfindingService pathfindingService = new();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            OffscreenTargetResolver resolver = new(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f)),
                pathfindingService,
                new StubOffscreenRuntimeSeam());

            int remaining = resolver.GetRemainingOffscreenPathNodeCount();

            remaining.Should().Be(0);
        }

        [TestMethod]
        public void GetRemainingOffscreenPathNodeCount_ReturnsZero_WhenRuntimeSeamCannotResolvePlayerGrid()
        {
            PathfindingService pathfindingService = new();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            Entity player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster);
            OffscreenTargetResolver resolver = new(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f)),
                pathfindingService,
                new StubOffscreenRuntimeSeam
                {
                    Player = player,
                    PlayerGrid = Vector2.Zero,
                    CanResolvePlayerGrid = false
                });

            int remaining = resolver.GetRemainingOffscreenPathNodeCount();

            remaining.Should().Be(0);
        }

        [TestMethod]
        public void TryResolveOffscreenTargetScreenPointFromPath_ReturnsTrue_WhenRuntimeSeamProvidesPlayerAndWindow()
        {
            PathfindingService pathfindingService = new();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(2, 0),
                new PathfindingService.GridPoint(4, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            Entity player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster);
            RectangleF window = new(100f, 200f, 1280f, 720f);
            OffscreenTargetResolver resolver = new(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(window),
                pathfindingService,
                new StubOffscreenRuntimeSeam
                {
                    Player = player,
                    PlayerGrid = Vector2.Zero,
                    Window = window
                });

            bool resolved = resolver.TryResolveOffscreenTargetScreenPointFromPath(out Vector2 targetScreen);

            resolved.Should().BeTrue();
            OffscreenTargetResolver.IsInsideWindow(window, targetScreen).Should().BeTrue();
        }

        [TestMethod]
        public void TryResolveOffscreenTargetScreenPointFromPath_ReturnsFalse_WhenPathIsTooShort()
        {
            PathfindingService pathfindingService = new();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            Entity player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster);
            OffscreenTargetResolver resolver = new(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f)),
                pathfindingService,
                new StubOffscreenRuntimeSeam
                {
                    Player = player,
                    PlayerGrid = Vector2.Zero
                });

            bool resolved = resolver.TryResolveOffscreenTargetScreenPointFromPath(out Vector2 targetScreen);

            resolved.Should().BeFalse();
            targetScreen.Should().Be(Vector2.Zero);
        }

        [TestMethod]
        public void TryResolveOffscreenTargetScreenPoint_ReturnsProjectedPoint_WhenRuntimeSeamProvidesFiniteProjection()
        {
            RectangleF window = new(100f, 200f, 1280f, 720f);
            Entity player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster);
            Entity target = EntityProbeFactory.Create(address: 2, gridX: 3, gridY: 2, type: EntityType.Monster);
            OffscreenTargetResolver resolver = new(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(window),
                new PathfindingService(),
                new StubOffscreenRuntimeSeam
                {
                    Player = player,
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(3f, 2f),
                    Window = window,
                    ProjectedPoint = new Vector2(900f, 500f),
                    ProjectWorldToScreen = true
                });

            bool resolved = resolver.TryResolveOffscreenTargetScreenPoint(target, out Vector2 targetScreen);

            resolved.Should().BeTrue();
            targetScreen.Should().Be(new Vector2(900f, 500f));
        }

        [TestMethod]
        public void TryResolveOffscreenTargetScreenPoint_FallsBackToGridDirection_WhenProjectedPointIsNearCorner()
        {
            RectangleF window = new(100f, 200f, 1280f, 720f);
            Entity player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster);
            Entity target = EntityProbeFactory.Create(address: 2, gridX: 5, gridY: 1, type: EntityType.Monster);
            OffscreenTargetResolver resolver = new(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(window),
                new PathfindingService(),
                new StubOffscreenRuntimeSeam
                {
                    Player = player,
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(5f, 1f),
                    Window = window,
                    ProjectedPoint = new Vector2(window.Left + 5f, window.Top + 5f),
                    ProjectWorldToScreen = true
                });

            bool resolved = resolver.TryResolveOffscreenTargetScreenPoint(target, out Vector2 targetScreen);

            resolved.Should().BeTrue();
            targetScreen.Should().NotBe(new Vector2(window.Left + 5f, window.Top + 5f));
            OffscreenTargetResolver.IsInsideWindow(window, targetScreen).Should().BeTrue();
        }

        [TestMethod]
        public void TryResolveOffscreenTargetScreenPoint_ReturnsFalse_WhenProjectionAndGridFallbackBothFail()
        {
            RectangleF window = new(100f, 200f, 1280f, 720f);
            Entity player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster);
            Entity target = EntityProbeFactory.Create(address: 2, gridX: 5, gridY: 1, type: EntityType.Monster);
            OffscreenTargetResolver resolver = new(
                ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(window),
                new PathfindingService(),
                new StubOffscreenRuntimeSeam
                {
                    Player = player,
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = Vector2.Zero,
                    CanResolveTargetGrid = false,
                    Window = window,
                    ProjectWorldToScreen = false
                });

            bool resolved = resolver.TryResolveOffscreenTargetScreenPoint(target, out Vector2 targetScreen);

            resolved.Should().BeFalse();
            targetScreen.Should().Be(Vector2.Zero);
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

        private sealed class StubOffscreenRuntimeSeam : IOffscreenRuntimeSeam
        {
            public Entity? Player { get; init; }

            public Vector2 PlayerGrid { get; init; }

            public bool CanResolvePlayerGrid { get; init; } = true;

            public Vector2 TargetGrid { get; init; }

            public bool CanResolveTargetGrid { get; init; } = true;

            public RectangleF Window { get; init; } = new(100f, 200f, 1280f, 720f);

            public Vector2 ProjectedPoint { get; init; }

            public bool ProjectWorldToScreen { get; init; }

            public Entity? GetPlayer(GameController gameController)
                => Player;

            public bool TryGetGridPosition(Entity entity, out Vector2 gridPosition)
            {
                if (ReferenceEquals(entity, Player))
                {
                    gridPosition = PlayerGrid;
                    return CanResolvePlayerGrid;
                }

                gridPosition = TargetGrid;
                return CanResolveTargetGrid && TargetGrid != default;
            }

            public RectangleF GetWindowRectangle(GameController gameController)
                => Window;

            public bool TryProjectWorldToScreen(GameController gameController, Entity target, out Vector2 targetScreen)
            {
                targetScreen = ProjectedPoint;
                return ProjectWorldToScreen;
            }
        }

    }
}