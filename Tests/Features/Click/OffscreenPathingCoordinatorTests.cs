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


        private static OffscreenPathingCoordinator CreateCoordinator(
            ClickRuntimeState runtimeState,
            ClickItSettings? settings = null,
            GameController? gameController = null,
            PathfindingService? pathfindingService = null,
            ClickDebugPublicationService? clickDebugPublisher = null,
            Func<InteractionExecutionRequest, bool>? executeInteraction = null,
            Func<Vector2, string, bool>? pointIsInClickableArea = null,
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

            var labelInteractionPort = ClickTestServiceFactory.CreateNoOpLabelInteractionPort();
            var labelInteraction = ClickTestServiceFactory.CreateLabelInteractionService(
                gameController: gameController,
                labelInteractionPort: labelInteractionPort,
                executeInteraction: executeInteraction ?? (static _ => false),
                isClickableInEitherSpace: pointIsInClickableArea,
                isInsideWindowInEitherSpace: static _ => true);
            var chestLootSettlement = CreateChestLootSettlementTracker(settings, clickDebugPublisher, labelInteraction);
            var targetResolver = new OffscreenTargetResolver(gameController, pathfindingService);
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
                PointIsInClickableArea: pointIsInClickableArea));
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

    }
}