namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenPathingCoordinatorTests
    {
        [TestMethod]
        public void TryWalkTowardOffscreenTarget_ReturnsFalse_WhenOffscreenTraversalIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.WalkTowardOffscreenLabels.Value = false;
            var pathfindingService = new PathfindingService();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };

            var coordinator = CreateCoordinator(runtimeState, settings: settings, pathfindingService: pathfindingService);

            bool walked = coordinator.TryWalkTowardOffscreenTarget();

            walked.Should().BeFalse();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(42);
            pathfindingService.GetLatestGridPath().Should().HaveCount(2);
        }

        [TestMethod]
        public void TryWalkTowardOffscreenTarget_ReturnsFalse_WhenOnscreenMechanicBlockerWins()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = new ToggleNode(true),
                PrioritizeOnscreenClickableMechanicsOverPathfinding = new ToggleNode(true),
                ClickShrines = new ToggleNode(true),
                ClickLostShipmentCrates = new ToggleNode(true),
                ClickSettlersOre = new ToggleNode(false),
                ClickEaterAltars = new ToggleNode(false),
                ClickExarchAltars = new ToggleNode(false)
            };
            var pathfindingService = new PathfindingService();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");

            ClickDebugSnapshot? published = null;
            var blocker = new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                Settings: settings,
                AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings),
                VisibleMechanics: new StubVisibleMechanicSelectionSource(hasClickableShrine: false, hasLostShipment: true, hasSettlers: false),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(() => true, snapshot => published = snapshot)));
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState));
            var stickyHandler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { }));

            var coordinator = new OffscreenPathingCoordinator(new OffscreenPathingCoordinatorDependencies(
                Settings: settings,
                GameController: null!,
                PathfindingService: pathfindingService,
                OnscreenMechanicPathingBlocker: blocker,
                TraversalTargetResolver: null!,
                StickyTargetHandler: stickyHandler,
                TargetResolver: null!,
                MovementSkills: null!,
                LabelInteraction: null!,
                DebugLog: static _ => { },
                HoldDebugTelemetryAfterSuccess: static _ => { },
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(() => true, _ => { }),
                PointIsInClickableArea: static (_, _) => false));

            bool walked = coordinator.TryWalkTowardOffscreenTarget();

            walked.Should().BeFalse();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
            pathfindingService.GetLatestGridPath().Should().BeEmpty();
            published.Should().NotBeNull();
            published!.Stage.Should().Be("OffscreenPathingBlocked");
        }

        [TestMethod]
        public void TryHandleStickyOffscreenTarget_ReturnsFalse_WhenStickyTargetCannotBeResolved()
        {
            var runtimeState = new ClickRuntimeState();
            var coordinator = CreateCoordinator(
                runtimeState,
                settings: new ClickItSettings { WalkTowardOffscreenLabels = new ToggleNode(false) },
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f)));

            bool handled = coordinator.TryHandleStickyOffscreenTarget(new Vector2(100f, 200f), allLabels: null);

            handled.Should().BeFalse();
        }

        [TestMethod]
        public void TryHandleStickyOffscreenTarget_ReturnsFalse_WhenStickyTargetStateCannotBeRead()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 77
            };
            Entity stickyTarget = CreateEntityWithAddress(77);
            var coordinator = CreateCoordinator(
                runtimeState,
                settings: new ClickItSettings { WalkTowardOffscreenLabels = new ToggleNode(true) },
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(stickyTarget));

            bool handled = coordinator.TryHandleStickyOffscreenTarget(new Vector2(100f, 200f), allLabels: null);

            handled.Should().BeFalse();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
        }

        [TestMethod]
        public void TryHandleStickyOffscreenTarget_ReturnsTrue_WhenStickyTargetResolvesButDirectClickFails()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            Entity stickyTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 42);
            var coordinator = CreateCoordinator(
                runtimeState,
                settings: new ClickItSettings { WalkTowardOffscreenLabels = new ToggleNode(false) },
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(stickyTarget));

            bool handled = coordinator.TryHandleStickyOffscreenTarget(new Vector2(100f, 200f), allLabels: null);

            handled.Should().BeTrue();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(42);
        }

        [TestMethod]
        public void TryWalkTowardOffscreenTarget_ReturnsFalse_WhenRitualBlockerIsActive()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            Entity ritualBlocker = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(
                address: 700,
                path: "Metadata/Terrain/Ritual/RitualBlocker");
            var pathfindingService = new PathfindingService();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            ClickDebugSnapshot? published = null;
            var coordinator = CreateCoordinator(
                runtimeState,
                settings: settings,
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(ritualBlocker),
                pathfindingService: pathfindingService,
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(() => true, snapshot => published = snapshot));

            bool walked = coordinator.TryWalkTowardOffscreenTarget();

            walked.Should().BeFalse();
            pathfindingService.GetLatestGridPath().Should().BeEmpty();
            published.Should().NotBeNull();
            published!.Stage.Should().Be("OffscreenPathingBlockedByRitual");
        }

        [TestMethod]
        public void TryWalkTowardOffscreenTarget_ReturnsFalse_WhenResolverFindsNoOffscreenTarget()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            var pathfindingService = new PathfindingService();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            var coordinator = CreateCoordinator(runtimeState, settings: settings, pathfindingService: pathfindingService);

            bool walked = coordinator.TryWalkTowardOffscreenTarget();

            walked.Should().BeFalse();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(42);
            pathfindingService.GetLatestGridPath().Should().BeEmpty();
        }

        [TestMethod]
        public void CancelTraversalState_ClearsStickyTargetAndLatestPath()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            var pathfindingService = new PathfindingService();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            var coordinator = CreateCoordinator(runtimeState, settings: settings, pathfindingService: pathfindingService);

            coordinator.CancelTraversalState();

            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
            pathfindingService.GetLatestGridPath().Should().BeEmpty();
        }

        [TestMethod]
        public void TryWalkTowardOffscreenTarget_ReturnsFalse_WhenPreferredTargetStateCannotBeRead()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            var pathfindingService = new PathfindingService();
            pathfindingService.RuntimeState.SetLatestPathState(
            [
                new PathfindingService.GridPoint(0, 0),
                new PathfindingService.GridPoint(1, 0)
            ],
                screenPath: null,
                targetPath: "Metadata/TestTarget");
            var coordinator = CreateCoordinator(runtimeState, settings: settings, pathfindingService: pathfindingService);
            Entity preferredTarget = CreateEntityWithAddress(42);

            bool walked = coordinator.TryWalkTowardOffscreenTarget(preferredTarget);

            walked.Should().BeFalse();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
            pathfindingService.GetLatestGridPath().Should().BeEmpty();
        }

        [TestMethod]
        public void TryWalkTowardOffscreenTarget_ReturnsFalse_WhenPreferredTargetIsHidden()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            var runtimeState = new ClickRuntimeState();
            var pathfindingService = new PathfindingService();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 2, isHidden: true);
            var coordinator = CreateCoordinator(runtimeState, settings: settings, pathfindingService: pathfindingService);

            bool walked = coordinator.TryWalkTowardOffscreenTarget(preferredTarget);

            walked.Should().BeFalse();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
            pathfindingService.GetLatestGridPath().Should().BeEmpty();
        }

        [TestMethod]
        public void TryStartTraversal_ReturnsTrue_AndSetsStickyTarget_WhenPreferredTargetIsValid()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            var runtimeState = new ClickRuntimeState();
            var pathfindingService = new PathfindingService();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 12, path: "Metadata/Monsters/TestValid");
            var coordinator = CreateCoordinator(runtimeState, settings: settings, pathfindingService: pathfindingService);

            bool started = InvokeTryStartTraversal(coordinator, preferredTarget, out object? context);

            started.Should().BeTrue();
            context.Should().NotBeNull();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(12);
            ReadTraversalContextPath(context!).Should().Be("Metadata/Monsters/TestValid");
        }

        [TestMethod]
        public void TryBuildTraversalPath_ReturnsTrue_WhenPathfindingFailsWithoutNoRouteBlock()
        {
            var settings = new ClickItSettings
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            var runtimeState = new ClickRuntimeState();
            var pathfindingService = new PathfindingService();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 22, path: "Metadata/Monsters/TestPath");
            var coordinator = new OffscreenPathingCoordinator(new OffscreenPathingCoordinatorDependencies(
                Settings: settings,
                GameController: null!,
                PathfindingService: pathfindingService,
                OnscreenMechanicPathingBlocker: null!,
                TraversalTargetResolver: null!,
                StickyTargetHandler: new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                    GameController: null!,
                    ShrineService: null!,
                    RuntimeState: runtimeState,
                    LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(),
                    ChestLootSettlement: null!,
                    IsClickableInEitherSpace: static (_, _) => false,
                    PathfindingLabelSuppression: new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState)),
                    LabelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                    HoldDebugTelemetryAfterSuccess: static _ => { })),
                TargetResolver: null!,
                MovementSkills: null!,
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(),
                DebugLog: static _ => { },
                HoldDebugTelemetryAfterSuccess: static _ => { },
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(() => true, _ => { }),
                PointIsInClickableArea: static (_, _) => false));
            bool started = InvokeTryStartTraversal(coordinator, preferredTarget, out object? context);

            started.Should().BeTrue();
            bool result = InvokeTryBuildTraversalPath(coordinator, context!, out bool builtPath);

            result.Should().BeTrue();
            builtPath.Should().BeFalse();
            pathfindingService.GetDebugSnapshot().LastFailureReason.Should().Be("GameController/target unavailable.");
        }

        [TestMethod]
        public void IsStickyTarget_ReturnsTrue_WhenEntityMatchesStickyAddress()
        {
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            var coordinator = CreateCoordinator(runtimeState);
            Entity entity = CreateEntityWithAddress(42);

            coordinator.IsStickyTarget(entity).Should().BeTrue();
        }

        [TestMethod]
        public void TryHandleStickyOffscreenTarget_ReturnsTrue_WhenStickyTargetDirectClickSucceeds()
        {
            var runtimeState = new ClickRuntimeState();
            runtimeState.StickyOffscreenTargetAddress = 42;
            Entity stickyTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 42);
            LabelOnGround label = OffscreenStickyTargetGraphShaper.CreateVisibleLabel(stickyTarget);
            var coordinator = CreateCoordinator(
                runtimeState,
                gameController: ExileCoreVisibleObjectBuilder.CreateGameControllerWithEntities(stickyTarget),
                tryResolveClickPosition: static (_, _, _, _) => (true, new Vector2(25f, 40f)),
                executeInteraction: static _ => true,
                labelInteractionPort: new StubLabelInteractionPort(MechanicIds.BasicChests));

            bool handled = coordinator.TryHandleStickyOffscreenTarget(new Vector2(100f, 200f), [label]);

            handled.Should().BeTrue();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
        }

        [TestMethod]
        public void TryResolveTraversalClick_ReturnsFalse_WhenDirectionalWalkClickCannotBeResolved_WithRuntimeSeam()
        {
            ClickItSettings settings = new()
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            ClickRuntimeState runtimeState = new();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 51, path: "Metadata/Monsters/TestProjected");
            OffscreenPathingCoordinator coordinator = CreateCoordinator(
                runtimeState,
                settings: settings,
                pointIsInClickableArea: static (_, _) => false,
                runtimeSeam: new StubOffscreenRuntimeSeam
                {
                    Player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster),
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(4f, 1f),
                    Window = new RectangleF(100f, 200f, 1280f, 720f),
                    ProjectedPoint = new Vector2(980f, 480f),
                    ProjectWorldToScreen = true
                });

            InvokeTryStartTraversal(coordinator, preferredTarget, out object? context).Should().BeTrue();
            bool walked = InvokeTryResolveTraversalClick(coordinator, context!, builtPath: false, out bool resolvedFromPath, out Vector2 targetScreen, out Vector2 walkClick);

            walked.Should().BeFalse();
            resolvedFromPath.Should().BeFalse();
            targetScreen.Should().Be(new Vector2(980f, 480f));
            walkClick.Should().Be(Vector2.Zero);
            runtimeState.StickyOffscreenTargetAddress.Should().Be(51);
        }

        [TestMethod]
        public void TryResolveTraversalClick_ReturnsTrue_WhenProjectedPointResolves_WithRuntimeSeam()
        {
            ClickItSettings settings = new()
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            ClickRuntimeState runtimeState = new();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 52, path: "Metadata/Monsters/TestProjected");
            List<InteractionExecutionRequest> requests = [];
            OffscreenPathingCoordinator coordinator = CreateCoordinator(
                runtimeState,
                settings: settings,
                executeInteraction: request =>
                {
                    requests.Add(request);
                    return true;
                },
                pointIsInClickableArea: static (_, _) => true,
                runtimeSeam: new StubOffscreenRuntimeSeam
                {
                    Player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster),
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(4f, 1f),
                    Window = new RectangleF(100f, 200f, 1280f, 720f),
                    ProjectedPoint = new Vector2(980f, 480f),
                    ProjectWorldToScreen = true
                });

            InvokeTryStartTraversal(coordinator, preferredTarget, out object? context).Should().BeTrue();
            bool walked = InvokeTryResolveTraversalClick(coordinator, context!, builtPath: false, out bool resolvedFromPath, out Vector2 targetScreen, out Vector2 walkClick);

            walked.Should().BeTrue();
            resolvedFromPath.Should().BeFalse();
            targetScreen.Should().Be(new Vector2(980f, 480f));
            walkClick.Should().NotBe(default);
            runtimeState.StickyOffscreenTargetAddress.Should().Be(52);
        }

        [TestMethod]
        public void HandleSuccessfulTraversalMovementSkill_ReturnsTrue_AndPublishesSuccessTelemetry()
        {
            ClickItSettings settings = new()
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            ClickRuntimeState runtimeState = new();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 61, path: "Metadata/Monsters/MovementSkillSuccess");
            List<string> debugLogs = [];
            List<string> heldTelemetry = [];
            PathfindingService pathfindingService = new();
            OffscreenPathingCoordinator coordinator = CreateCoordinator(
                runtimeState,
                settings: settings,
                pathfindingService: pathfindingService,
                debugLog: debugLogs.Add,
                holdDebugTelemetryAfterSuccess: heldTelemetry.Add,
                runtimeSeam: new StubOffscreenRuntimeSeam
                {
                    Player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster),
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(7f, 3f),
                    Window = new RectangleF(100f, 200f, 1280f, 720f)
                });

            InvokeTryStartTraversal(coordinator, preferredTarget, out object? context).Should().BeTrue();

            bool result = InvokeHandleSuccessfulTraversalMovementSkill(
                coordinator,
                context!,
                builtPath: true,
                resolvedFromPath: false,
                targetScreen: new Vector2(820f, 460f),
                movementSkillCastPoint: new Vector2(700f, 420f),
                movementSkillDebug: "Used Shield Charge");

            result.Should().BeTrue();
            heldTelemetry.Should().ContainSingle().Which.Should().Contain("Offscreen traversal movement skill used: Metadata/Monsters/MovementSkillSuccess");
            debugLogs.Should().ContainSingle(log => log.Contains("Used movement skill toward offscreen target: Metadata/Monsters/MovementSkillSuccess", StringComparison.Ordinal));
            OffscreenMovementDebugSnapshot snapshot = pathfindingService.GetLatestOffscreenMovementDebug();
            snapshot.Stage.Should().Be("MovementSkillUsed");
            snapshot.TargetPath.Should().Be("Metadata/Monsters/MovementSkillSuccess");
            snapshot.ClickScreen.Should().Be(new Vector2(700f, 420f));
            snapshot.MovementSkillDebug.Should().Be("Used Shield Charge");
        }

        [TestMethod]
        public void HandleTraversalClickResult_ReturnsFalse_WhenClickIsRejected()
        {
            ClickItSettings settings = new()
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            ClickRuntimeState runtimeState = new();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 62, path: "Metadata/Monsters/ClickRejected");
            List<string> debugLogs = [];
            List<string> heldTelemetry = [];
            PathfindingService pathfindingService = new();
            OffscreenPathingCoordinator coordinator = CreateCoordinator(
                runtimeState,
                settings: settings,
                pathfindingService: pathfindingService,
                debugLog: debugLogs.Add,
                holdDebugTelemetryAfterSuccess: heldTelemetry.Add,
                runtimeSeam: new StubOffscreenRuntimeSeam
                {
                    Player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster),
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(8f, 2f),
                    Window = new RectangleF(100f, 200f, 1280f, 720f)
                });

            InvokeTryStartTraversal(coordinator, preferredTarget, out object? context).Should().BeTrue();

            bool result = InvokeHandleTraversalClickResult(
                coordinator,
                context!,
                builtPath: false,
                resolvedFromPath: false,
                targetScreen: new Vector2(830f, 470f),
                walkClick: new Vector2(760f, 440f),
                movementSkillDebug: "No movement skill",
                clicked: false);

            result.Should().BeFalse();
            heldTelemetry.Should().BeEmpty();
            debugLogs.Should().BeEmpty();
            OffscreenMovementDebugSnapshot snapshot = pathfindingService.GetLatestOffscreenMovementDebug();
            snapshot.Stage.Should().Be("ClickRejected");
            snapshot.TargetPath.Should().Be("Metadata/Monsters/ClickRejected");
            snapshot.ClickScreen.Should().Be(new Vector2(760f, 440f));
            pathfindingService.GetLatestGridPath().Should().BeEmpty();
        }

        [TestMethod]
        public void HandleTraversalClickResult_ReturnsTrue_WhenClickSucceeds()
        {
            ClickItSettings settings = new()
            {
                WalkTowardOffscreenLabels = new ToggleNode(true)
            };
            ClickRuntimeState runtimeState = new();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 63, path: "Metadata/Monsters/ClickSucceeded");
            List<string> debugLogs = [];
            List<string> heldTelemetry = [];
            PathfindingService pathfindingService = new();
            OffscreenStickyTargetHandler stickyHandler = new(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => true,
                PathfindingLabelSuppression: new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState)),
                LabelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                HoldDebugTelemetryAfterSuccess: heldTelemetry.Add));
            OffscreenPathingCoordinator coordinator = new(new OffscreenPathingCoordinatorDependencies(
                Settings: settings,
                GameController: null!,
                PathfindingService: pathfindingService,
                OnscreenMechanicPathingBlocker: null!,
                TraversalTargetResolver: null!,
                StickyTargetHandler: stickyHandler,
                TargetResolver: null!,
                MovementSkills: null!,
                LabelInteraction: null!,
                DebugLog: debugLogs.Add,
                HoldDebugTelemetryAfterSuccess: heldTelemetry.Add,
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(() => true, _ => { }),
                PointIsInClickableArea: static (_, _) => true,
                RuntimeSeam: new StubOffscreenRuntimeSeam
                {
                    Player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster),
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(9f, 5f),
                    Window = new RectangleF(100f, 200f, 1280f, 720f)
                }));

            InvokeTryStartTraversal(coordinator, preferredTarget, out object? context).Should().BeTrue();

            bool result = InvokeHandleTraversalClickResult(
                coordinator,
                context!,
                builtPath: true,
                resolvedFromPath: true,
                targetScreen: new Vector2(840f, 480f),
                walkClick: new Vector2(770f, 450f),
                movementSkillDebug: string.Empty,
                clicked: true);

            result.Should().BeTrue();
            heldTelemetry.Should().ContainSingle().Which.Should().Contain("Offscreen traversal click succeeded: Metadata/Monsters/ClickSucceeded");
            debugLogs.Should().ContainSingle(log => log.Contains("Walking toward offscreen target: Metadata/Monsters/ClickSucceeded", StringComparison.Ordinal));
            OffscreenMovementDebugSnapshot snapshot = pathfindingService.GetLatestOffscreenMovementDebug();
            snapshot.Stage.Should().Be("Clicked");
            snapshot.TargetPath.Should().Be("Metadata/Monsters/ClickSucceeded");
            snapshot.ClickScreen.Should().Be(new Vector2(770f, 450f));
            pathfindingService.GetDebugSnapshot().LastFailureReason.Should().Be("GameController/target unavailable.");
        }

        [TestMethod]
        public void TryWalkTowardOffscreenTarget_ReturnsTrue_WhenMovementSkillIsSkippedAndClickSucceeds()
        {
            ClickItSettings settings = new()
            {
                WalkTowardOffscreenLabels = new ToggleNode(true),
                UseMovementSkillsForOffscreenPathfinding = new ToggleNode(true)
            };
            ClickRuntimeState runtimeState = new();
            Entity preferredTarget = OffscreenStickyTargetGraphShaper.CreateActiveStickyEntity(address: 64, path: "Metadata/Monsters/PublicTraversalSuccess");
            List<string> debugLogs = [];
            List<string> heldTelemetry = [];
            PathfindingService pathfindingService = new();
            GameController interactionController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));
            ClickDebugPublicationService clickDebugPublisher = ClickTestDebugPublisherFactory.Create(() => true, _ => { });
            Func<Vector2, string, bool> pointIsInClickableArea = static (_, _) => true;
            ILabelInteractionPort labelInteractionPort = ClickTestServiceFactory.CreateNoOpLabelInteractionPort();
            ClickLabelInteractionService labelInteraction = ClickTestServiceFactory.CreateLabelInteractionService(
                gameController: interactionController,
                labelInteractionPort: labelInteractionPort,
                executeInteraction: static _ => true,
                isClickableInEitherSpace: pointIsInClickableArea,
                isInsideWindowInEitherSpace: static _ => true);
            OffscreenTargetResolver targetResolver = new(
                interactionController,
                pathfindingService,
                new StubOffscreenRuntimeSeam
                {
                    Player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster),
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(6f, 1f),
                    Window = new RectangleF(100f, 200f, 1280f, 720f),
                    ProjectedPoint = new Vector2(980f, 480f),
                    ProjectWorldToScreen = true
                });
            MovementSkillCoordinator movementSkills = new(new MovementSkillCoordinatorDependencies(
                Settings: settings,
                GameController: interactionController,
                RuntimeState: runtimeState,
                PerformanceMonitor: new PerformanceMonitor(settings),
                GetRemainingOffscreenPathNodeCount: targetResolver.GetRemainingOffscreenPathNodeCount,
                EnsureCursorInsideGameWindowForClick: static _ => true,
                PointIsInClickableArea: pointIsInClickableArea,
                DebugLog: debugLogs.Add));
            OffscreenStickyTargetHandler stickyHandler = new(new OffscreenStickyTargetHandlerDependencies(
                GameController: interactionController,
                ShrineService: new ShrineService(interactionController, (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera))),
                RuntimeState: runtimeState,
                LabelInteraction: labelInteraction,
                ChestLootSettlement: CreateChestLootSettlementTracker(settings, clickDebugPublisher, labelInteraction),
                IsClickableInEitherSpace: pointIsInClickableArea,
                PathfindingLabelSuppression: new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState)),
                LabelInteractionPort: labelInteractionPort,
                HoldDebugTelemetryAfterSuccess: heldTelemetry.Add));
            OffscreenPathingCoordinator coordinator = new(new OffscreenPathingCoordinatorDependencies(
                Settings: settings,
                GameController: null!,
                PathfindingService: pathfindingService,
                OnscreenMechanicPathingBlocker: new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                    Settings: settings,
                    AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings),
                    VisibleMechanics: new StubVisibleMechanicSelectionSource(hasClickableShrine: false, hasLostShipment: false, hasSettlers: false),
                    ClickDebugPublisher: clickDebugPublisher)),
                TraversalTargetResolver: null!,
                StickyTargetHandler: stickyHandler,
                TargetResolver: targetResolver,
                MovementSkills: movementSkills,
                LabelInteraction: labelInteraction,
                DebugLog: debugLogs.Add,
                HoldDebugTelemetryAfterSuccess: heldTelemetry.Add,
                ClickDebugPublisher: clickDebugPublisher,
                PointIsInClickableArea: pointIsInClickableArea,
                RuntimeSeam: new StubOffscreenRuntimeSeam
                {
                    Player = EntityProbeFactory.Create(address: 1, gridX: 0, gridY: 0, type: EntityType.Monster),
                    PlayerGrid = Vector2.Zero,
                    TargetGrid = new Vector2(6f, 1f),
                    Window = new RectangleF(100f, 200f, 1280f, 720f),
                    ProjectedPoint = new Vector2(980f, 480f),
                    ProjectWorldToScreen = true
                }));

            bool walked = coordinator.TryWalkTowardOffscreenTarget(preferredTarget);

            walked.Should().BeTrue();
            heldTelemetry.Should().ContainSingle().Which.Should().Contain("Offscreen traversal click succeeded: Metadata/Monsters/PublicTraversalSuccess");
            debugLogs.Should().Contain(log => log.Contains("Movement skill not used: Skipped: no fresh path available", StringComparison.Ordinal));
            debugLogs.Should().Contain(log => log.Contains("Walking toward offscreen target: Metadata/Monsters/PublicTraversalSuccess", StringComparison.Ordinal));
            OffscreenMovementDebugSnapshot snapshot = pathfindingService.GetLatestOffscreenMovementDebug();
            snapshot.Stage.Should().Be("Clicked");
            snapshot.TargetPath.Should().Be("Metadata/Monsters/PublicTraversalSuccess");
            snapshot.MovementSkillDebug.Should().Contain("no fresh path available");
            runtimeState.StickyOffscreenTargetAddress.Should().Be(64);
        }




        private static OffscreenPathingCoordinator CreateCoordinator(
            ClickRuntimeState runtimeState,
            ClickItSettings? settings = null,
            GameController? gameController = null,
            PathfindingService? pathfindingService = null,
            ClickDebugPublicationService? clickDebugPublisher = null,
            Func<LabelOnGround, Vector2, IReadOnlyList<LabelOnGround>?, Func<Vector2, bool>?, (bool Success, Vector2 ClickPos)>? tryResolveClickPosition = null,
            Func<InteractionExecutionRequest, bool>? executeInteraction = null,
            ILabelInteractionPort? labelInteractionPort = null,
            Func<Vector2, string, bool>? pointIsInClickableArea = null,
            IOffscreenRuntimeSeam? runtimeSeam = null,
            Action<string>? debugLog = null,
            Action<string>? holdDebugTelemetryAfterSuccess = null)
        {
            settings ??= new ClickItSettings();
            gameController ??= ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));
            pathfindingService ??= new PathfindingService();
            clickDebugPublisher ??= ClickTestDebugPublisherFactory.Create(() => true, _ => { });
            pointIsInClickableArea ??= static (_, _) => true;
            debugLog ??= static _ => { };
            holdDebugTelemetryAfterSuccess ??= static _ => { };

            labelInteractionPort ??= ClickTestServiceFactory.CreateNoOpLabelInteractionPort();
            var labelInteraction = ClickTestServiceFactory.CreateLabelInteractionService(
                gameController: gameController,
                labelInteractionPort: labelInteractionPort,
                tryResolveClickPosition: tryResolveClickPosition,
                executeInteraction: executeInteraction ?? (static _ => false),
                isClickableInEitherSpace: pointIsInClickableArea,
                isInsideWindowInEitherSpace: static _ => true);
            var chestLootSettlement = CreateChestLootSettlementTracker(settings, clickDebugPublisher, labelInteraction);
            var targetResolver = new OffscreenTargetResolver(gameController, pathfindingService, runtimeSeam);
            var movementSkills = new MovementSkillCoordinator(new MovementSkillCoordinatorDependencies(
                Settings: settings,
                GameController: gameController,
                RuntimeState: runtimeState,
                PerformanceMonitor: new PerformanceMonitor(settings),
                GetRemainingOffscreenPathNodeCount: targetResolver.GetRemainingOffscreenPathNodeCount,
                EnsureCursorInsideGameWindowForClick: static _ => true,
                PointIsInClickableArea: pointIsInClickableArea,
                DebugLog: debugLog));

            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState));
            var stickyHandler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: gameController,
                ShrineService: new ShrineService(gameController, (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera))),
                RuntimeState: runtimeState,
                LabelInteraction: labelInteraction,
                ChestLootSettlement: chestLootSettlement,
                IsClickableInEitherSpace: pointIsInClickableArea,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: labelInteractionPort,
                HoldDebugTelemetryAfterSuccess: holdDebugTelemetryAfterSuccess));
            var traversalTargetResolver = new OffscreenTraversalTargetResolver(new OffscreenTraversalTargetResolverDependencies(
                Settings: settings,
                GameController: gameController,
                MechanicPriorityContextProvider: new MechanicPriorityContextProvider(settings, new MechanicPrioritySnapshotService()),
                LabelInteraction: labelInteraction,
                LabelInteractionPort: labelInteractionPort,
                VisibleLabelSnapshots: new VisibleLabelSnapshotProvider(gameController, new TimeCache<List<LabelOnGround>>(() => [], 50)),
                IsClickableInEitherSpace: pointIsInClickableArea,
                IsInsideWindowInEitherSpace: static _ => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression));

            return new OffscreenPathingCoordinator(new OffscreenPathingCoordinatorDependencies(
                Settings: settings,
                GameController: gameController,
                PathfindingService: pathfindingService,
                OnscreenMechanicPathingBlocker: new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                    Settings: settings,
                    AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings),
                    VisibleMechanics: new StubVisibleMechanicSelectionSource(hasClickableShrine: false, hasLostShipment: false, hasSettlers: false),
                    ClickDebugPublisher: clickDebugPublisher)),
                TraversalTargetResolver: traversalTargetResolver,
                StickyTargetHandler: stickyHandler,
                TargetResolver: targetResolver,
                MovementSkills: movementSkills,
                LabelInteraction: labelInteraction,
                DebugLog: debugLog,
                HoldDebugTelemetryAfterSuccess: holdDebugTelemetryAfterSuccess,
                ClickDebugPublisher: clickDebugPublisher,
                PointIsInClickableArea: pointIsInClickableArea,
                RuntimeSeam: runtimeSeam));
        }

        private static ChestLootSettlementTracker CreateChestLootSettlementTracker(
            ClickItSettings settings,
            ClickDebugPublicationService clickDebugPublisher,
            ClickLabelInteractionService labelInteraction)
        {
            return new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                Settings: settings,
                State: new ChestLootSettlementState(),
                GroundLabelEntityAddresses: (GroundLabelEntityAddressProvider)RuntimeHelpers.GetUninitializedObject(typeof(GroundLabelEntityAddressProvider)),
                ClickDebugPublisher: clickDebugPublisher,
                LabelInteraction: labelInteraction));
        }

        private sealed class StubVisibleMechanicSelectionSource(bool hasClickableShrine, bool hasLostShipment, bool hasSettlers) : IVisibleMechanicQueryPort
        {
            public Entity? ResolveNextShrineCandidate()
                => null;

            public bool HasClickableShrine()
                => hasClickableShrine;

            public void ResolveVisibleMechanicCandidates(
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate,
                IReadOnlyList<LabelOnGround>? labelsOverride = null)
            {
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

            public VisibleMechanicAvailabilitySnapshot GetVisibleMechanicAvailabilitySnapshot()
                => new(hasLostShipment, hasSettlers);
        }

        private sealed class StubLabelInteractionPort(string mechanicId) : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => default;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => mechanicId;

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => null;

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }

        private static Entity CreateEntityWithAddress(long address)
        {
            Entity entity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            SetMember(entity, "Address", address);
            return entity;
        }

        private static void SetMember(object instance, string memberName, object value)
        {
            Type? currentType = instance.GetType();
            while (currentType != null)
            {
                FieldInfo? backingField = currentType.GetField($"<{memberName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField($"_{char.ToLowerInvariant(memberName[0])}{memberName[1..]}", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (backingField != null)
                {
                    backingField.SetValue(instance, value);
                    return;
                }

                PropertyInfo? property = currentType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                MethodInfo? setMethod = property?.GetSetMethod(nonPublic: true);
                if (setMethod != null)
                {
                    setMethod.Invoke(instance, [value]);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Unable to set member '{memberName}' on {instance.GetType().FullName}.");
        }

        private static bool InvokeTryStartTraversal(OffscreenPathingCoordinator coordinator, Entity preferredTarget, out object? context)
        {
            object?[] args = [preferredTarget, null];
            MethodInfo method = typeof(OffscreenPathingCoordinator).GetMethod("TryStartTraversal", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TryStartTraversal method was not found.");

            bool result = (bool)method.Invoke(coordinator, args)!;
            context = args[1];
            return result;
        }

        private static bool InvokeTryBuildTraversalPath(OffscreenPathingCoordinator coordinator, object context, out bool builtPath)
        {
            object?[] args = [context, null];
            MethodInfo method = typeof(OffscreenPathingCoordinator).GetMethod("TryBuildTraversalPath", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TryBuildTraversalPath method was not found.");

            bool result = (bool)method.Invoke(coordinator, args)!;
            builtPath = (bool)args[1]!;
            return result;
        }

        private static bool InvokeTryResolveTraversalClick(
            OffscreenPathingCoordinator coordinator,
            object context,
            bool builtPath,
            out bool resolvedFromPath,
            out Vector2 targetScreen,
            out Vector2 walkClick)
        {
            object?[] args = [context, builtPath, null, null, null];
            MethodInfo method = typeof(OffscreenPathingCoordinator).GetMethod("TryResolveTraversalClick", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TryResolveTraversalClick method was not found.");

            bool result = (bool)method.Invoke(coordinator, args)!;
            resolvedFromPath = (bool)args[2]!;
            targetScreen = (Vector2)args[3]!;
            walkClick = (Vector2)args[4]!;
            return result;
        }

        private static bool InvokeHandleSuccessfulTraversalMovementSkill(
            OffscreenPathingCoordinator coordinator,
            object context,
            bool builtPath,
            bool resolvedFromPath,
            Vector2 targetScreen,
            Vector2 movementSkillCastPoint,
            string movementSkillDebug)
        {
            object?[] args = [context, builtPath, resolvedFromPath, targetScreen, movementSkillCastPoint, movementSkillDebug];
            MethodInfo method = typeof(OffscreenPathingCoordinator).GetMethod("HandleSuccessfulTraversalMovementSkill", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("HandleSuccessfulTraversalMovementSkill method was not found.");

            return (bool)method.Invoke(coordinator, args)!;
        }

        private static bool InvokeHandleTraversalClickResult(
            OffscreenPathingCoordinator coordinator,
            object context,
            bool builtPath,
            bool resolvedFromPath,
            Vector2 targetScreen,
            Vector2 walkClick,
            string movementSkillDebug,
            bool clicked)
        {
            object?[] args = [context, builtPath, resolvedFromPath, targetScreen, walkClick, movementSkillDebug, clicked];
            MethodInfo method = typeof(OffscreenPathingCoordinator).GetMethod("HandleTraversalClickResult", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("HandleTraversalClickResult method was not found.");

            return (bool)method.Invoke(coordinator, args)!;
        }

        private static string ReadTraversalContextPath(object context)
            => (string?)context.GetType().GetProperty("TargetPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(context)
               ?? string.Empty;

        private sealed class StubOffscreenRuntimeSeam : IOffscreenRuntimeSeam
        {
            public Entity? Player { get; init; }

            public Vector2 PlayerGrid { get; init; }

            public Vector2 TargetGrid { get; init; }

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
                    return true;
                }

                gridPosition = TargetGrid;
                return TargetGrid != default;
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