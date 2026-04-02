using System.Collections;
using ExileCore;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ClickIt.Definitions;
using ClickIt.Components;
using ClickIt.Utils;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using System.Diagnostics.CodeAnalysis;
using ClickIt.Services.Click.Safety;
using ClickIt.Services.Click.Interaction;
using ClickIt.Services.Click.Runtime;
using ClickIt.Services.Click.Application;
using ClickIt.Services.Click.Label;
using ClickIt.Services.Click;
using ClickIt.Services.Click.Ranking;
using ClickIt.Services.Click.Runtime.Composition;
using ClickIt.Services.Mechanics;
using ClickIt.Services.Observability;

#nullable enable

namespace ClickIt.Services
{

    public class ClickService(
        ClickItSettings settings,
        GameController gameController,
        ErrorHandler errorHandler,
        AltarService altarService,
        WeightCalculator weightCalculator,
        Rendering.AltarDisplayRenderer altarDisplayRenderer,
        Func<Vector2, string, bool> pointIsInClickableArea,
        InputHandler inputHandler,
        LabelFilterService labelFilterService,
        ShrineService shrineService,
        PathfindingService pathfindingService,
        Func<bool> groundItemsVisible,
        TimeCache<List<LabelOnGround>> cachedLabels,
        PerformanceMonitor performanceMonitor,
        Action<string, int>? freezeDebugTelemetrySnapshot = null)
        : Observability.IClickTelemetryPublisher, IClickAutomationService
    {
        private readonly ClickItSettings settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly GameController gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
        private readonly ErrorHandler errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        private readonly AltarService altarService = altarService ?? throw new ArgumentNullException(nameof(altarService));
        private readonly WeightCalculator weightCalculator = weightCalculator ?? throw new ArgumentNullException(nameof(weightCalculator));
        private readonly Rendering.AltarDisplayRenderer altarDisplayRenderer = altarDisplayRenderer ?? throw new ArgumentNullException(nameof(altarDisplayRenderer));
        private readonly Func<Vector2, string, bool> pointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));
        private readonly InputHandler inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
        private readonly LabelFilterService labelFilterService = labelFilterService ?? throw new ArgumentNullException(nameof(labelFilterService));
        private readonly ShrineService shrineService = shrineService ?? throw new ArgumentNullException(nameof(shrineService));
        private readonly PathfindingService pathfindingService = pathfindingService ?? throw new ArgumentNullException(nameof(pathfindingService));
        private readonly Func<bool> groundItemsVisible = groundItemsVisible ?? throw new ArgumentNullException(nameof(groundItemsVisible));
        private readonly TimeCache<List<LabelOnGround>> cachedLabels = cachedLabels;
        private readonly PerformanceMonitor performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        private readonly Action<string, int>? freezeDebugTelemetrySnapshot = freezeDebugTelemetrySnapshot;
        private readonly ClickTelemetryStore _clickTelemetryStore = new(settings);
        private readonly IClickSafetyPolicy _clickSafetyPolicy = new ClickSafetyPolicy();
        private readonly LockedInteractionDispatcher _lockedInteractionDispatcher = new(inputHandler);
        private readonly ChestLootSettlementState chestLootSettlementState = new();
        private readonly ClickRuntimeState _runtimeState = new();
        private readonly MechanicPriorityContextProvider _mechanicPriorityContextProvider = new(settings, new MechanicPrioritySnapshotService());
        private ClickServiceDomainFacade? _domainFacade;

        private ClickServiceDomainFacade DomainFacade => _domainFacade ??= new(new ClickServiceDomainFacadeDependencies(
            settings,
            gameController,
            errorHandler,
            altarService,
            weightCalculator,
            altarDisplayRenderer,
            pointIsInClickableArea,
            inputHandler,
            labelFilterService,
            shrineService,
            pathfindingService,
            groundItemsVisible,
            cachedLabels,
            performanceMonitor,
            _clickSafetyPolicy,
            _lockedInteractionDispatcher,
            chestLootSettlementState,
            _runtimeState,
            _mechanicPriorityContextProvider,
            SetLatestRuntimeDebugLog,
            ShouldCaptureClickDebug,
            SetLatestClickDebug,
            ShouldCaptureUltimatumDebug,
            PublishUltimatumDebug,
            TryHandleUltimatumPanelUi,
            GetLabelsForRegularSelection,
            GetLabelsForOffscreenSelection,
            label => ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(
                LabelSelection.ShouldSuppressLeverClick(label),
                UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(label)),
            TryClickPreferredUltimatumModifier,
            freezeDebugTelemetrySnapshot));

        private ClickFacadeSupport FacadeSupport => DomainFacade.FacadeSupport;

        [ThreadStatic]
        private static HashSet<long>? _threadGroundLabelEntityAddresses;

        internal const string ShrineMechanicId = MechanicIds.Shrines;
        internal const string LostShipmentMechanicId = MechanicIds.LostShipment;
        internal const int HiddenFallbackCandidateCacheWindowMs = 150;
        internal const int VisibleMechanicCandidateCacheWindowMs = 80;
        internal const int GroundLabelEntityAddressCacheWindowMs = 150;

        private ClickRuntimeCompositionHost RuntimeComposition => DomainFacade.RuntimeComposition;

        private AltarAutomationService AltarAutomation => RuntimeComposition.AltarAutomation;

        private ClickDebugPublicationService ClickDebugPublisher => RuntimeComposition.ClickDebugPublisher;

        private VisibleLabelSnapshotProvider VisibleLabelSnapshots => RuntimeComposition.VisibleLabelSnapshots;

        private LabelSelectionCoordinator LabelSelection => RuntimeComposition.LabelSelection;

        private ChestLootSettlementTracker ChestLootSettlement => RuntimeComposition.ChestLootSettlement;

        private VisibleMechanicCoordinator VisibleMechanics => RuntimeComposition.VisibleMechanics;

        private OffscreenPathingCoordinator OffscreenPathing => RuntimeComposition.OffscreenPathing;

        private ClickRuntimeEngine RegularClick => RuntimeComposition.RegularClick;

        internal void CancelOffscreenPathingState()
        {
            OffscreenPathing.ClearStickyOffscreenTarget();
            pathfindingService.ClearLatestPath();
        }

        internal void CancelPostChestLootSettlementState()
        {
            ChestLootSettlement.ClearPendingChestOpenConfirmation();
            ChestLootSettlement.ClearPostChestLootSettlementWatch();
        }

        internal static void ClearThreadLocalStorageForCurrentThread()
        {
            _threadGroundLabelEntityAddresses?.Clear();
            _threadGroundLabelEntityAddresses = null;
            MovementSkillMath.ClearThreadSkillBarEntriesBuffer();
        }

        internal bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? allLabels)
        {
            return LabelSelection.TryClickManualUiHoverLabel(allLabels);
        }

        internal IEnumerator ProcessRegularClick()
        {
            return RegularClick.Run();
        }

        internal IEnumerator ProcessAltarClicking()
            => AltarAutomation.ProcessAltarClicking();

        internal bool HasClickableAltars()
            => AltarAutomation.HasClickableAltars();

        private bool TryClickManualCursorPreferredAltarOption(Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => AltarAutomation.TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft);

        internal bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
            => AltarAutomation.ShouldClickAltar(altar, clickEater, clickExarch);

        public bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            => UltimatumAutomation.TryGetOptionPreview(out previews);

        private bool TryClickPreferredUltimatumModifier(LabelOnGround label, Vector2 windowTopLeft)
            => UltimatumAutomation.TryClickPreferredModifier(label, windowTopLeft);

        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
            => UltimatumAutomation.TryHandlePanelUi(windowTopLeft);

        private IReadOnlyList<LabelOnGround>? GetLabelsForOffscreenSelection()
            => VisibleLabelSnapshots.GetVisibleOrCachedLabels();

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => VisibleLabelSnapshots.GetVisibleOrCachedLabels();

        public ClickDebugSnapshot GetLatestClickDebug()
        {
            return _clickTelemetryStore.GetLatestClickDebug();
        }

        public IReadOnlyList<string> GetLatestClickDebugTrail()
        {
            return _clickTelemetryStore.GetLatestClickDebugTrail();
        }

        public RuntimeDebugLogSnapshot GetLatestRuntimeDebugLog()
        {
            return _clickTelemetryStore.GetLatestRuntimeDebugLog();
        }

        public IReadOnlyList<string> GetLatestRuntimeDebugLogTrail()
        {
            return _clickTelemetryStore.GetLatestRuntimeDebugLogTrail();
        }

        public UltimatumDebugSnapshot GetLatestUltimatumDebug()
        {
            return _clickTelemetryStore.GetLatestUltimatumDebug();
        }

        public IReadOnlyList<string> GetLatestUltimatumDebugTrail()
        {
            return _clickTelemetryStore.GetLatestUltimatumDebugTrail();
        }

        private void SetLatestClickDebug(ClickDebugSnapshot snapshot)
        {
            if (!ShouldCaptureClickDebug())
                return;

            _clickTelemetryStore.PublishClickSnapshot(snapshot);
        }

        private bool ShouldCaptureClickDebug()
        {
            return settings.DebugMode.Value && settings.DebugShowClicking.Value;
        }

        private bool ShouldCaptureUltimatumDebug()
        {
            return settings.DebugMode.Value && settings.DebugShowUltimatum.Value;
        }

        private void SetLatestUltimatumDebug(UltimatumDebugSnapshot snapshot)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _clickTelemetryStore.PublishUltimatumSnapshot(snapshot);
        }

        private void PublishUltimatumDebug(UltimatumDebugEvent debugEvent)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _clickTelemetryStore.PublishUltimatumEvent(debugEvent);
        }

        private void SetLatestRuntimeDebugLog(string message)
        {
            _clickTelemetryStore.PublishRuntimeLog(message);
        }

        private UltimatumAutomationService UltimatumAutomation => DomainFacade.UltimatumAutomation;

        private ClickLabelInteractionService LabelInteraction => DomainFacade.LabelInteraction;

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

        void IClickTelemetryPublisher.PublishClickSnapshot(ClickDebugSnapshot snapshot)
        {
            SetLatestClickDebug(snapshot);
        }

        void IClickTelemetryPublisher.PublishUltimatumSnapshot(UltimatumDebugSnapshot snapshot)
        {
            SetLatestUltimatumDebug(snapshot);
        }

        void IClickTelemetryPublisher.PublishRuntimeLog(string message)
        {
            SetLatestRuntimeDebugLog(message);
        }

    }
}


