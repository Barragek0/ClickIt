
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
        private readonly InputHandler _inputHandler;
        private readonly ILabelInteractionPort _labelInteractionPort;
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
            PointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));
            ForceRefreshPointIsInClickableArea = forceRefreshPointIsInClickableArea ?? throw new ArgumentNullException(nameof(forceRefreshPointIsInClickableArea));
            _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
            _labelInteractionPort = labelInteractionPort ?? throw new ArgumentNullException(nameof(labelInteractionPort));
            _labelClickPointResolver = new LabelClickPointResolver(settings);
            _shrineService = shrineService ?? throw new ArgumentNullException(nameof(shrineService));
            _pathfindingService = pathfindingService ?? throw new ArgumentNullException(nameof(pathfindingService));
            _groundItemsVisible = groundItemsVisible ?? throw new ArgumentNullException(nameof(groundItemsVisible));
            _cachedLabels = cachedLabels;
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
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

        internal void RefreshUltimatumPreview()
            => UltimatumAutomation.RefreshUltimatumPreview();

        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
            => UltimatumAutomation.TryHandlePanelUi(windowTopLeft);

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => VisibleLabelSnapshots.GetCachedLabels();

        // Pathfinding safety: every blight tower's build icon (Child[2]) and upgrade icon (Child[3])
        // is an UNCLICKABLE BOX — pathfinding walk-clicks must never land in one (or near one), or
        // they would accidentally build/upgrade a tower.  Pure geometry: each icon rect is padded
        // and the padded rect is tested.  No UIHover — the hover element is unreliable for a
        // freshly-moved cursor, so it let walk-clicks slip through (and added a per-click hover cost).
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

        // Fail-closed pre-click check for blight MENU clicks (build icon, upgrade icon, tower-type
        // slot, or spec button): the click point must still be over part of the tower's menu UI —
        // otherwise the menu moved or closed since the position was resolved and the click is
        // skipped.  The tower-type slots and spec buttons are children of the upgrade icon (Child[3])
        // that extend beyond its own rect, so they are checked too.  Rect-based only (no UIHover —
        // it was unreliable and rejected valid clicks while the hover was still updating).
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

        // True when the point is inside any VISIBLE child of the tower menu
        // (Child[0].Child[3].Child[i]) — the tower-type build slots and specialization buttons,
        // which extend beyond the upgrade icon's own rect.
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

        // Padding around a blight build/upgrade icon that makes it an unclickable box — a click that
        // lands in or near the icon is treated as an accidental icon click.
        private const float BlightIconBoxPadding = 30f;

        private static bool PointInRect(RectangleF rect, Vector2 point)
            => point.X >= rect.X && point.X <= rect.X + rect.Width
               && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;

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