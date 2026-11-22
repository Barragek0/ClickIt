
namespace ClickIt.Features.Click
{
    internal readonly record struct ClickAutomationPortDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ErrorHandler ErrorHandler,
        AltarService AltarService,
        WeightCalculator WeightCalculator,
        AltarChoiceEvaluator AltarChoiceEvaluator,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Func<Vector2, string, bool> ForceRefreshPointIsInClickableArea,
        InputHandler InputHandler,
        ILabelInteractionPort LabelInteractionPort,
        ILabelSelectionService LabelSelectionService,
        ShrineService ShrineService,
        PathfindingService PathfindingService,
        Func<bool> GroundItemsVisible,
        TimeCache<List<LabelOnGround>> CachedLabels,
        PerformanceMonitor PerformanceMonitor,
        Action<string, int>? FreezeDebugTelemetrySnapshot,
        PluginManager? PluginManager = null,
        InventoryInteractionPolicy? Inventory = null);

    public sealed partial class ClickAutomationPort
    {
        // Keep the constructor eager only for the small set of always-on host dependencies; heavier mechanic/runtime owners stay lazy.
        internal static void ClearThreadLocalStorageForCurrentThread()
        {
            SkillBarBindingResolver.ClearThreadSkillBarEntriesBuffer();
        }

        private readonly ClickItSettings _settings;
        private readonly GameController _gameController;
        private readonly ErrorHandler _errorHandler;
        private readonly AltarService _altarService;
        private readonly WeightCalculator _weightCalculator;
        private readonly AltarChoiceEvaluator _altarChoiceEvaluator;
        private readonly InputHandler _inputHandler;
        private readonly ILabelInteractionPort _labelInteractionPort;
        private readonly ILabelSelectionService _labelSelectionService;
        private readonly LabelClickPointResolver _labelClickPointResolver;
        private readonly ShrineService _shrineService;
        private readonly PathfindingService _pathfindingService;
        private readonly Func<bool> _groundItemsVisible;
        private readonly TimeCache<List<LabelOnGround>> _cachedLabels;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly InventoryInteractionPolicy? _inventory;
        private readonly InteractionExecutor _interactionExecutor;
        private readonly ChestLootSettlementState _chestLootSettlementState = new();
        private readonly ClickRuntimeState _runtimeState = new();
        private readonly MechanicPriorityContextProvider _mechanicPriorityContextProvider;
        private readonly AutoAuguryFatesClickService _autoAuguryFatesClickService;

        internal Func<BlightBuildAction>? TryProgressBlightBuilding { get; set; }
        internal Func<Entity?>? GetBlightPathfindTarget { get; set; }
        internal Func<bool>? IsBlightEncounterActive { get; set; }
        internal Func<RectangleF, bool>? IsBlightMenuRegionClickable { get; set; }
        internal ElementTreeInspector? BlightChestDebug { get; set; }
        internal HarvestEncounterUpgrade? HarvestUpgrade { get; init; }

        internal Func<Vector2, string, bool> PointIsInClickableArea { get; }
        internal Func<Vector2, string, bool> ForceRefreshPointIsInClickableArea { get; }

        private readonly ClickSuccessAnchor _clickSuccessAnchor = new();



        internal ClickAutomationPort(ClickAutomationPortDependencies dependencies)
        {
            ClickItSettings settings = _settings = dependencies.Settings ?? throw new ArgumentNullException(nameof(dependencies.Settings));
            _gameController = dependencies.GameController ?? throw new ArgumentNullException(nameof(dependencies.GameController));
            ErrorHandler errorHandler = _errorHandler = dependencies.ErrorHandler ?? throw new ArgumentNullException(nameof(dependencies.ErrorHandler));
            _altarService = dependencies.AltarService ?? throw new ArgumentNullException(nameof(dependencies.AltarService));
            _weightCalculator = dependencies.WeightCalculator ?? throw new ArgumentNullException(nameof(dependencies.WeightCalculator));
            _altarChoiceEvaluator = dependencies.AltarChoiceEvaluator ?? throw new ArgumentNullException(nameof(dependencies.AltarChoiceEvaluator));
            Func<Vector2, string, bool> pointIsInClickableArea = PointIsInClickableArea = dependencies.PointIsInClickableArea ?? throw new ArgumentNullException(nameof(dependencies.PointIsInClickableArea));
            ForceRefreshPointIsInClickableArea = dependencies.ForceRefreshPointIsInClickableArea ?? throw new ArgumentNullException(nameof(dependencies.ForceRefreshPointIsInClickableArea));
            InputHandler inputHandler = _inputHandler = dependencies.InputHandler ?? throw new ArgumentNullException(nameof(dependencies.InputHandler));
            _labelInteractionPort = dependencies.LabelInteractionPort ?? throw new ArgumentNullException(nameof(dependencies.LabelInteractionPort));
            _labelSelectionService = dependencies.LabelSelectionService ?? throw new ArgumentNullException(nameof(dependencies.LabelSelectionService));
            _labelClickPointResolver = new LabelClickPointResolver(settings);
            _shrineService = dependencies.ShrineService ?? throw new ArgumentNullException(nameof(dependencies.ShrineService));
            _pathfindingService = dependencies.PathfindingService ?? throw new ArgumentNullException(nameof(dependencies.PathfindingService));
            _groundItemsVisible = dependencies.GroundItemsVisible ?? throw new ArgumentNullException(nameof(dependencies.GroundItemsVisible));
            _cachedLabels = dependencies.CachedLabels;
            PerformanceMonitor performanceMonitor = _performanceMonitor = dependencies.PerformanceMonitor ?? throw new ArgumentNullException(nameof(dependencies.PerformanceMonitor));
            Action<string, int>? freezeDebugTelemetrySnapshot = dependencies.FreezeDebugTelemetrySnapshot;
            _inventory = dependencies.Inventory;
            ClickAutomationSupport = new ClickAutomationSupport(new ClickAutomationSupportDependencies(
                Settings: settings,
                TelemetryStore: new ClickTelemetryStore(settings),
                GetWindowRectangle: () => _gameController.Window.GetWindowRectangleTimeCache,
                GetCursorPosition: static () =>
                {
                    SystemDrawingPoint cursor = Mouse.GetCursorPosition();
                    return new Vector2(cursor.X, cursor.Y);
                },
                PointIsInClickableArea: pointIsInClickableArea,
                LogMessage: message => errorHandler.LogMessage(message),
                FreezeDebugTelemetrySnapshot: freezeDebugTelemetrySnapshot));
            _interactionExecutor = new InteractionExecutor(settings, performanceMonitor, inputHandler.IsClickHotkeyActiveForCurrentInputState, errorHandler);
            LockedInteractionDispatcher = new LockedInteractionDispatcher(_interactionExecutor);
            _mechanicPriorityContextProvider = new MechanicPriorityContextProvider(settings, new MechanicPrioritySnapshotService());
            _autoAuguryFatesClickService = new AutoAuguryFatesClickService(settings, dependencies.PluginManager,
                _gameController, ClickAutomationSupport.IsClickableInEitherSpace,
                (position, validateSettledHover, controlClick) =>
                {
                    if (!ClickAutomationSupport.EnsureCursorInsideGameWindowForClick("[AutoAugury] Cursor outside game window")
                        || Input.GetKeyState(Keys.ControlKey))
                        return false;
                    try
                    {
                        if (controlClick && !Mouse.DisableNativeInput)
                            Keyboard.KeyDown(Keys.LControlKey);
                        return LockedInteractionDispatcher.PerformClick(
                            position, null, _gameController, validateSettledHover: validateSettledHover);
                    }
                    finally
                    {
                        if (controlClick && !Mouse.DisableNativeInput)
                            Keyboard.KeyUp(Keys.LControlKey);
                    }
                },
                performanceMonitor.RecordClickInterval, ClickAutomationSupport.DebugLog);
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
        {
            if (_gameController.Window == null)
                return false;

            RectangleF windowArea = _gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            SystemDrawingPoint cursor = Mouse.GetCursorPosition();
            Vector2 cursorAbsolute = new(cursor.X, cursor.Y);

            if (AltarAutomation.TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft))
                return true;

            if (ManualCursorLabels.TryResolveCandidate(allLabels, cursorAbsolute, windowTopLeft, out LabelOnGround? hoveredLabel, out string? mechanicId))
                return ManualCursorLabelInteraction.TryClickCandidate(hoveredLabel, mechanicId, cursorAbsolute, windowTopLeft, allLabels);

            return ManualCursorVisibleMechanics.TryClick(cursorAbsolute, windowTopLeft);
        }

        internal IEnumerator ProcessRegularClick()
        {
            if (_autoAuguryFatesClickService.TryHandlePanel())
                yield break;

            yield return RegularClick.Run();
        }

        internal bool IsAutoAuguryPluginAvailable()
            => _autoAuguryFatesClickService.IsPluginAvailable;

        internal bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            => UltimatumAutomation.TryGetOptionPreview(out previews);

        internal string? GetEssenceCurrencyWarning()
            => LabelInteraction.GetVaalOrbWarning();

        internal void RefreshUltimatumPreview()
            => UltimatumAutomation.RefreshUltimatumPreview();

        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
            => UltimatumAutomation.TryHandlePanelUi(windowTopLeft);

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => VisibleLabelSnapshots.GetCachedLabels();

        private bool IsBlightBuildOrUpgradeIconAt(Vector2 screenPos)
            => IsBlightTowerUiAt(screenPos, null, 0f);

        internal bool IsBlightTowerUiAt(Vector2 screenPos, Element? hover, float predictedDrift)
        {
            IReadOnlyList<LabelOnGround>? labels = GetLabelsForRegularSelection();
            if (labels == null)
                return true;

            float padding = BlightIconBoxPadding + predictedDrift;
            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label == null || !BlightEntityCache.IsBlightFoundationOrTowerLabel(label))
                    continue;

                Element? element = BlightEntityCache.ResolveLabelElement(label);
                if (element == null)
                    continue;
                if (BlightMenuInteractions.IsHoverInside(hover, element))
                    return true;

                for (int childIndex = 2; childIndex <= 3; childIndex++)
                {
                    Element? control = BlightMenuInteractions.GetMenuChildElement(element, childIndex);
                    if (control?.IsVisible == true && IsNearBlightControl(control, screenPos, padding))
                        return true;
                }
                if (BlightMenuInteractions.GetVisibleMenuChildren(element, 3) is { } slots)
                    foreach (Element slot in slots)
                        if (IsNearBlightControl(slot, screenPos, padding))
                            return true;
            }

            return false;
        }

        private static bool IsNearBlightControl(Element control, Vector2 point, float padding)
        {
            RectangleF rect = control.GetClientRect();
            return rect.Width > 0 && rect.Height > 0 && PointInRect(
                new RectangleF(rect.X - padding, rect.Y - padding,
                    rect.Width + (padding * 2f), rect.Height + (padding * 2f)), point);
        }

        // Padding around a blight build/upgrade icon that makes it an unclickable box — a click that lands in or near the icon is treated as an accidental icon click.
        private const float BlightIconBoxPadding = 30f;

        private static bool PointInRect(RectangleF rect, Vector2 point)
            => point.X >= rect.X && point.X <= rect.X + rect.Width
               && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
    }
}
