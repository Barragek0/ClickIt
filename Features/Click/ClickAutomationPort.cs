#nullable enable

namespace ClickIt.Features.Click
{
    public sealed partial class ClickAutomationPort : IClickAutomationService
    {
        internal static void ClearThreadLocalStorageForCurrentThread()
        {
            MovementSkillMath.ClearThreadSkillBarEntriesBuffer();
        }

        private readonly ClickItSettings _settings;
        private readonly GameController _gameController;
        private readonly ErrorHandler _errorHandler;
        private readonly AltarService _altarService;
        private readonly WeightCalculator _weightCalculator;
        private readonly AltarDisplayRenderer _altarDisplayRenderer;
        private readonly Func<Vector2, string, bool> _pointIsInClickableArea;
        private readonly InputHandler _inputHandler;
        private readonly ILabelInteractionPort _labelInteractionPort;
        private readonly ShrineService _shrineService;
        private readonly PathfindingService _pathfindingService;
        private readonly Func<bool> _groundItemsVisible;
        private readonly TimeCache<List<LabelOnGround>> _cachedLabels;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly Action<string, int>? _freezeDebugTelemetrySnapshot;
        private readonly ClickTelemetryStore _clickTelemetryStore;
        private readonly IClickSafetyPolicy _clickSafetyPolicy = new ClickSafetyPolicy();
        private readonly LockedInteractionDispatcher _lockedInteractionDispatcher;
        private readonly ChestLootSettlementState _chestLootSettlementState = new();
        private readonly ClickRuntimeState _runtimeState = new();
        private readonly MechanicPriorityContextProvider _mechanicPriorityContextProvider;
        private IInteractionExecutionRuntime? _interactionExecutionRuntime;
        private ClickTickContextFactory? _tickContextFactory;
        private AltarAutomationService? _altarAutomationService;
        private ClickDebugPublicationService? _clickDebugPublicationService;
        private VisibleLabelSnapshotProvider? _visibleLabelSnapshotProvider;
        private LabelSelectionCoordinator? _labelSelectionCoordinator;
        private ChestLootSettlementTracker? _chestLootSettlementTracker;
        private VisibleMechanicTargetSelector? _visibleMechanicTargetSelector;
        private VisibleMechanicCoordinator? _visibleMechanicCoordinator;
        private OffscreenStickyTargetHandler? _offscreenStickyTargetHandler;
        private OffscreenPathingCoordinator? _offscreenPathingCoordinator;
        private MovementSkillCoordinator? _movementSkillCoordinator;
        private ClickRuntimeEngine? _clickRuntimeEngine;
        private OffscreenTargetResolver? _offscreenTargetResolver;
        private OffscreenMechanicTargetSelector? _offscreenMechanicTargetSelector;
        private ClickLabelInteractionService? _labelInteractionService;
        private UltimatumAutomationService? _ultimatumAutomationService;

        internal ClickAutomationPort(
            ClickItSettings settings,
            GameController gameController,
            ErrorHandler errorHandler,
            AltarService altarService,
            WeightCalculator weightCalculator,
            AltarDisplayRenderer altarDisplayRenderer,
            Func<Vector2, string, bool> pointIsInClickableArea,
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
            _altarDisplayRenderer = altarDisplayRenderer ?? throw new ArgumentNullException(nameof(altarDisplayRenderer));
            _pointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));
            _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
            _labelInteractionPort = labelInteractionPort ?? throw new ArgumentNullException(nameof(labelInteractionPort));
            _shrineService = shrineService ?? throw new ArgumentNullException(nameof(shrineService));
            _pathfindingService = pathfindingService ?? throw new ArgumentNullException(nameof(pathfindingService));
            _groundItemsVisible = groundItemsVisible ?? throw new ArgumentNullException(nameof(groundItemsVisible));
            _cachedLabels = cachedLabels;
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _freezeDebugTelemetrySnapshot = freezeDebugTelemetrySnapshot;
            _clickTelemetryStore = new ClickTelemetryStore(settings);
            _lockedInteractionDispatcher = new LockedInteractionDispatcher(inputHandler);
            _mechanicPriorityContextProvider = new MechanicPriorityContextProvider(settings, new MechanicPrioritySnapshotService());
        }

        internal void CancelOffscreenPathingState()
        {
            OffscreenPathing.ClearStickyOffscreenTarget();
            _pathfindingService.ClearLatestPath();
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

        internal ClickDebugSnapshot GetLatestClickDebug()
            => _clickTelemetryStore.GetLatestClickDebug();

        internal IReadOnlyList<string> GetLatestClickDebugTrail()
            => _clickTelemetryStore.GetLatestClickDebugTrail();

        internal RuntimeDebugLogSnapshot GetLatestRuntimeDebugLog()
            => _clickTelemetryStore.GetLatestRuntimeDebugLog();

        internal IReadOnlyList<string> GetLatestRuntimeDebugLogTrail()
            => _clickTelemetryStore.GetLatestRuntimeDebugLogTrail();

        internal UltimatumDebugSnapshot GetLatestUltimatumDebug()
            => _clickTelemetryStore.GetLatestUltimatumDebug();

        internal IReadOnlyList<string> GetLatestUltimatumDebugTrail()
            => _clickTelemetryStore.GetLatestUltimatumDebugTrail();

        internal void PublishClickSnapshot(ClickDebugSnapshot snapshot)
        {
            if (!ShouldCaptureClickDebug())
                return;

            _clickTelemetryStore.PublishClickSnapshot(snapshot);
        }

        internal void PublishUltimatumSnapshot(UltimatumDebugSnapshot snapshot)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _clickTelemetryStore.PublishUltimatumSnapshot(snapshot);
        }

        internal void PublishRuntimeLog(string message)
            => SetLatestRuntimeDebugLog(message);

        private bool ShouldCaptureClickDebug()
            => _settings.DebugMode.Value && _settings.DebugShowClicking.Value;

        private bool ShouldCaptureUltimatumDebug()
            => _settings.DebugMode.Value && _settings.DebugShowUltimatum.Value;

        private void PublishUltimatumDebug(UltimatumDebugEvent debugEvent)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _clickTelemetryStore.PublishUltimatumEvent(debugEvent);
        }

        private void SetLatestRuntimeDebugLog(string message)
            => _clickTelemetryStore.PublishRuntimeLog(message);

        private bool TryClickPreferredUltimatumModifier(LabelOnGround label, Vector2 windowTopLeft)
            => UltimatumAutomation.TryClickPreferredModifier(label, windowTopLeft);

        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
            => UltimatumAutomation.TryHandlePanelUi(windowTopLeft);

        private IReadOnlyList<LabelOnGround>? GetLabelsForOffscreenSelection()
            => VisibleLabelSnapshots.GetVisibleOrCachedLabels();

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => VisibleLabelSnapshots.GetVisibleOrCachedLabels();

        private void DebugLog(string message)
        {
            if (_settings.DebugMode?.Value != true)
                return;

            _clickTelemetryStore.PublishRuntimeLog(message);

            if (_settings.LogMessages?.Value == true)
                _errorHandler.LogMessage(message);
        }

        private bool IsClickableInEitherSpace(Vector2 clientPoint, string path)
        {
            RectangleF windowArea = _gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            return _clickSafetyPolicy.IsPointClickableInEitherSpace(clientPoint, windowTopLeft, _pointIsInClickableArea, path);
        }

        private bool IsInsideWindowInEitherSpace(Vector2 point)
        {
            RectangleF windowArea = _gameController.Window.GetWindowRectangleTimeCache;
            return ClickLabelSelectionMath.IsInsideWindowInEitherSpace(point, windowArea);
        }

        private bool EnsureCursorInsideGameWindowForClick(string outsideWindowLogMessage)
        {
            if (_settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(outsideWindowLogMessage);
                return false;
            }

            return true;
        }

        private void HoldDebugTelemetryAfterSuccessfulInteraction(string reason)
        {
            if (_settings.DebugMode?.Value != true || _settings.RenderDebug?.Value != true)
                return;

            int holdDurationMs = Math.Max(0, _settings.DebugFreezeSuccessfulInteractionMs?.Value ?? 0);
            if (holdDurationMs <= 0)
                return;

            _freezeDebugTelemetrySnapshot?.Invoke(reason, holdDurationMs);
        }

        private bool IsCursorInsideGameWindow()
        {
            try
            {
                var winRect = _gameController.Window.GetWindowRectangleTimeCache;
                var cursor = Mouse.GetCursorPosition();
                return _clickSafetyPolicy.IsCursorInsideWindow(winRect, new Vector2(cursor.X, cursor.Y));
            }
            catch
            {
                return true;
            }
        }

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