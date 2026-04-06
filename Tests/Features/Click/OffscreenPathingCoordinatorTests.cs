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

            var coordinator = new OffscreenPathingCoordinator(new OffscreenPathingCoordinatorDependencies(
                Settings: settings,
                GameController: null!,
                PathfindingService: null!,
                OnscreenMechanicPathingBlocker: null!,
                TraversalTargetResolver: null!,
                StickyTargetHandler: null!,
                TargetResolver: null!,
                MovementSkills: null!,
                LabelInteraction: null!,
                DebugLog: static _ => { },
                HoldDebugTelemetryAfterSuccess: static _ => { },
                ClickDebugPublisher: null!,
                PointIsInClickableArea: static (_, _) => false));

            bool walked = coordinator.TryWalkTowardOffscreenTarget();

            walked.Should().BeFalse();
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
            var pathfindingService = new PathfindingService(settings);
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
            var runtimeState = new ClickRuntimeState();
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

        private static OffscreenPathingCoordinator CreateCoordinator(
            ClickRuntimeState runtimeState,
            ClickItSettings? settings = null,
            GameController? gameController = null,
            PathfindingService? pathfindingService = null)
        {
            settings ??= new ClickItSettings();
            gameController ??= ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));
            pathfindingService ??= new PathfindingService(settings);

            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState));
            var stickyHandler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: gameController,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => false,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { }));

            return new OffscreenPathingCoordinator(new OffscreenPathingCoordinatorDependencies(
                Settings: settings,
                GameController: gameController,
                PathfindingService: pathfindingService,
                OnscreenMechanicPathingBlocker: new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                    Settings: settings,
                    AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings),
                    VisibleMechanics: new StubVisibleMechanicSelectionSource(hasClickableShrine: false, hasLostShipment: false, hasSettlers: false),
                    ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(() => true, _ => { }))),
                TraversalTargetResolver: null!,
                StickyTargetHandler: stickyHandler,
                TargetResolver: null!,
                MovementSkills: null!,
                LabelInteraction: null!,
                DebugLog: static _ => { },
                HoldDebugTelemetryAfterSuccess: static _ => { },
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(() => true, _ => { }),
                PointIsInClickableArea: static (_, _) => true));
        }

        private sealed class StubVisibleMechanicSelectionSource(bool hasClickableShrine, bool hasLostShipment, bool hasSettlers) : IVisibleMechanicSelectionSource
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

            public (LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers) GetVisibleMechanicCandidates()
                => (null, null);

            public (bool HasLostShipment, bool HasSettlers) GetVisibleMechanicAvailability()
                => (hasLostShipment, hasSettlers);
        }
    }
}