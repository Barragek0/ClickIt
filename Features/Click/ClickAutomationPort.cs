
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
        Action<string, int>? FreezeDebugTelemetrySnapshot);

    public sealed partial class ClickAutomationPort
    {
        // Keep the constructor eager only for the small set of always-on host dependencies; heavier mechanic/runtime owners stay lazy.
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
        private readonly InputHandler _inputHandler;
        private readonly ILabelInteractionPort _labelInteractionPort;
        private readonly ILabelSelectionService _labelSelectionService;
        private readonly LabelClickPointResolver _labelClickPointResolver;
        private readonly ShrineService _shrineService;
        private readonly PathfindingService _pathfindingService;
        private readonly Func<bool> _groundItemsVisible;
        private readonly TimeCache<List<LabelOnGround>> _cachedLabels;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly InteractionExecutor _interactionExecutor;
        private readonly ChestLootSettlementState _chestLootSettlementState = new();
        private readonly ClickRuntimeState _runtimeState = new();
        private readonly MechanicPriorityContextProvider _mechanicPriorityContextProvider;

        internal Func<LabelOnGround?>? GetHarvestLabelToClick { get; set; }
        internal Func<BlightBuildAction>? TryProgressBlightBuilding { get; set; }
        internal Func<Entity?>? GetBlightPathfindTarget { get; set; }
        internal Func<bool>? IsBlightEncounterActive { get; set; }
        internal ElementTreeInspector? BlightChestDebug { get; set; }

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
            yield return RegularClick.Run();
        }

        internal bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            => UltimatumAutomation.TryGetOptionPreview(out previews);

        internal void RefreshUltimatumPreview()
            => UltimatumAutomation.RefreshUltimatumPreview();

        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
            => UltimatumAutomation.TryHandlePanelUi(windowTopLeft);

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => VisibleLabelSnapshots.GetCachedLabels();

        // Pathfinding safety: blight build/upgrade icons are padded UNCLICKABLE BOXES (pure geometry, no UIHover).
        private bool IsBlightBuildOrUpgradeIconAt(Vector2 screenPos, float paddingPx = BlightIconBoxPadding)
        {
            IReadOnlyList<LabelOnGround>? labels = GetLabelsForRegularSelection();
            if (labels == null || labels.Count == 0)
                return false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label == null || !BlightEntityCache.IsBlightFoundationOrTowerLabel(label))
                    continue;

                Element? labelElement = BlightEntityCache.ResolveLabelElement(label);
                if (labelElement == null)
                    continue;

                if (IsBlightIconAt(labelElement, 2, screenPos, paddingPx)
                    || IsBlightIconAt(labelElement, 3, screenPos, paddingPx))
                    return true;
            }

            return false;
        }

        private static bool IsBlightIconAt(Element labelElement, int childIndex, Vector2 screenPos, float paddingPx)
        {
            RectangleF? iconRect = BlightMenuInteractions.GetMenuChildRect(labelElement, childIndex);
            if (iconRect == null)
                return false;
            RectangleF padded = new(
                iconRect.Value.X - paddingPx,
                iconRect.Value.Y - paddingPx,
                iconRect.Value.Width + (paddingPx * 2f),
                iconRect.Value.Height + (paddingPx * 2f));
            return PointInRect(padded, screenPos);
        }

        // Fail-closed pre-click check for blight MENU clicks: the click point must still be over the tower's menu UI (rect-based, no UIHover).
        internal bool IsBlightTowerUiAt(Vector2 screenPos)
        {
            IReadOnlyList<LabelOnGround>? labels = GetLabelsForRegularSelection();
            if (labels == null || labels.Count == 0)
                return false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label == null || !BlightEntityCache.IsBlightFoundationOrTowerLabel(label))
                    continue;

                Element? labelElement = BlightEntityCache.ResolveLabelElement(label);
                if (labelElement == null)
                    continue;

                if (IsBlightIconAt(labelElement, 2, screenPos, 0f)
                    || IsBlightIconAt(labelElement, 3, screenPos, 0f)
                    || IsBlightMenuSlotAt(labelElement, screenPos))
                    return true;
            }

            return false;
        }

        // True when the point is inside any VISIBLE child of the tower menu (Child[0].Child[3].Child[i]) — the tower-type build slots and specialization buttons, which extend beyond the upgrade icon's own rect.
        private static bool IsBlightMenuSlotAt(Element labelElement, Vector2 screenPos)
        {
            Element? menu = BlightMenuInteractions.GetMenuChildElement(labelElement, 3);
            if (menu == null)
                return false;
            try
            {
                for (int i = 0; i < menu.ChildCount; i++)
                {
                    Element? child = menu.GetChildAtIndex(i);
                    if (child == null || !child.IsVisible)
                        continue;
                    RectangleF rect = child.GetClientRect();
                    if (PointInRect(rect, screenPos))
                        return true;
                }
            }
            catch { }
            return false;
        }

        // Padding around a blight build/upgrade icon that makes it an unclickable box — a click that lands in or near the icon is treated as an accidental icon click.
        private const float BlightIconBoxPadding = 30f;

        private static bool PointInRect(RectangleF rect, Vector2 point)
            => point.X >= rect.X && point.X <= rect.X + rect.Width
               && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
    }
}
