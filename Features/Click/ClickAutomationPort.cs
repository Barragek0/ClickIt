#nullable enable

namespace ClickIt.Features.Click
{
    public sealed partial class ClickAutomationPort : IClickAutomationService
    {
        /**
        `ClickAutomationPort` is the click-domain entry surface, so keep the constructor eager only for the small set of always-on host dependencies and let the heavier mechanic/runtime owners stay lazy. The lazy members below are intentionally grouped in roughly the same order the runtime reaches them: interaction execution and tick context first, then label/manual-cursor selection, then mechanic/offscreen traversal, and finally Ultimatum handling.
         */
        internal static void ClearThreadLocalStorageForCurrentThread()
        {
            MovementSkillMath.ClearThreadSkillBarEntriesBuffer();
        }

        private readonly ClickItSettings _settings;
        private readonly GameController _gameController;
        private readonly ErrorHandler _errorHandler;
        private readonly AltarService _altarService;
        private readonly WeightCalculator _weightCalculator;
        private readonly AltarChoiceEvaluator _altarChoiceEvaluator;
        private readonly Func<Vector2, string, bool> _pointIsInClickableArea;
        private readonly Func<Vector2, string, bool> _forceRefreshPointIsInClickableArea;
        private readonly InputHandler _inputHandler;
        private readonly ILabelInteractionPort _labelInteractionPort;
        private readonly LabelClickPointResolver _labelClickPointResolver;
        private readonly ShrineService _shrineService;
        private readonly PathfindingService _pathfindingService;
        private readonly Func<bool> _groundItemsVisible;
        private readonly TimeCache<List<LabelOnGround>> _cachedLabels;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly ClickAutomationSupport _support;
        private readonly LockedInteractionDispatcher _lockedInteractionDispatcher;
        private readonly ChestLootSettlementState _chestLootSettlementState = new();
        private readonly ClickRuntimeState _runtimeState = new();
        private readonly MechanicPriorityContextProvider _mechanicPriorityContextProvider;
        private IInteractionExecutionRuntime? _interactionExecutionRuntime;
        private ClickTickContextFactory? _tickContextFactory;
        private AltarAutomationService? _altarAutomationService;
        private ClickDebugPublicationService? _clickDebugPublicationService;
        private GroundLabelEntityAddressProvider? _groundLabelEntityAddressProvider;
        private VisibleLabelSnapshotProvider? _visibleLabelSnapshotProvider;
        private PathfindingLabelSuppressionEvaluator? _pathfindingLabelSuppressionEvaluator;
        private SpecialLabelInteractionHandler? _specialLabelInteractionHandler;
        private ManualCursorLabelInteractionHandler? _manualCursorLabelInteractionHandler;
        private LabelSelectionScanEngine? _labelSelectionScanEngine;
        private ManualCursorLabelSelector? _manualCursorLabelSelector;
        private ManualCursorVisibleMechanicSelector? _manualCursorVisibleMechanicSelector;
        private LabelSelectionCoordinator? _labelSelectionCoordinator;
        private ChestLootSettlementTracker? _chestLootSettlementTracker;
        private LostShipmentTargetSelector? _lostShipmentTargetSelector;
        private SettlersOreTargetSelector? _settlersOreTargetSelector;
        private VisibleMechanicCoordinator? _visibleMechanicCoordinator;
        private OffscreenStickyTargetHandler? _offscreenStickyTargetHandler;
        private OnscreenMechanicPathingBlocker? _onscreenMechanicPathingBlocker;
        private OffscreenTraversalTargetResolver? _offscreenTraversalTargetResolver;
        private OffscreenPathingCoordinator? _offscreenPathingCoordinator;
        private MovementSkillCoordinator? _movementSkillCoordinator;
        private ClickRuntimeEngine? _clickRuntimeEngine;
        private OffscreenTargetResolver? _offscreenTargetResolver;
        private ClickLabelInteractionService? _labelInteractionService;
        private UltimatumAutomationService? _ultimatumAutomationService;

        internal ClickAutomationPort(
            ClickItSettings settings,
            GameController gameController,
            ErrorHandler errorHandler,
            AltarService altarService,
            WeightCalculator weightCalculator,
            AltarChoiceEvaluator altarChoiceEvaluator,
            Func<Vector2, string, bool> pointIsInClickableArea,
            Func<Vector2, string, bool> forceRefreshPointIsInClickableArea,
            InputHandler inputHandler,
            ILabelInteractionPort labelInteractionPort,
            ShrineService shrineService,
            PathfindingService pathfindingService,
            Func<bool> groundItemsVisible,
            TimeCache<List<LabelOnGround>> cachedLabels,
            PerformanceMonitor performanceMonitor,
            Action<string, int>? freezeDebugTelemetrySnapshot)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _altarService = altarService ?? throw new ArgumentNullException(nameof(altarService));
            _weightCalculator = weightCalculator ?? throw new ArgumentNullException(nameof(weightCalculator));
            _altarChoiceEvaluator = altarChoiceEvaluator ?? throw new ArgumentNullException(nameof(altarChoiceEvaluator));
            _pointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));
            _forceRefreshPointIsInClickableArea = forceRefreshPointIsInClickableArea ?? throw new ArgumentNullException(nameof(forceRefreshPointIsInClickableArea));
            _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
            _labelInteractionPort = labelInteractionPort ?? throw new ArgumentNullException(nameof(labelInteractionPort));
            _labelClickPointResolver = new LabelClickPointResolver(settings);
            _shrineService = shrineService ?? throw new ArgumentNullException(nameof(shrineService));
            _pathfindingService = pathfindingService ?? throw new ArgumentNullException(nameof(pathfindingService));
            _groundItemsVisible = groundItemsVisible ?? throw new ArgumentNullException(nameof(groundItemsVisible));
            _cachedLabels = cachedLabels;
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _support = new ClickAutomationSupport(new ClickAutomationSupportDependencies(
                Settings: settings,
                TelemetryStore: new ClickTelemetryStore(settings),
                GetWindowRectangle: () => _gameController.Window.GetWindowRectangleTimeCache,
                GetCursorPosition: static () =>
                {
                    var cursor = Mouse.GetCursorPosition();
                    return new Vector2(cursor.X, cursor.Y);
                },
                PointIsInClickableArea: pointIsInClickableArea,
                LogMessage: message => errorHandler.LogMessage(message),
                FreezeDebugTelemetrySnapshot: freezeDebugTelemetrySnapshot));
            var interactionExecutor = new InteractionExecutor(settings, performanceMonitor, inputHandler.IsClickHotkeyActiveForCurrentInputState, errorHandler);
            _lockedInteractionDispatcher = new LockedInteractionDispatcher(interactionExecutor);
            _mechanicPriorityContextProvider = new MechanicPriorityContextProvider(settings, new MechanicPrioritySnapshotService());
        }

        internal void CancelOffscreenPathingState()
        {
            OffscreenPathing.CancelTraversalState();
        }

        internal void CancelPostChestLootSettlementState()
        {
            ChestLootSettlement.ClearPendingChestOpenConfirmation();
            ChestLootSettlement.ClearPostChestLootSettlementWatch();
        }

        internal bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? allLabels)
            => LabelSelection.TryClickManualUiHoverLabel(allLabels);

        internal IEnumerator ProcessRegularClick()
            => RegularClick.Run();

        internal IEnumerator ProcessAltarClicking()
            => AltarAutomation.ProcessAltarClicking();

        internal bool HasClickableAltars()
            => AltarAutomation.HasClickableAltars();

        internal bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
            => AltarAutomation.ShouldClickAltar(altar, clickEater, clickExarch);

        internal bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            => UltimatumAutomation.TryGetOptionPreview(out previews);

        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
            => UltimatumAutomation.TryHandlePanelUi(windowTopLeft);

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => VisibleLabelSnapshots.GetVisibleOrCachedLabels();

        IEnumerator IClickAutomationService.ProcessRegularClick()
            => ProcessRegularClick();

        bool IClickAutomationService.TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? labels)
            => TryClickManualUiHoverLabel(labels);

        void IClickAutomationService.CancelOffscreenPathingState()
            => CancelOffscreenPathingState();

        void IClickAutomationService.CancelPostChestLootSettlementState()
            => CancelPostChestLootSettlementState();

        bool IClickAutomationService.TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            => TryGetUltimatumOptionPreview(out previews);
    }
}