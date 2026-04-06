namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class InteractionExecutionEngineTests
    {
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
                    tryClickShrine: _ => shrineClicks++));

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
            IVisibleMechanicManualInteractionPort? visibleMechanics = null,
            ClickLabelInteractionService? labelInteraction = null,
            Func<bool>? shouldCaptureClickDebug = null,
            Action<string>? debugLog = null,
            ClickDebugPublicationService? clickDebugPublisher = null)
        {
            ILabelInteractionPort resolvedLabelInteractionPort = labelInteractionPort ?? ClickTestServiceFactory.CreateNoOpLabelInteractionPort();

            return new InteractionExecutionEngine(new InteractionExecutionEngineDependencies(
                Settings: new ClickItSettings(),
                LabelInteractionPort: resolvedLabelInteractionPort,
                PathfindingService: null!,
                VisibleMechanics: visibleMechanics ?? new StubVisibleMechanicInteractionPort(),
                LabelSelection: null!,
                PathfindingLabelSuppression: null!,
                ChestLootSettlement: CreateChestLootSettlementTracker(),
                OffscreenPathing: null!,
                ClickDebugPublisher: clickDebugPublisher ?? ClickTestDebugPublisherFactory.Create(),
                LabelInteraction: labelInteraction ?? ClickTestServiceFactory.CreateLabelInteractionService(labelInteractionPort: resolvedLabelInteractionPort),
                ShouldCaptureClickDebug: shouldCaptureClickDebug ?? (static () => false),
                HoldDebugTelemetryAfterSuccess: static _ => { },
                DebugLog: debugLog ?? (static _ => { })));
        }

        private static ChestLootSettlementTracker CreateChestLootSettlementTracker()
        {
            return new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                Settings: new ClickItSettings(),
                State: new ChestLootSettlementState(),
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(static () => []),
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
                LabelInteraction: ClickTestServiceFactory.CreateLabelInteractionService()));
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
            Action<LostShipmentCandidate>? tryClickLostShipment = null,
            Action<Entity>? tryClickShrine = null) : IVisibleMechanicManualInteractionPort
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

            public (LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers) GetVisibleMechanicCandidates()
                => (null, null);

            public (bool HasLostShipment, bool HasSettlers) GetVisibleMechanicAvailability()
                => (false, false);

            public bool TryClickSettlersOre(SettlersOreCandidate candidate)
                => (tryClickSettlersOre ?? (_ => false))(candidate);

            public void TryClickLostShipment(LostShipmentCandidate candidate)
            {
                (tryClickLostShipment ?? (_ => { }))(candidate);
            }

            public void TryClickShrine(Entity shrine)
            {
                (tryClickShrine ?? (_ => { }))(shrine);
            }

            public void HandleSuccessfulMechanicEntityClick(Entity? entity)
            {
            }

            public void HandleSuccessfulShrineClick(Entity? shrine)
            {
            }
        }
    }
}