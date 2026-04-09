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
        public void TryClickSettlersOre_AllowsInteraction_WhenCandidateEntityIsNull()
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

            bool clicked = coordinator.TryClickSettlersOre(CreateSettlersCandidate(MechanicIds.SettlersCopper));

            clicked.Should().BeTrue();
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Value.ClickPosition.Should().Be(new Vector2(10f, 20f));
        }

        [TestMethod]
        public void TryClickLostShipmentInteraction_ReturnsFalse_WhenInteractionFails()
        {
            var settings = new ClickItSettings();
            var runtimeState = new ClickRuntimeState();
            Entity entity = ExileCoreOpaqueFactory.CreateOpaqueEntity();

            var (_, coordinator) = CreateCoordinator(
                settings,
                runtimeState,
                executeInteraction: static _ => false);

            bool clicked = coordinator.TryClickLostShipmentInteraction(CreateLostShipmentCandidate(new Vector2(33f, 44f), entity));

            clicked.Should().BeFalse();
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

        [TestMethod]
        public void ResolveHiddenFallbackCandidates_ReusesCachedSnapshot_WhenCacheWindowAndLabelCountMatch()
        {
            var settings = new ClickItSettings();
            settings.ClickLostShipmentCrates.Value = false;
            settings.ClickSettlersOre.Value = false;

            var (_, coordinator) = CreateCoordinator(settings, new ClickRuntimeState());
            LostShipmentCandidate expectedLostShipment = CreateLostShipmentCandidate(new Vector2(12f, 34f));
            SettlersOreCandidate expectedSettlers = CreateSettlersCandidate(MechanicIds.SettlersCopper);
            SeedHiddenFallbackCache(coordinator, now: Environment.TickCount64, labelCount: 0, expectedLostShipment, expectedSettlers);

            coordinator.ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate);

            lostShipmentCandidate.Should().NotBeNull();
            lostShipmentCandidate!.Value.ClickPosition.Should().Be(new Vector2(12f, 34f));
            settlersOreCandidate.Should().NotBeNull();
            settlersOreCandidate!.Value.MechanicId.Should().Be(MechanicIds.SettlersCopper);
        }

        [TestMethod]
        public void ResolveHiddenFallbackCandidates_DoesNotReuseCachedSnapshot_WhenLabelCountChanges()
        {
            var settings = new ClickItSettings();
            settings.ClickLostShipmentCrates.Value = false;
            settings.ClickSettlersOre.Value = false;

            var (_, coordinator) = CreateCoordinator(settings, new ClickRuntimeState());
            SeedHiddenFallbackCache(
                coordinator,
                now: Environment.TickCount64,
                labelCount: 1,
                CreateLostShipmentCandidate(new Vector2(12f, 34f)),
                CreateSettlersCandidate(MechanicIds.SettlersCopper));

            coordinator.ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate);

            lostShipmentCandidate.Should().BeNull();
            settlersOreCandidate.Should().BeNull();
        }

        [TestMethod]
        public void ResolveVisibleMechanicCandidates_DoesNotReuseUnusableCachedCandidates()
        {
            var settings = new ClickItSettings();
            settings.ClickLostShipmentCrates.Value = false;
            settings.ClickSettlersOre.Value = false;

            var (_, coordinator) = CreateCoordinator(settings, new ClickRuntimeState());
            SeedVisibleCache(
                coordinator,
                now: Environment.TickCount64,
                labelCount: 0,
                CreateLostShipmentCandidate(new Vector2(20f, 30f)),
                CreateSettlersCandidate(MechanicIds.SettlersCopper));

            coordinator.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate, labelsOverride: []);

            lostShipmentCandidate.Should().BeNull();
            settlersOreCandidate.Should().BeNull();
        }

        [TestMethod]
        public void VisibleMechanicRuntimePort_GetVisibleMechanicSelectionSnapshotForLabels_ForwardsOverride()
        {
            List<LabelOnGround> labels = [ExileCoreOpaqueFactory.CreateOpaqueLabel()];
            LostShipmentCandidate expectedLostShipment = CreateLostShipmentCandidate(new Vector2(12f, 34f));
            SettlersOreCandidate expectedSettlers = CreateSettlersCandidate(MechanicIds.SettlersCopper);
            IReadOnlyList<LabelOnGround>? capturedLabels = null;
            IVisibleMechanicRuntimePort port = new StubVisibleMechanicRuntimePort(
                resolveVisible: overrideLabels =>
                {
                    capturedLabels = overrideLabels;
                    return new VisibleMechanicSelectionSnapshot(expectedLostShipment, expectedSettlers);
                });

            VisibleMechanicSelectionSnapshot snapshot = port.GetVisibleMechanicSelectionSnapshotForLabels(labels);

            capturedLabels.Should().BeSameAs(labels);
            snapshot.LostShipment.Should().Be(expectedLostShipment);
            snapshot.Settlers.Should().Be(expectedSettlers);
        }

        [TestMethod]
        public void VisibleMechanicRuntimePort_GetVisibleMechanicSelectionSnapshot_UsesNullOverride()
        {
            IReadOnlyList<LabelOnGround>? capturedLabels = [ExileCoreOpaqueFactory.CreateOpaqueLabel()];
            IVisibleMechanicRuntimePort port = new StubVisibleMechanicRuntimePort(
                resolveVisible: overrideLabels =>
                {
                    capturedLabels = overrideLabels;
                    return new VisibleMechanicSelectionSnapshot(null, null);
                });

            VisibleMechanicSelectionSnapshot snapshot = port.GetVisibleMechanicSelectionSnapshot();

            capturedLabels.Should().BeNull();
            snapshot.HasLostShipment.Should().BeFalse();
            snapshot.HasSettlers.Should().BeFalse();
        }

        [TestMethod]
        public void VisibleMechanicRuntimePort_GetHiddenFallbackSelectionSnapshot_ReturnsHiddenCandidates()
        {
            LostShipmentCandidate expectedLostShipment = CreateLostShipmentCandidate(new Vector2(44f, 55f));
            SettlersOreCandidate expectedSettlers = CreateSettlersCandidate(MechanicIds.SettlersVerisium);
            IVisibleMechanicRuntimePort port = new StubVisibleMechanicRuntimePort(
                resolveHidden: () => new VisibleMechanicSelectionSnapshot(expectedLostShipment, expectedSettlers));

            VisibleMechanicSelectionSnapshot snapshot = port.GetHiddenFallbackSelectionSnapshot();

            snapshot.LostShipment.Should().Be(expectedLostShipment);
            snapshot.Settlers.Should().Be(expectedSettlers);
        }

        [TestMethod]
        public void VisibleMechanicRuntimePort_GetVisibleMechanicAvailabilitySnapshot_ProjectsAvailabilityFlags()
        {
            IVisibleMechanicRuntimePort port = new StubVisibleMechanicRuntimePort(
                resolveVisible: _ => new VisibleMechanicSelectionSnapshot(
                    CreateLostShipmentCandidate(new Vector2(10f, 20f)),
                    null));

            VisibleMechanicAvailabilitySnapshot snapshot = port.GetVisibleMechanicAvailabilitySnapshot();

            snapshot.HasLostShipment.Should().BeTrue();
            snapshot.HasSettlers.Should().BeFalse();
        }

        private static LostShipmentCandidate CreateLostShipmentCandidate(Vector2 clickPosition, Entity? entity = null)
        {
            object boxed = default(LostShipmentCandidate);
            SetStructMember(boxed, "Entity", entity!);
            SetStructMember(boxed, "ClickPosition", clickPosition);
            SetStructMember(boxed, "Distance", 0f);
            return (LostShipmentCandidate)boxed;
        }

        private static SettlersOreCandidate CreateSettlersCandidate(string mechanicId, Entity? entity = null)
        {
            object boxed = default(SettlersOreCandidate);
            SetStructMember(boxed, "Entity", entity!);
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
                LostShipmentTargets: new LostShipmentTargetSelector(new LostShipmentTargetSelectorDependencies(
                    Settings: settings,
                    GameController: null!,
                    DebugLog: debugLog ?? (static _ => { }),
                    IsInsideWindowInEitherSpace: static _ => true,
                    IsClickableInEitherSpace: static (_, _) => true)),
                SettlersOreTargets: new SettlersOreTargetSelector(new SettlersOreTargetSelectorDependencies(
                    Settings: settings,
                    GameController: null!,
                    ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                        shouldCaptureClickDebug: shouldCaptureClickDebug,
                        setLatestClickDebug: setLatestClickDebug,
                        isClickableInEitherSpace: static (_, _) => true,
                        isInsideWindowInEitherSpace: static _ => true),
                    DebugLog: debugLog ?? (static _ => { }),
                    IsInsideWindowInEitherSpace: static _ => true,
                    IsClickableInEitherSpace: static (_, _) => true,
                    GroundLabelEntityAddresses: (GroundLabelEntityAddressProvider)RuntimeHelpers.GetUninitializedObject(typeof(GroundLabelEntityAddressProvider)))),
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

        private static void SeedHiddenFallbackCache(
            VisibleMechanicCoordinator coordinator,
            long now,
            int labelCount,
            LostShipmentCandidate? lostShipmentCandidate,
            SettlersOreCandidate? settlersOreCandidate)
        {
            VisibleMechanicCacheState cacheState = GetCacheState(coordinator);
            cacheState.StoreHiddenFallbackCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
        }

        private static void SeedVisibleCache(
            VisibleMechanicCoordinator coordinator,
            long now,
            int labelCount,
            LostShipmentCandidate? lostShipmentCandidate,
            SettlersOreCandidate? settlersOreCandidate)
        {
            VisibleMechanicCacheState cacheState = GetCacheState(coordinator);
            cacheState.StoreVisibleCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
        }

        private static VisibleMechanicCacheState GetCacheState(VisibleMechanicCoordinator coordinator)
            => (VisibleMechanicCacheState)typeof(VisibleMechanicCoordinator)
                .GetField("cacheState", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(coordinator)!;

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

        private sealed class StubVisibleMechanicRuntimePort(
            Func<IReadOnlyList<LabelOnGround>?, VisibleMechanicSelectionSnapshot>? resolveVisible = null,
            Func<VisibleMechanicSelectionSnapshot>? resolveHidden = null) : IVisibleMechanicRuntimePort
        {
            public Entity? ResolveNextShrineCandidate()
                => null;

            public bool HasClickableShrine()
                => false;

            public void ResolveVisibleMechanicCandidates(
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate,
                IReadOnlyList<LabelOnGround>? labelsOverride = null)
            {
                VisibleMechanicSelectionSnapshot snapshot = (resolveVisible ?? (_ => new VisibleMechanicSelectionSnapshot(null, null)))(labelsOverride);
                lostShipmentCandidate = snapshot.LostShipment;
                settlersOreCandidate = snapshot.Settlers;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                VisibleMechanicSelectionSnapshot snapshot = (resolveHidden ?? (() => new VisibleMechanicSelectionSnapshot(null, null)))();
                lostShipmentCandidate = snapshot.LostShipment;
                settlersOreCandidate = snapshot.Settlers;
            }

            public bool TryClickSettlersOre(SettlersOreCandidate candidate)
                => false;

            public bool TryClickLostShipmentInteraction(LostShipmentCandidate candidate)
                => false;

            public bool TryClickShrineInteraction(Entity shrine)
                => false;

            public void HandleSuccessfulMechanicEntityClick(Entity? entity)
            {
            }

            public void HandleSuccessfulShrineClick(Entity? shrine)
            {
            }
        }
    }
}