namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class InteractionExecutionEngineTests
    {
        [TestMethod]
        public void Execute_VisibleStopsAfterSettlersSuccess_WithoutTryingLaterMechanics()
        {
            List<string> calls = [];
            Entity shrine = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            InteractionExecutionEngine engine = CreateEngine(
                visibleMechanics: new StubVisibleMechanicInteractionPort(
                    tryClickSettlersOre: _ =>
                    {
                        calls.Add("settlers");
                        return true;
                    },
                    tryClickLostShipment: _ =>
                    {
                        calls.Add("lost-shipment");
                        return true;
                    },
                    tryClickShrine: _ =>
                    {
                        calls.Add("shrine");
                        return true;
                    }));

            ExecutionResult result = engine.Execute(
                CreateContext(groundItemsVisible: true, nextShrine: shrine),
                new ClickCandidates(
                    CreateLostShipmentCandidate(new Vector2(40f, 40f), 12f),
                    CreateSettlersCandidate(new Vector2(10f, 10f), MechanicIds.SettlersVerisium, 8f),
                    null,
                    null),
                CreateDecision(trySettlers: true, tryLostShipment: true, tryShrine: true, groundItemsVisible: true));

            result.ShouldRunPostActions.Should().BeFalse();
            calls.Should().Equal("settlers");
        }

        [TestMethod]
        public void Execute_HiddenFallsBackToLostShipment_WhenSettlersClickFails()
        {
            List<string> calls = [];
            InteractionExecutionEngine engine = CreateEngine(
                visibleMechanics: new StubVisibleMechanicInteractionPort(
                    tryClickSettlersOre: _ =>
                    {
                        calls.Add("settlers");
                        return false;
                    },
                    tryClickLostShipment: _ =>
                    {
                        calls.Add("lost-shipment");
                        return true;
                    }));

            ExecutionResult result = engine.Execute(
                CreateContext(groundItemsVisible: false),
                new ClickCandidates(
                    CreateLostShipmentCandidate(new Vector2(25f, 25f), 15f),
                    CreateSettlersCandidate(new Vector2(10f, 10f), MechanicIds.SettlersVerisium, 8f),
                    null,
                    null),
                CreateDecision(trySettlers: true, tryLostShipment: true, tryShrine: false, groundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            calls.Should().Equal("settlers", "lost-shipment");
        }

        [TestMethod]
        public void Execute_HiddenFallsBackToShrine_WhenEarlierMechanicsDoNotClick()
        {
            List<string> calls = [];
            Entity shrine = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            InteractionExecutionEngine engine = CreateEngine(
                visibleMechanics: new StubVisibleMechanicInteractionPort(
                    tryClickSettlersOre: _ =>
                    {
                        calls.Add("settlers");
                        return false;
                    },
                    tryClickLostShipment: _ =>
                    {
                        calls.Add("lost-shipment");
                        return false;
                    },
                    tryClickShrine: _ =>
                    {
                        calls.Add("shrine");
                        return true;
                    }));

            ExecutionResult result = engine.Execute(
                CreateContext(groundItemsVisible: false, nextShrine: shrine),
                new ClickCandidates(
                    CreateLostShipmentCandidate(new Vector2(25f, 25f), 15f),
                    CreateSettlersCandidate(new Vector2(10f, 10f), MechanicIds.SettlersVerisium, 8f),
                    null,
                    null),
                CreateDecision(trySettlers: true, tryLostShipment: true, tryShrine: true, groundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            calls.Should().Equal("settlers", "lost-shipment", "shrine");
        }

        [TestMethod]
        public void Execute_HiddenPublishesSettlersFallbackSuccessStage_WhenSettlersClickSucceeds()
        {
            List<ClickDebugSnapshot> snapshots = [];
            InteractionExecutionEngine engine = CreateEngine(
                visibleMechanics: new StubVisibleMechanicInteractionPort(tryClickSettlersOre: _ => true),
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                CreateContext(groundItemsVisible: false),
                new ClickCandidates(
                    null,
                    CreateSettlersCandidate(new Vector2(10f, 10f), MechanicIds.SettlersVerisium, 8f),
                    null,
                    null),
                CreateDecision(trySettlers: true, tryLostShipment: false, tryShrine: false, groundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            snapshots.Should().Contain(snapshot => snapshot.Stage == "HiddenSettlersFallback"
                && snapshot.Notes.Contains("Using hidden settlers candidate", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Execute_HiddenPublishesSettlersFallbackSkippedStage_WhenSettlersClickFails()
        {
            List<ClickDebugSnapshot> snapshots = [];
            InteractionExecutionEngine engine = CreateEngine(
                visibleMechanics: new StubVisibleMechanicInteractionPort(tryClickSettlersOre: _ => false),
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                CreateContext(groundItemsVisible: false),
                new ClickCandidates(
                    null,
                    CreateSettlersCandidate(new Vector2(10f, 10f), MechanicIds.SettlersVerisium, 8f),
                    null,
                    null),
                CreateDecision(trySettlers: true, tryLostShipment: false, tryShrine: false, groundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            snapshots.Should().Contain(snapshot => snapshot.Stage == "HiddenSettlersFallbackSkipped"
                && snapshot.Notes.Contains("not targetable/valid", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Execute_VisibleUsesSettlersEntityFallback_WhenLabelClickPointCannotBeResolved()
        {
            List<ClickDebugSnapshot> snapshots = [];
            int settlersClicks = 0;
            InteractionExecutionEngine engine = CreateEngine(
                labelSelection: CreateLabelSelectionCoordinator(),
                visibleMechanics: new StubVisibleMechanicInteractionPort(
                    tryClickSettlersOre: _ =>
                    {
                        settlersClicks++;
                        return true;
                    }),
                labelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(
                    labelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                    tryResolveClickPosition: static (_, _, _, _) => (false, default),
                    groundItemsVisible: static () => true),
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                CreateContext(groundItemsVisible: true),
                new ClickCandidates(
                    null,
                    CreateSettlersCandidate(new Vector2(10f, 10f), MechanicIds.SettlersVerisium, 8f),
                    CreateSelectableLabelWithItem("Metadata/TestLabel"),
                    MechanicIds.SettlersVerisium),
                CreateDecision(trySettlers: false, tryLostShipment: false, tryShrine: false, groundItemsVisible: true));

            result.ShouldRunPostActions.Should().BeFalse();
            settlersClicks.Should().Be(1);
            snapshots.Should().Contain(snapshot => snapshot.Stage == "ClickPointResolveFailed");
            snapshots.Should().Contain(snapshot => snapshot.Stage == "SettlersEntityFallbackAttempt");
            snapshots.Should().Contain(snapshot => snapshot.Stage == "SettlersEntityFallbackSuccess");
        }

        [TestMethod]
        public void Execute_ReturnsStopped_WhenGroundItemsHiddenAndChestSettlementBlocks()
        {
            var logs = new List<string>();
            var snapshots = new List<ClickDebugSnapshot>();
            var engine = CreateEngine(
                debugLog: logs.Add,
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                new ClickTickContext(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    Now: Environment.TickCount64,
                    IsPostChestLootSettleBlocking: true,
                    ChestLootSettleReason: "waiting for chest loot to settle",
                    AllLabels: null,
                    NextShrine: null,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: false),
                new ClickCandidates(null, null, null, null),
                new DecisionResult(
                    TrySettlers: false,
                    TryLostShipment: false,
                    TryShrine: false,
                    GroundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            logs.Should().ContainSingle(message => message.Contains("waiting for chest loot to settle", StringComparison.Ordinal));
            snapshots.Should().Contain(snapshot => snapshot.Stage == "PostChestLootSettleBlocked"
                && snapshot.Notes.Contains("waiting for chest loot to settle", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Execute_HiddenSkipsSettlers_WhenPostChestSettlementBlocksMechanic()
        {
            var calls = new List<string>();
            var snapshots = new List<ClickDebugSnapshot>();
            var engine = CreateEngine(
                visibleMechanics: new StubVisibleMechanicInteractionPort(
                    tryClickSettlersOre: _ =>
                    {
                        calls.Add("settlers");
                        return true;
                    }),
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                new ClickTickContext(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    Now: Environment.TickCount64,
                    IsPostChestLootSettleBlocking: true,
                    ChestLootSettleReason: "waiting for chest loot to settle",
                    AllLabels: null,
                    NextShrine: null,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: false),
                new ClickCandidates(
                    null,
                    CreateSettlersCandidate(new Vector2(12f, 34f), MechanicIds.SettlersVerisium, 5f, entity: null),
                    null,
                    null),
                CreateDecision(trySettlers: true, tryLostShipment: false, tryShrine: false, groundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            calls.Should().BeEmpty();
            snapshots.Should().Contain(snapshot => snapshot.Stage == "PostChestLootSettleBlocked"
                && snapshot.Notes.Contains("nearby-bypass:watcher-inactive", StringComparison.Ordinal));
            snapshots.Should().Contain(snapshot => snapshot.Stage == "HiddenSettlersFallbackSkipped"
                && snapshot.MechanicId == MechanicIds.SettlersVerisium);
        }

        [TestMethod]
        public void Execute_HiddenSkipsLostShipmentCandidate_WhenPostChestSettlementBlocksMechanicSelection()
        {
            var calls = new List<string>();
            var snapshots = new List<ClickDebugSnapshot>();
            var engine = CreateEngine(
                visibleMechanics: new StubVisibleMechanicInteractionPort(
                    tryClickLostShipment: _ =>
                    {
                        calls.Add("lost-shipment");
                        return true;
                    }),
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                new ClickTickContext(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    Now: Environment.TickCount64,
                    IsPostChestLootSettleBlocking: true,
                    ChestLootSettleReason: "waiting for chest loot to settle",
                    AllLabels: null,
                    NextShrine: null,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: false),
                new ClickCandidates(
                    CreateLostShipmentCandidate(new Vector2(56f, 78f), 9f, entity: null),
                    null,
                    null,
                    null),
                CreateDecision(trySettlers: false, tryLostShipment: true, tryShrine: false, groundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            calls.Should().BeEmpty();
            snapshots.Should().Contain(snapshot => snapshot.Stage == "PostChestLootSettleBlocked"
                && snapshot.Notes.Contains("nearby-bypass:watcher-inactive", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Execute_PublishesNoLabelDiagnostics_WhenGroundItemsVisibleButNoLabelCandidateExists()
        {
            var snapshots = new List<ClickDebugSnapshot>();
            var labelInteractionPort = new TrackingLabelInteractionPort();
            var labels = new List<LabelOnGround>
            {
                (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround))
            };
            var engine = CreateEngine(
                labelInteractionPort: labelInteractionPort,
                labelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(
                    labelInteractionPort: labelInteractionPort,
                    groundItemsVisible: static () => true),
                shouldCaptureClickDebug: static () => true,
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                new ClickTickContext(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    Now: Environment.TickCount64,
                    IsPostChestLootSettleBlocking: false,
                    ChestLootSettleReason: string.Empty,
                    AllLabels: labels,
                    NextShrine: null,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: true),
                new ClickCandidates(null, null, null, null),
                new DecisionResult(
                    TrySettlers: false,
                    TryLostShipment: false,
                    TryShrine: false,
                    GroundItemsVisible: true));

            result.ShouldRunPostActions.Should().BeFalse();
            labelInteractionPort.LogSelectionDiagnosticsCalls.Should().Be(1);
            snapshots.Should().Contain(snapshot => snapshot.Stage == "NoLabelCandidate"
                && snapshot.Notes.Contains("visible:0 cached:1 groundVisible:True", StringComparison.Ordinal));
            snapshots.Should().Contain(snapshot => snapshot.Stage == "NoLabelExit");
        }

        [TestMethod]
        public void Execute_PublishesGroundItemsHiddenExit_WhenNoHiddenFallbackIsAvailable()
        {
            var logs = new List<string>();
            var snapshots = new List<ClickDebugSnapshot>();
            var engine = CreateEngine(
                debugLog: logs.Add,
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                new ClickTickContext(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    Now: Environment.TickCount64,
                    IsPostChestLootSettleBlocking: false,
                    ChestLootSettleReason: string.Empty,
                    AllLabels: null,
                    NextShrine: null,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: false),
                new ClickCandidates(null, null, null, null),
                new DecisionResult(
                    TrySettlers: false,
                    TryLostShipment: false,
                    TryShrine: false,
                    GroundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            logs.Should().ContainSingle(message => message.Contains("Ground items not visible, breaking", StringComparison.Ordinal));
            snapshots.Should().Contain(snapshot => snapshot.Stage == "GroundItemsHiddenExit");
        }

        [TestMethod]
        public void Execute_ClicksHiddenShrineFallback_WhenShrineCandidateExists()
        {
            Entity shrine = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            int shrineClicks = 0;
            var engine = CreateEngine(
                visibleMechanics: new StubVisibleMechanicInteractionPort(
                    tryClickShrine: _ =>
                    {
                        shrineClicks++;
                        return true;
                    }));

            ExecutionResult result = engine.Execute(
                new ClickTickContext(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    Now: Environment.TickCount64,
                    IsPostChestLootSettleBlocking: false,
                    ChestLootSettleReason: string.Empty,
                    AllLabels: null,
                    NextShrine: shrine,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: false),
                new ClickCandidates(null, null, null, null),
                new DecisionResult(
                    TrySettlers: false,
                    TryLostShipment: false,
                    TryShrine: true,
                    GroundItemsVisible: false));

            result.ShouldRunPostActions.Should().BeFalse();
            shrineClicks.Should().Be(1);
        }

        [TestMethod]
        public void Execute_StopsVisibleNoLabelPath_WhenChestSettlementStillBlocks()
        {
            var logs = new List<string>();
            var snapshots = new List<ClickDebugSnapshot>();
            var engine = CreateEngine(
                debugLog: logs.Add,
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshots.Add));

            ExecutionResult result = engine.Execute(
                new ClickTickContext(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    Now: Environment.TickCount64,
                    IsPostChestLootSettleBlocking: true,
                    ChestLootSettleReason: "waiting for chest loot to settle",
                    AllLabels: [],
                    NextShrine: null,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: true),
                new ClickCandidates(null, null, null, null),
                new DecisionResult(
                    TrySettlers: false,
                    TryLostShipment: false,
                    TryShrine: false,
                    GroundItemsVisible: true));

            result.ShouldRunPostActions.Should().BeFalse();
            logs.Should().ContainSingle(message => message.Contains("waiting for chest loot to settle", StringComparison.Ordinal));
            snapshots.Should().Contain(snapshot => snapshot.Stage == "PostChestLootSettleBlocked"
                && snapshot.Notes.Contains("waiting for chest loot to settle", StringComparison.Ordinal));
        }

        private static InteractionExecutionEngine CreateEngine(
            ILabelInteractionPort? labelInteractionPort = null,
            IVisibleMechanicRuntimePort? visibleMechanics = null,
            LabelSelectionCoordinator? labelSelection = null,
            ClickLabelInteractionService? labelInteraction = null,
            Func<bool>? shouldCaptureClickDebug = null,
            Action<string>? debugLog = null,
            Action<string>? holdDebugTelemetryAfterSuccess = null,
            ClickDebugPublicationService? clickDebugPublisher = null)
        {
            ClickItSettings settings = new();
            ClickRuntimeState runtimeState = new();
            ILabelInteractionPort resolvedLabelInteractionPort = labelInteractionPort ?? ClickTestServiceFactory.CreateNoOpLabelInteractionPort();
            ClickDebugPublicationService resolvedClickDebugPublisher = clickDebugPublisher ?? ClickTestDebugPublisherFactory.Create();
            ClickLabelInteractionService resolvedLabelInteraction = labelInteraction ?? ClickTestServiceFactory.CreateLabelInteractionService(labelInteractionPort: resolvedLabelInteractionPort);
            PathfindingService pathfindingService = new(settings);
            PathfindingLabelSuppressionEvaluator pathfindingLabelSuppression = new(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState));
            ChestLootSettlementTracker chestLootSettlement = CreateChestLootSettlementTracker(settings, resolvedClickDebugPublisher, resolvedLabelInteraction);
            OffscreenPathingCoordinator offscreenPathing = CreateOffscreenPathingCoordinator(settings, runtimeState, pathfindingService, resolvedClickDebugPublisher);

            return new InteractionExecutionEngine(new InteractionExecutionEngineDependencies(
                Settings: settings,
                LabelInteractionPort: resolvedLabelInteractionPort,
                PathfindingService: pathfindingService,
                VisibleMechanics: visibleMechanics ?? new StubVisibleMechanicInteractionPort(),
                LabelSelection: labelSelection ?? CreateLabelSelectionCoordinator(),
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                ChestLootSettlement: chestLootSettlement,
                OffscreenPathing: offscreenPathing,
                ClickDebugPublisher: resolvedClickDebugPublisher,
                LabelInteraction: resolvedLabelInteraction,
                ShouldCaptureClickDebug: shouldCaptureClickDebug ?? (static () => false),
                HoldDebugTelemetryAfterSuccess: holdDebugTelemetryAfterSuccess ?? (static _ => { }),
                DebugLog: debugLog ?? (static _ => { })));
        }

        private static ClickTickContext CreateContext(bool groundItemsVisible, Entity? nextShrine = null)
        {
            return new ClickTickContext(
                WindowTopLeft: Vector2.Zero,
                CursorAbsolute: Vector2.Zero,
                Now: Environment.TickCount64,
                IsPostChestLootSettleBlocking: false,
                ChestLootSettleReason: string.Empty,
                AllLabels: null,
                NextShrine: nextShrine,
                MechanicPriorityContext: default,
                GroundItemsVisible: groundItemsVisible);
        }

        private static LabelSelectionCoordinator CreateLabelSelectionCoordinator()
        {
            ClickItSettings settings = new();
            SpecialLabelInteractionHandler specialHandler = new(new SpecialLabelInteractionHandlerDependencies(
                Settings: settings,
                AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService(labelInteractionPort: ClickTestServiceFactory.CreateNoOpLabelInteractionPort()),
                UltimatumAutomation: ClickTestServiceFactory.CreateUltimatumAutomationService(settings),
                DebugLog: static _ => { }));

            return new LabelSelectionCoordinator(new LabelSelectionCoordinatorDependencies(
                GameController: ExileCoreOpaqueFactory.CreateOpaqueGameController(),
                ScanEngine: null!,
                ManualCursorLabelSelector: null!,
                ManualCursorVisibleMechanicSelector: null!,
                SpecialLabelInteractionHandler: specialHandler,
                ManualCursorLabelInteractionHandler: null!));
        }

        private static DecisionResult CreateDecision(bool trySettlers, bool tryLostShipment, bool tryShrine, bool groundItemsVisible)
        {
            return new DecisionResult(
                TrySettlers: trySettlers,
                TryLostShipment: tryLostShipment,
                TryShrine: tryShrine,
                GroundItemsVisible: groundItemsVisible);
        }

        private static OffscreenPathingCoordinator CreateOffscreenPathingCoordinator(
            ClickItSettings settings,
            ClickRuntimeState runtimeState,
            PathfindingService pathfindingService,
            ClickDebugPublicationService clickDebugPublisher)
        {
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));
            var labelInteractionPort = ClickTestServiceFactory.CreateNoOpLabelInteractionPort();
            var labelInteraction = ClickTestServiceFactory.CreateLabelInteractionService(
                gameController: gameController,
                labelInteractionPort: labelInteractionPort,
                isClickableInEitherSpace: static (_, _) => true,
                isInsideWindowInEitherSpace: static _ => true);
            var chestLootSettlement = CreateChestLootSettlementTracker(settings, clickDebugPublisher, labelInteraction);
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(settings, runtimeState));
            var stickyHandler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                GameController: gameController,
                ShrineService: new ShrineService(gameController, (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera))),
                RuntimeState: runtimeState,
                LabelInteraction: labelInteraction,
                ChestLootSettlement: chestLootSettlement,
                IsClickableInEitherSpace: static (_, _) => true,
                PathfindingLabelSuppression: pathfindingLabelSuppression,
                LabelInteractionPort: labelInteractionPort,
                HoldDebugTelemetryAfterSuccess: static _ => { }));

            return new OffscreenPathingCoordinator(new OffscreenPathingCoordinatorDependencies(
                Settings: settings,
                GameController: gameController,
                PathfindingService: pathfindingService,
                OnscreenMechanicPathingBlocker: new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                    Settings: settings,
                    AltarAutomation: ClickTestServiceFactory.CreateAltarAutomationService(settings),
                    VisibleMechanics: new StubVisibleMechanicSelectionSource(),
                    ClickDebugPublisher: clickDebugPublisher)),
                TraversalTargetResolver: null!,
                StickyTargetHandler: stickyHandler,
                TargetResolver: null!,
                MovementSkills: null!,
                LabelInteraction: labelInteraction,
                DebugLog: static _ => { },
                HoldDebugTelemetryAfterSuccess: static _ => { },
                ClickDebugPublisher: clickDebugPublisher,
                PointIsInClickableArea: static (_, _) => true));
        }

        private static ChestLootSettlementTracker CreateChestLootSettlementTracker(
            ClickItSettings settings,
            ClickDebugPublicationService clickDebugPublisher,
            ClickLabelInteractionService labelInteraction)
        {
            return new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                Settings: settings,
                State: new ChestLootSettlementState(),
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(static () => []),
                ClickDebugPublisher: clickDebugPublisher,
                LabelInteraction: labelInteraction));
        }

        private static LostShipmentCandidate CreateLostShipmentCandidate(Vector2 clickPosition, float distance, Entity? entity = null)
        {
            object boxed = default(LostShipmentCandidate);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.Entity), entity);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.ClickPosition), clickPosition);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(LostShipmentCandidate.Distance), distance);
            return (LostShipmentCandidate)boxed;
        }

        private static LabelOnGround CreateSelectableLabelWithItem(string path)
        {
            _ = path;
            return ExileCoreVisibleObjectBuilder.CreateSelectableLabel();
        }

        private static SettlersOreCandidate CreateSettlersCandidate(Vector2 clickPosition, string mechanicId, float distance, Entity? entity = null)
        {
            object boxed = default(SettlersOreCandidate);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.Entity), entity);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.ClickPosition), clickPosition);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.MechanicId), mechanicId);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.EntityPath), "Metadata/Test");
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.WorldScreenRaw), Vector2.Zero);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.WorldScreenAbsolute), Vector2.Zero);
            RuntimeMemberAccessor.SetRequiredMember(boxed, nameof(SettlersOreCandidate.Distance), distance);
            return (SettlersOreCandidate)boxed;
        }

        private sealed class TrackingLabelInteractionPort : ILabelInteractionPort
        {
            public int LogSelectionDiagnosticsCalls { get; private set; }

            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => default;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
                LogSelectionDiagnosticsCalls++;
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => null;

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => null;

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }

        private sealed class StubVisibleMechanicInteractionPort(
            Func<SettlersOreCandidate, bool>? tryClickSettlersOre = null,
            Func<LostShipmentCandidate, bool>? tryClickLostShipment = null,
            Func<Entity, bool>? tryClickShrine = null) : IVisibleMechanicRuntimePort
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
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

            public bool TryClickSettlersOre(SettlersOreCandidate candidate)
                => (tryClickSettlersOre ?? (_ => false))(candidate);

            public bool TryClickLostShipmentInteraction(LostShipmentCandidate candidate)
                => (tryClickLostShipment ?? (_ => false))(candidate);

            public bool TryClickShrineInteraction(Entity shrine)
                => (tryClickShrine ?? (_ => false))(shrine);

            public void HandleSuccessfulMechanicEntityClick(Entity? entity)
            {
            }

            public void HandleSuccessfulShrineClick(Entity? shrine)
            {
            }
        }

        private sealed class StubVisibleMechanicSelectionSource : IVisibleMechanicQueryPort
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
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                lostShipmentCandidate = null;
                settlersOreCandidate = null;
            }
        }
    }
}