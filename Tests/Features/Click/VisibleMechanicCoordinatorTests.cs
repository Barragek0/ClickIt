namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class VisibleMechanicCoordinatorTests
    {
        [TestMethod]
        public void TryClickLostShipment_PerformsMechanicClickAtCandidatePosition()
        {
            var settings = new ClickItSettings();
            var runtimeState = new ClickRuntimeState();
            InteractionExecutionRequest? capturedRequest = null;
            var (_, coordinator) = CreateCoordinator(
                settings,
                runtimeState,
                executeInteraction: request =>
                {
                    capturedRequest = request;
                    return true;
                });

            var candidate = CreateLostShipmentCandidate(new Vector2(33f, 44f));

            coordinator.TryClickLostShipmentInteraction(candidate).Should().BeTrue();

            capturedRequest.Should().NotBeNull();
            capturedRequest!.Value.ClickPosition.Should().Be(new Vector2(33f, 44f));
            capturedRequest.Value.UseHoldClick.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickSettlersOre_ReturnsFalse_WhenInteractionFails_AndLeavesLatestDebugAtAttempt()
        {
            var settings = new ClickItSettings();
            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            ClickDebugSnapshot? latestSnapshot = null;

            var (pathfindingService, coordinator) = CreateCoordinator(
                settings,
                runtimeState,
                executeInteraction: static _ => false,
                shouldCaptureClickDebug: static () => true,
                setLatestClickDebug: snapshot => latestSnapshot = snapshot);

            SeedLatestPath(pathfindingService);
            var candidate = CreateSettlersCandidate(MechanicIds.SettlersCopper);

            bool clicked = coordinator.TryClickSettlersOre(candidate);

            clicked.Should().BeFalse();
            runtimeState.StickyOffscreenTargetAddress.Should().Be(42);
            pathfindingService.GetLatestGridPath().Count.Should().Be(2);
            latestSnapshot.Should().NotBeNull();
            latestSnapshot!.Stage.Should().Be("ClickAttempt");
        }

        [TestMethod]
        public void TryClickSettlersOre_UsesHoldClickForVerisium_WhenInteractionSucceeds()
        {
            var settings = new ClickItSettings();
            var runtimeState = new ClickRuntimeState();
            ClickDebugSnapshot? latestSnapshot = null;
            InteractionExecutionRequest? capturedRequest = null;

            var (pathfindingService, coordinator) = CreateCoordinator(
                settings,
                runtimeState,
                executeInteraction: request =>
                {
                    capturedRequest = request;
                    return true;
                },
                shouldCaptureClickDebug: static () => true,
                setLatestClickDebug: snapshot => latestSnapshot = snapshot);

            SeedLatestPath(pathfindingService);
            var candidate = CreateSettlersCandidate(MechanicIds.SettlersVerisium);

            bool clicked = coordinator.TryClickSettlersOre(candidate);

            clicked.Should().BeTrue();
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Value.UseHoldClick.Should().BeTrue();
            capturedRequest.Value.ClickPosition.Should().Be(new Vector2(10f, 20f));
            pathfindingService.GetLatestGridPath().Count.Should().Be(2);
            latestSnapshot.Should().NotBeNull();
            latestSnapshot!.Stage.Should().Be("ClickSuccess");
            latestSnapshot.MechanicId.Should().Be(MechanicIds.SettlersVerisium);
            latestSnapshot.Notes.Should().Contain("Settlers click completed");
        }

        [TestMethod]
        public void HandleSuccessfulMechanicEntityClick_DoesNothing_WhenEntityIsNull()
        {
            var settings = new ClickItSettings();
            settings.WalkTowardOffscreenLabels.Value = true;

            var runtimeState = new ClickRuntimeState();
            var telemetryReasons = new List<string>();

            var (pathfindingService, coordinator) = CreateCoordinator(
                settings,
                runtimeState,
                holdDebugTelemetryAfterSuccess: telemetryReasons.Add);

            SeedLatestPath(pathfindingService);

            coordinator.HandleSuccessfulMechanicEntityClick(null);

            telemetryReasons.Should().BeEmpty();
            pathfindingService.GetLatestGridPath().Count.Should().Be(2);
        }

        [TestMethod]
        public void HandleSuccessfulMechanicEntityClick_ClearsStickyTargetAndPath_ThroughBoundedSeam()
        {
            var settings = new ClickItSettings();
            settings.WalkTowardOffscreenLabels.Value = true;

            var runtimeState = new ClickRuntimeState
            {
                StickyOffscreenTargetAddress = 42
            };
            var telemetryReasons = new List<string>();

            var (pathfindingService, coordinator) = CreateCoordinator(
                settings,
                runtimeState,
                holdDebugTelemetryAfterSuccess: telemetryReasons.Add);

            SeedLatestPath(pathfindingService);

            coordinator.HandleSuccessfulMechanicEntityClick("Metadata/TestMechanic", isStickyTarget: true);

            telemetryReasons.Should().Equal("Successful mechanic click: Metadata/TestMechanic");
            runtimeState.StickyOffscreenTargetAddress.Should().Be(0);
            pathfindingService.GetLatestGridPath().Count.Should().Be(0);
        }

        [TestMethod]
        public void HandleSuccessfulMechanicEntityClick_UsesGenericReason_WhenPathMissing()
        {
            var settings = new ClickItSettings();
            var telemetryReasons = new List<string>();

            var (_, coordinator) = CreateCoordinator(
                settings,
                new ClickRuntimeState(),
                holdDebugTelemetryAfterSuccess: telemetryReasons.Add);

            coordinator.HandleSuccessfulMechanicEntityClick(string.Empty, isStickyTarget: false);

            telemetryReasons.Should().Equal("Successful mechanic click");
        }

        private static LostShipmentCandidate CreateLostShipmentCandidate(Vector2 clickPosition)
        {
            object boxed = default(LostShipmentCandidate);
            SetStructMember(boxed, "Entity", null!);
            SetStructMember(boxed, "ClickPosition", clickPosition);
            SetStructMember(boxed, "Distance", 0f);
            return (LostShipmentCandidate)boxed;
        }

        private static SettlersOreCandidate CreateSettlersCandidate(string mechanicId)
        {
            object boxed = default(SettlersOreCandidate);
            SetStructMember(boxed, "Entity", null!);
            SetStructMember(boxed, "ClickPosition", new Vector2(10f, 20f));
            SetStructMember(boxed, "MechanicId", mechanicId);
            SetStructMember(boxed, "EntityPath", MechanicIds.SettlersVerisiumMarker);
            SetStructMember(boxed, "WorldScreenRaw", new Vector2(15f, 25f));
            SetStructMember(boxed, "WorldScreenAbsolute", new Vector2(115f, 225f));
            SetStructMember(boxed, "Distance", 0f);
            return (SettlersOreCandidate)boxed;
        }

        private static void SeedLatestPath(PathfindingService pathfindingService)
        {
            pathfindingService.RuntimeState.SetLatestPathState(
                new List<PathfindingService.GridPoint>
                {
                    new(0, 0),
                    new(1, 1)
                },
                new List<Vector2>
                {
                    new(5f, 5f),
                    new(10f, 10f)
                },
                "Metadata/SomeTarget");
        }

        private static (PathfindingService PathfindingService, VisibleMechanicCoordinator Coordinator) CreateCoordinator(
            ClickItSettings settings,
            ClickRuntimeState runtimeState,
            Func<InteractionExecutionRequest, bool>? executeInteraction = null,
            Action<string>? debugLog = null,
            Action<string>? holdDebugTelemetryAfterSuccess = null,
            Func<bool>? shouldCaptureClickDebug = null,
            Action<ClickDebugSnapshot>? setLatestClickDebug = null)
        {
            var pathfindingService = new PathfindingService(settings);
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));
            var stickyTargets = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: null!,
                ShrineService: null!,
                RuntimeState: runtimeState,
                LabelInteraction: null!,
                ChestLootSettlement: null!,
                IsClickableInEitherSpace: static (_, _) => true,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: null!,
                HoldDebugTelemetryAfterSuccess: static _ => { }));
            var labelInteraction = ClickTestServiceFactory.CreateLabelInteractionService(
                executeInteraction: executeInteraction ?? (static _ => false),
                isClickableInEitherSpace: static (_, _) => true,
                isInsideWindowInEitherSpace: static _ => true);

            var coordinator = new VisibleMechanicCoordinator(new VisibleMechanicCoordinatorDependencies(
                Settings: settings,
                GameController: null!,
                ShrineService: null!,
                LostShipmentTargets: null!,
                SettlersOreTargets: null!,
                PointIsInClickableArea: static (_, _) => true,
                LabelInteraction: labelInteraction,
                StickyTargets: stickyTargets,
                PathfindingService: pathfindingService,
                DebugLog: debugLog ?? (static _ => { }),
                HoldDebugTelemetryAfterSuccess: holdDebugTelemetryAfterSuccess ?? (static _ => { }),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: shouldCaptureClickDebug,
                    setLatestClickDebug: setLatestClickDebug,
                    isClickableInEitherSpace: static (_, _) => true,
                    isInsideWindowInEitherSpace: static _ => true)));

            return (pathfindingService, coordinator);
        }

        private static void SetStructMember(object instance, string memberName, object value)
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

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Unable to set member '{memberName}' on {instance.GetType().FullName}.");
        }
    }
}