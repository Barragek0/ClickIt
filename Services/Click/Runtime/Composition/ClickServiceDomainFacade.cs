using ClickIt.Services.Click.Application;
using ClickIt.Services.Click.Interaction;
using ClickIt.Services.Click.Label;
using ClickIt.Services.Click.Ranking;
using ClickIt.Services.Mechanics;
using ClickIt.Services.Observability;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using SharpDX;

namespace ClickIt.Services.Click.Runtime.Composition
{
    internal sealed class ClickServiceDomainFacade(ClickServiceDomainFacadeDependencies dependencies)
    {
        private readonly ClickServiceDomainFacadeDependencies _dependencies = dependencies;
        private ClickFacadeSupport? _facadeSupport;
        private ClickLabelInteractionService? _labelInteractionService;
        private UltimatumAutomationService? _ultimatumAutomationService;
        private ClickRuntimeCompositionHost? _runtimeComposition;

        internal ClickFacadeSupport FacadeSupport => _facadeSupport ??= new(
            _dependencies.Settings,
            _dependencies.GameController,
            _dependencies.ErrorHandler,
            _dependencies.PointIsInClickableArea,
            _dependencies.ClickSafetyPolicy,
            _dependencies.LockedInteractionDispatcher,
            _dependencies.PerformanceMonitor,
            _dependencies.SetLatestRuntimeDebugLog,
            _dependencies.FreezeDebugTelemetrySnapshot);

        internal ClickLabelInteractionService LabelInteraction
            => _labelInteractionService ??= new ClickLabelInteractionService(
                new ClickLabelInteractionServiceDependencies(
                    _dependencies.Settings,
                    _dependencies.GameController,
                    _dependencies.InputHandler,
                    _dependencies.LabelFilterService,
                    FacadeSupport.IsClickableInEitherSpace,
                    FacadeSupport.IsInsideWindowInEitherSpace,
                    FacadeSupport.InteractionExecutionRuntime.Execute,
                    _dependencies.GroundItemsVisible,
                    messageFactory => FacadeSupport.DebugLog(messageFactory())));

        internal UltimatumAutomationService UltimatumAutomation
            => _ultimatumAutomationService ??= new UltimatumAutomationService(
                new UltimatumAutomationServiceDependencies(
                    _dependencies.Settings,
                    _dependencies.GameController,
                    _dependencies.CachedLabels,
                    FacadeSupport.EnsureCursorInsideGameWindowForClick,
                    FacadeSupport.IsClickableInEitherSpace,
                    messageFactory => FacadeSupport.DebugLog(messageFactory()),
                    (clickPos, clickElement) => _dependencies.LockedInteractionDispatcher.PerformClick(clickPos, clickElement, _dependencies.GameController),
                    _dependencies.PerformanceMonitor.RecordClickInterval,
                    _dependencies.ShouldCaptureUltimatumDebug,
                    _dependencies.PublishUltimatumDebug));

        internal ClickRuntimeCompositionHost RuntimeComposition
            => _runtimeComposition ??= new(new ClickRuntimeCompositionHostDependencies(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.ErrorHandler,
                _dependencies.AltarService,
                _dependencies.WeightCalculator,
                _dependencies.AltarDisplayRenderer,
                _dependencies.PointIsInClickableArea,
                _dependencies.InputHandler,
                _dependencies.LabelFilterService,
                _dependencies.ShrineService,
                _dependencies.PathfindingService,
                _dependencies.GroundItemsVisible,
                _dependencies.CachedLabels,
                _dependencies.PerformanceMonitor,
                _dependencies.LockedInteractionDispatcher,
                _dependencies.ChestLootSettlementState,
                _dependencies.RuntimeState,
                () => LabelInteraction,
                () => FacadeSupport.InteractionExecutionRuntime,
                FacadeSupport.EnsureCursorInsideGameWindowForClick,
                FacadeSupport.DebugLog,
                _dependencies.ShouldCaptureClickDebug,
                _dependencies.SetLatestClickDebug,
                _dependencies.ShouldCaptureUltimatumDebug,
                _dependencies.PublishUltimatumDebug,
                FacadeSupport.IsClickableInEitherSpace,
                FacadeSupport.IsInsideWindowInEitherSpace,
                LabelInteraction.TryCorruptEssence,
                LabelInteraction.TryGetCursorDistanceSquaredToEntity,
                LabelInteraction.BuildLabelRangeRejectionDebugSummary,
                LabelInteraction.BuildLabelSourceDebugSummary,
                LabelInteraction.BuildNoLabelDebugSummary,
                _dependencies.MechanicPriorityContextProvider.Refresh,
                _dependencies.MechanicPriorityContextProvider.CreateContext,
                _dependencies.TryHandleUltimatumPanelUi,
                _dependencies.GetLabelsForRegularSelection,
                _dependencies.GetLabelsForOffscreenSelection,
                _dependencies.ShouldSuppressPathfindingLabel,
                _dependencies.TryClickPreferredUltimatumModifier,
                FacadeSupport.HoldDebugTelemetryAfterSuccessfulInteraction));
    }

    internal readonly record struct ClickServiceDomainFacadeDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ErrorHandler ErrorHandler,
        AltarService AltarService,
        WeightCalculator WeightCalculator,
        Rendering.AltarDisplayRenderer AltarDisplayRenderer,
        Func<Vector2, string, bool> PointIsInClickableArea,
        InputHandler InputHandler,
        LabelFilterService LabelFilterService,
        ShrineService ShrineService,
        PathfindingService PathfindingService,
        Func<bool> GroundItemsVisible,
        TimeCache<List<LabelOnGround>> CachedLabels,
        PerformanceMonitor PerformanceMonitor,
        global::ClickIt.Services.Click.Safety.IClickSafetyPolicy ClickSafetyPolicy,
        LockedInteractionDispatcher LockedInteractionDispatcher,
        ChestLootSettlementState ChestLootSettlementState,
        ClickRuntimeState RuntimeState,
        MechanicPriorityContextProvider MechanicPriorityContextProvider,
        Action<string> SetLatestRuntimeDebugLog,
        Func<bool> ShouldCaptureClickDebug,
        Action<ClickDebugSnapshot> SetLatestClickDebug,
        Func<bool> ShouldCaptureUltimatumDebug,
        Action<UltimatumDebugEvent> PublishUltimatumDebug,
        Func<Vector2, bool> TryHandleUltimatumPanelUi,
        Func<IReadOnlyList<LabelOnGround>?> GetLabelsForRegularSelection,
        Func<IReadOnlyList<LabelOnGround>?> GetLabelsForOffscreenSelection,
        Func<LabelOnGround, bool> ShouldSuppressPathfindingLabel,
        Func<LabelOnGround, Vector2, bool> TryClickPreferredUltimatumModifier,
        Action<string, int>? FreezeDebugTelemetrySnapshot = null);
}