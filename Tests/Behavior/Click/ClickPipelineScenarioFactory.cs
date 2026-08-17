namespace ClickIt.Tests.Behavior.Click
{
    // Scenario harness that composes the REAL click-pipeline components exactly the way production wires them (see ClickAutomationPort.Composition / LabelFilterPort.Composition), so scenario tests exercise the actual geometry, selection, ranking, and click-vs-walk logic against simulated game state (labels with real element rects + entity probes + blocked rectangles).
    internal static class ClickPipelineScenarioFactory
    {
        internal sealed class ScenarioConfig
        {
            public RectangleF Window { get; set; } = new(0f, 0f, 1920f, 1080f);
            public int ClickDistance { get; set; } = 100;
            public bool ClickItems { get; set; } = true;
            public bool ClickStrongboxes { get; set; } = true;
            public bool WalkTowardOffscreenLabels { get; set; } = true;
            public bool CaptureClickDebug { get; set; }
            public int MechanicPriorityDistancePenalty { get; set; }
            public Dictionary<string, int> MechanicPriorityIndexMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public List<RectangleF> BlockedRects { get; set; } = [];

            // Visible mechanics simulated on screen (shrine / settlers ore / lost shipment) and whether the port reports their interaction as succeeding.
            public Entity? ShrineCandidate { get; set; }
            public bool ShrineClickable { get; set; } = true;
            public SettlersOreCandidate? SettlersCandidate { get; set; }
            public bool SettlersClickable { get; set; } = true;
            public LostShipmentCandidate? LostShipmentCandidate { get; set; }
            public bool LostShipmentClickable { get; set; } = true;
        }

        internal sealed class ScenarioLabelElement(RectangleF clientRect) : Element
        {
            public new bool IsValid { get; set; } = true;

            public override RectangleF GetClientRect() => clientRect;
        }

        // A scenario element with a virtual rect; set its Address (IsVisible = address > 0) when it must be treated as visible on screen (e.g. for altar option elements).
        internal static Element CreateLabelElement(RectangleF clientRect)
            => new ScenarioLabelElement(clientRect);

        // Mirrors production mechanic classification for the scenario label set. Reads entity fields via DynamicAccess (production-style) because the base Entity memory-read properties cannot be touched on uninitialized probe objects.
        internal static string? ResolveMechanicId(LabelOnGround label, Entity item, ClickSettings settings)
        {
            string path = DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            if (path.Contains("strongbox", StringComparison.OrdinalIgnoreCase))
                return settings.ClickStrongboxes && !MechanicClassifier.IsLockedStrongbox(item)
                    ? MechanicIds.Strongboxes
                    : null;
            bool isWorldItem = DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Type, out object? rawType)
                && rawType switch
                {
                    EntityType type => type == EntityType.WorldItem,
                    int intType => (EntityType)intType == EntityType.WorldItem,
                    _ => false,
                };
            return isWorldItem && settings.ClickItems ? MechanicIds.Items : null;
        }

        internal static ClickSettings CreateClickSettings(ScenarioConfig config)
            => new()
            {
                ClickDistance = config.ClickDistance,
                ClickItems = config.ClickItems,
                ClickStrongboxes = config.ClickStrongboxes,
                MechanicPriorityIndexMap = config.MechanicPriorityIndexMap,
                IgnoreDistanceMechanicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                IgnoreDistanceWithinByMechanicId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                MechanicPriorityDistancePenalty = config.MechanicPriorityDistancePenalty,
            };

        internal static Func<Vector2, bool> CreateClickableArea(ScenarioConfig config)
            => point =>
            {
                RectangleF win = config.Window;
                if (point.X < win.Left || point.Y < win.Top || point.X > win.Right || point.Y > win.Bottom)
                    return false;
                for (int i = 0; i < config.BlockedRects.Count; i++)
                {
                    if (config.BlockedRects[i].Contains(point))
                        return false;
                }
                return true;
            };

        internal static LabelOnGround CreateLabel(RectangleF rect, Entity item, long address)
        {
            var label = (LabelProbe)RuntimeHelpers.GetUninitializedObject(typeof(LabelProbe));
            SetLabelAddress(label, address);
            label.Label = new ScenarioLabelElement(rect);
            label.ItemOnGround = item;
            return label;
        }

        internal static Entity CreateWorldItem(
            float distance,
            long address,
            float posX = 0f,
            float posY = 0f,
            string path = "Metadata/MiscellaneousObjects/WorldItem")
            => EntityProbeFactory.Create(
                path: path,
                type: EntityType.WorldItem,
                distancePlayer: distance,
                address: address,
                posX: posX,
                posY: posY);

        internal static Entity CreateStrongbox(
            string strongboxPath,
            float distance,
            long address,
            bool locked,
            float posX = 0f,
            float posY = 0f)
        {
            Entity entity = EntityProbeFactory.Create(
                path: strongboxPath,
                type: EntityType.Chest,
                distancePlayer: distance,
                address: address,
                posX: posX,
                posY: posY);
            if (locked)
                EntityProbeFactory.WithComponent<Chest>(entity, new LockedChestProbe { IsLocked = true });
            return entity;
        }

        private static void SetLabelAddress(LabelOnGround label, long address)
        {
            System.Reflection.PropertyInfo addressProperty = typeof(RemoteMemoryObject).GetProperty(
                nameof(RemoteMemoryObject.Address),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
            addressProperty!.SetValue(label, address);
        }

        internal static void SetElementAddress(Element element, long address)
        {
            System.Reflection.PropertyInfo addressProperty = typeof(RemoteMemoryObject).GetProperty(
                nameof(RemoteMemoryObject.Address),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
            addressProperty!.SetValue(element, address);
        }

        // The label element is read via DynamicAccess (the production pattern) because LabelProbe hides LabelOnGround.Label with `new`, so the typed property hits the memory-read base getter.
        internal static Element GetLabelElement(LabelOnGround label)
            => DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawElement) && rawElement is Element element
                ? element
                : throw new InvalidOperationException("Scenario label has no label element.");

        internal sealed class ScenarioHarness
        {
            public ScenarioConfig Config { get; }
            public GameController GameController { get; }
            public ClickItSettings Settings { get; }
            public LabelClickPointResolver ClickPointResolver { get; }
            public LabelSelectionService SelectionService { get; }
            public LabelSelectionScanEngine ScanEngine { get; }
            public InteractionExecutionEngine ExecutionEngine { get; }
            public ClickLabelInteractionService LabelInteraction { get; }
            public Func<Vector2, bool> ClickableArea { get; }

            // The game's hovered UI element, simulated for the UI-hover essence/strongbox preferences; null means nothing is hovered (the preference is inert).
            public Element? HoveredElement { get; set; }

            // Number of times the click interaction was actually executed (the click path reached PerformResolvedLabelInteraction). A walk decision stops the tick before this runs.
            public int InteractionsExecuted { get; private set; }

            // Debug stages published by the click pipeline (populated when CaptureClickDebug is enabled), so tests can assert which path ran (e.g. a walk vs a settle-block).
            public List<ClickDebugSnapshot> ClickDebugSnapshots { get; } = [];

            // The click position of the last executed interaction, so tests can assert the click landed inside the label and outside any blocked rectangle.
            public Vector2? LastClickPosition { get; private set; }

            public ILabelInteractionPort LabelInteractionPort => _labelInteractionPort;

            // The visible-mechanic interaction port the execution engine clicks through; tests read its click counters to affirm which mechanic was actually clicked.
            public ScenarioVisibleMechanicInteractionPort VisibleMechanics { get; }

            // Labels currently on the ground, consumed by the real offscreen traversal resolver.
            public List<LabelOnGround> CurrentLabels { get; set; } = [];

            // Direct eligibility probe (same builder the selection service uses) so tests can assert exactly why a label is or is not a candidate.
            public bool TryBuildCandidate(LabelOnGround label, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason)
                => LabelEligibilityEngine.TryBuildCandidate(
                    label,
                    CreateClickSettings(Config),
                    LabelTargetabilityPolicy.IsEntityTargetableForClick,
                    ResolveMechanicId,
                    out item,
                    out mechanicId,
                    out rejectReason);

            private readonly ScenarioLabelInteractionPort _labelInteractionPort;

            public ScenarioHarness(ScenarioConfig config)
            {
                Config = config;
                GameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(config.Window);
                Settings = new ClickItSettings();
                Settings.WalkTowardOffscreenLabels.Value = config.WalkTowardOffscreenLabels;
                ClickableArea = CreateClickableArea(config);
                ClickPointResolver = new LabelClickPointResolver(Settings);
                _labelInteractionPort = new ScenarioLabelInteractionPort(config);
                VisibleMechanics = new ScenarioVisibleMechanicInteractionPort(config);

                SelectionService = new LabelSelectionService(new LabelSelectionServiceDependencies(
                    GameController: null,
                    CreateClickSettings: _ => CreateClickSettings(config),
                    ShouldCaptureLabelDebug: static () => false,
                    PublishLabelDebugStage: static _ => { },
                    TryBuildLabelCandidate: (LabelOnGround label, ClickSettings settings, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                        LabelEligibilityEngine.TryBuildCandidate(
                            label,
                            settings,
                            LabelTargetabilityPolicy.IsEntityTargetableForClick,
                            ResolveMechanicId,
                            out item,
                            out mechanicId,
                            out rejectReason),
                    GetMechanicIdForLabelCore: _labelInteractionPort.GetMechanicIdForLabel));

                var clickDebugPublisher = ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: () => config.CaptureClickDebug,
                    setLatestClickDebug: ClickDebugSnapshots.Add);

                LabelInteraction = new ClickLabelInteractionService(new ClickLabelInteractionServiceDependencies(
                    Settings,
                    GameController,
                    _labelInteractionPort,
                    TryResolveClickPosition: (label, windowTopLeft, allLabels, isClickableArea) =>
                        (ClickPointResolver.TryCalculateClickPosition(label, windowTopLeft, allLabels, isClickableArea, out Vector2 clickPos), clickPos),
                    IsClickableInEitherSpace: (point, _) => ClickableArea(point),
                    IsInsideWindowInEitherSpace: ClickableArea,
                    ExecuteInteraction: request =>
                    {
                        InteractionsExecuted++;
                        LastClickPosition = request.ClickPosition;
                        return true;
                    },
                    GroundItemsVisible: static () => true,
                    DebugLog: static _ => { }));

                var mechanicPriorityContextProvider = new MechanicPriorityContextProvider(
                    Settings,
                    new MechanicPrioritySnapshotService());

                ScanEngine = new LabelSelectionScanEngine(new LabelSelectionScanEngineDependencies(
                    GameController,
                    _labelInteractionPort,
                    SelectionService,
                    ClickPointResolver,
                    ShouldSuppressLeverClick: static _ => false,
                    ShouldSuppressInactiveUltimatumLabel: static _ => false,
                    ShouldSuppressBlightChestClick: static _ => false,
                    LabelInteraction,
                    mechanicPriorityContextProvider,
                    clickDebugPublisher,
                    DebugLog: static _ => { })
                {
                    IsEssenceClickingEnabled = static () => false,
                    IsStrongboxClickingEnabled = () => config.ClickStrongboxes,
                    ShouldSuppressLockedStrongboxClick = LockedStrongboxLabelSuppression.ShouldSuppress,
                    GetUiHoverElement = () => HoveredElement
                });

                ExecutionEngine = CreateExecutionEngine(config, clickDebugPublisher, _labelInteractionPort);
            }

            public ClickTickContext CreateContext(bool groundItemsVisible, IReadOnlyList<LabelOnGround>? allLabels)
                => CreateContext(groundItemsVisible, allLabels, nextShrine: null);

            public ClickTickContext CreateContext(bool groundItemsVisible, IReadOnlyList<LabelOnGround>? allLabels, Entity? nextShrine)
                => new(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    IsPostChestLootSettleBlocking: false,
                    ChestLootSettleReason: string.Empty,
                    AllLabels: allLabels,
                    NextShrine: nextShrine,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: groundItemsVisible);

            public ClickTickContext CreatePostChestSettleContext(IReadOnlyList<LabelOnGround>? allLabels)
                => new(
                    WindowTopLeft: Vector2.Zero,
                    CursorAbsolute: Vector2.Zero,
                    IsPostChestLootSettleBlocking: true,
                    ChestLootSettleReason: "waiting for chest loot to settle",
                    AllLabels: allLabels,
                    NextShrine: null,
                    MechanicPriorityContext: default,
                    GroundItemsVisible: true);

            public ClickCandidates CreateCandidates(LabelOnGround? nextLabel, string? mechanicId)
                => new(null, null, nextLabel, mechanicId);

            public ClickCandidates CreateCandidates(
                LabelOnGround? nextLabel,
                string? mechanicId,
                SettlersOreCandidate? settlers,
                LostShipmentCandidate? lostShipment)
                => new(lostShipment, settlers, nextLabel, mechanicId);

            private InteractionExecutionEngine CreateExecutionEngine(
                ScenarioConfig config,
                ClickDebugPublicationService clickDebugPublisher,
                ScenarioLabelInteractionPort labelInteractionPort)
            {
                var runtimeState = new ClickRuntimeState();
                var pathfindingService = new PathfindingService();
                var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(
                    new PathfindingLabelSuppressionEvaluatorDependencies(Settings, runtimeState));
                var chestLootSettlement = new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                    Settings,
                    new ChestLootSettlementState(),
                    new GroundLabelEntityAddressProvider(static () => []),
                    clickDebugPublisher,
                    LabelInteraction));
                var offscreenPathing = CreateOffscreenPathingCoordinator(config, runtimeState, pathfindingService, clickDebugPublisher);

                return new InteractionExecutionEngine(new ClickRuntimeEngineDependencies(
                    Telemetry: new ClickTelemetryDependencies(
                        ClickDebugPublisher: clickDebugPublisher,
                        ShouldCaptureClickDebug: () => config.CaptureClickDebug,
                        HoldDebugTelemetryAfterSuccess: static _ => { },
                        DebugLog: static _ => { },
                        RecordAllocationBreakdown: null,
                        RecordBreakdownStage: null),
                    Policy: new ClickPolicyDependencies(
                        Settings: Settings,
                        InputHandler: null!,
                        PointIsInClickableArea: (point, _) => ClickableArea(point),
                        ClickSuccessAnchor: null),
                    Selection: new ClickSelectionDependencies(
                        TickContextFactory: null!,
                        LabelInteractionPort: labelInteractionPort,
                        VisibleMechanics: VisibleMechanics,
                        LabelSelectionScan: null!,
                        SpecialLabelInteraction: CreateSpecialLabelInteractionHandler(),
                        LabelInteraction: LabelInteraction,
                        ChestLootSettlement: chestLootSettlement),
                    Pathing: new ClickPathingDependencies(
                        PathfindingService: pathfindingService,
                        PathfindingLabelSuppression: pathfindingLabelSuppression,
                        OffscreenPathing: offscreenPathing),
                    Mechanics: new ClickMechanicDependencies(
                        AltarAutomation: null!,
                        GetHarvestLabelToClick: null,
                        TryProgressBlightBuilding: null,
                        GetBlightPathfindTarget: null,
                        IsBlightEncounterActive: null)));
            }

            private SpecialLabelInteractionHandler CreateSpecialLabelInteractionHandler()
                => new(new SpecialLabelInteractionHandlerDependencies(
                    Settings,
                    ClickTestServiceFactory.CreateAltarAutomationService(Settings),
                    LabelInteraction,
                    ClickTestServiceFactory.CreateUltimatumAutomationService(Settings),
                    DebugLog: static _ => { }));

            private OffscreenPathingCoordinator CreateOffscreenPathingCoordinator(
                ScenarioConfig config,
                ClickRuntimeState runtimeState,
                PathfindingService pathfindingService,
                ClickDebugPublicationService clickDebugPublisher)
            {
                var labelInteractionPort = new ScenarioLabelInteractionPort(config);
                var labelInteraction = new ClickLabelInteractionService(new ClickLabelInteractionServiceDependencies(
                    Settings,
                    GameController,
                    labelInteractionPort,
                    TryResolveClickPosition: (label, windowTopLeft, allLabels, isClickableArea) =>
                        (ClickPointResolver.TryCalculateClickPosition(label, windowTopLeft, allLabels, isClickableArea, out Vector2 clickPos), clickPos),
                    IsClickableInEitherSpace: (point, _) => ClickableArea(point),
                    IsInsideWindowInEitherSpace: ClickableArea,
                    ExecuteInteraction: static _ => false,
                    GroundItemsVisible: static () => true,
                    DebugLog: static _ => { }));
                var chestLootSettlement = new ChestLootSettlementTracker(new ChestLootSettlementTrackerDependencies(
                    Settings,
                    new ChestLootSettlementState(),
                    new GroundLabelEntityAddressProvider(static () => []),
                    clickDebugPublisher,
                    labelInteraction));
                var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(
                    new PathfindingLabelSuppressionEvaluatorDependencies(Settings, runtimeState));
                var stickyHandler = new OffscreenStickyTargetHandler(new OffscreenStickyTargetHandlerDependencies(
                    GameController,
                    new ShrineService(GameController, (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera))),
                    runtimeState,
                    labelInteraction,
                    chestLootSettlement,
                    IsClickableInEitherSpace: (point, _) => ClickableArea(point),
                    pathfindingLabelSuppression,
                    labelInteractionPort,
                    HoldDebugTelemetryAfterSuccess: static _ => { }));

                var traversalResolver = new OffscreenTraversalTargetResolver(new OffscreenTraversalTargetResolverDependencies(
                    Settings,
                    GameController,
                    new MechanicPriorityContextProvider(Settings, new MechanicPrioritySnapshotService()),
                    labelInteraction,
                    labelInteractionPort,
                    new VisibleLabelSnapshotProvider(new TimeCache<List<LabelOnGround>>(() => CurrentLabels, 50)),
                    IsClickableInEitherSpace: (point, _) => ClickableArea(point),
                    IsInsideWindowInEitherSpace: ClickableArea,
                    pathfindingLabelSuppression,
                    DebugLog: static _ => { },
                    IsLabelFullyOverlapped: ClickPointResolver.IsLabelFullyOverlapped));

                return new OffscreenPathingCoordinator(new OffscreenPathingCoordinatorDependencies(
                    Settings,
                    GameController,
                    pathfindingService,
                    new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                        Settings,
                        ClickTestServiceFactory.CreateAltarAutomationService(Settings),
                        new ScenarioVisibleMechanicSelectionSource(),
                        clickDebugPublisher)),
                    TraversalTargetResolver: traversalResolver,
                    StickyTargetHandler: stickyHandler,
                    MovementSkills: null!,
                    LabelInteraction: labelInteraction,
                    DebugLog: static _ => { },
                    HoldDebugTelemetryAfterSuccess: static _ => { },
                    ClickDebugPublisher: clickDebugPublisher,
                    PointIsInClickableArea: (point, _) => ClickableArea(point)));
            }
        }

        internal sealed class ScenarioLabelInteractionPort(ScenarioConfig config) : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => default;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
            {
                Entity? item = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                    ? rawItem as Entity
                    : null;
                return item != null ? ResolveMechanicId(label!, item, CreateClickSettings(config)) : null;
            }

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }

        internal sealed class ScenarioVisibleMechanicInteractionPort(ScenarioConfig config) : IVisibleMechanicRuntimePort
        {
            public int ShrineClicks { get; private set; }

            public int SettlersClicks { get; private set; }

            public int LostShipmentClicks { get; private set; }

            public bool TryClickSettlersOre(SettlersOreCandidate candidate)
            {
                if (!config.SettlersClickable)
                    return false;
                SettlersClicks++;
                return true;
            }

            public bool TryClickLostShipmentInteraction(LostShipmentCandidate candidate)
            {
                if (!config.LostShipmentClickable)
                    return false;
                LostShipmentClicks++;
                return true;
            }

            public bool TryClickShrineInteraction(Entity shrine)
            {
                if (!config.ShrineClickable)
                    return false;
                ShrineClicks++;
                return true;
            }

            public void HandleSuccessfulMechanicEntityClick(Entity? entity)
            {
            }

            public void HandleSuccessfulShrineClick(Entity? shrine)
            {
            }

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

        internal sealed class ScenarioVisibleMechanicSelectionSource : IVisibleMechanicQueryPort
        {
            public Entity? ResolveNextShrineCandidate() => null;

            public bool HasClickableShrine() => false;

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
