using System.Collections;
using ClickIt.Services.Click.Application;
using ClickIt.Services.Click.Interaction;
using ClickIt.Services.Click.Label;
using ClickIt.Services.Click.Selection;
using ClickIt.Services.Label.Classification.Policies;
using ClickIt.Services.Observability;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using SharpDX;

namespace ClickIt.Services.Click.Runtime
{
    internal sealed class ClickRuntimeCompositionHost(ClickRuntimeCompositionHostDependencies dependencies)
    {
        private readonly ClickRuntimeCompositionHostDependencies _dependencies = dependencies;
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

        internal AltarAutomationService AltarAutomation => _altarAutomationService ??= new(CreateAltarAutomationServiceDependencies());

        internal ClickDebugPublicationService ClickDebugPublisher => _clickDebugPublicationService ??= new(new ClickDebugPublicationServiceDependencies(
            _dependencies.GameController,
            _dependencies.ShouldCaptureClickDebug,
            _dependencies.SetLatestClickDebug,
            _dependencies.IsClickableInEitherSpace,
            _dependencies.IsInsideWindowInEitherSpace));

        internal VisibleLabelSnapshotProvider VisibleLabelSnapshots => _visibleLabelSnapshotProvider ??= new(_dependencies.GameController, _dependencies.CachedLabels);

        internal LabelSelectionCoordinator LabelSelection => _labelSelectionCoordinator ??= new(CreateLabelSelectionCoordinatorDependencies());

        internal ChestLootSettlementTracker ChestLootSettlement => _chestLootSettlementTracker ??= new(CreateChestLootSettlementTrackerDependencies());

        internal VisibleMechanicCoordinator VisibleMechanics => _visibleMechanicCoordinator ??= new(CreateVisibleMechanicCoordinatorDependencies());

        internal OffscreenPathingCoordinator OffscreenPathing => _offscreenPathingCoordinator ??= new(CreateOffscreenPathingCoordinatorDependencies());

        internal ClickRuntimeEngine RegularClick => _clickRuntimeEngine ??= new(CreateClickRuntimeEngineDependencies());

        private VisibleMechanicTargetSelector VisibleMechanicSelection => _visibleMechanicTargetSelector ??= new(CreateVisibleMechanicTargetSelectorDependencies());

        private OffscreenStickyTargetHandler OffscreenStickyTargets => _offscreenStickyTargetHandler ??= new(CreateOffscreenStickyTargetHandlerDependencies());

        private OffscreenMechanicTargetSelector OffscreenTargetSelection => _offscreenMechanicTargetSelector ??= new(CreateOffscreenMechanicTargetSelectorDependencies());

        private MovementSkillCoordinator MovementSkills => _movementSkillCoordinator ??= new(CreateMovementSkillCoordinatorDependencies());

        private OffscreenTargetResolver OffscreenTargetResolver => _offscreenTargetResolver ??= new(_dependencies.GameController, _dependencies.PathfindingService);

        private ClickTickContextFactory TickContextFactory => _tickContextFactory ??= new(CreateClickTickContextFactoryDependencies());

        private ClickLabelInteractionService LabelInteraction => _dependencies.GetLabelInteraction();

        private IInteractionExecutionRuntime InteractionExecutionRuntime => _dependencies.GetInteractionExecutionRuntime();

        private AltarAutomationServiceDependencies CreateAltarAutomationServiceDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.AltarService.GetAltarComponentsReadOnly,
                _dependencies.AltarService.RemoveAltarComponentsByElement,
                pc => _dependencies.WeightCalculator.CalculateAltarWeights(pc),
                (altar, weights, topModsRect, bottomModsRect, topModsTopLeft) => _dependencies.AltarDisplayRenderer.DetermineAltarChoice(altar, weights, topModsRect, bottomModsRect, topModsTopLeft),
                _dependencies.IsClickableInEitherSpace,
                _dependencies.EnsureCursorInsideGameWindowForClick,
                InteractionExecutionRuntime.Execute,
                _dependencies.DebugLog,
                _dependencies.ErrorHandler.LogError,
                _dependencies.LockedInteractionDispatcher.ElementLock);

        private ChestLootSettlementTrackerDependencies CreateChestLootSettlementTrackerDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.ChestLootSettlementState,
                () => VisibleMechanics.CollectGroundLabelEntityAddresses(),
                ClickDebugPublisher.PublishClickFlowDebugStage,
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformTrackedLabelClick);

        private LabelSelectionCoordinatorDependencies CreateLabelSelectionCoordinatorDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.LabelFilterService,
                _dependencies.InputHandler,
                AltarAutomation.HasClickableAltars,
                AltarAutomation.TryClickManualCursorPreferredAltarOption,
                _dependencies.TryCorruptEssence,
                _dependencies.TryClickPreferredUltimatumModifier,
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformManualCursorInteraction,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => _dependencies.PathfindingService.ClearLatestPath(),
                _dependencies.DebugLog,
                () => VisibleMechanics.ResolveNextShrineCandidate(),
                () =>
                {
                    VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
                    return (lostShipment, settlers);
                },
                entity => VisibleMechanics.HandleSuccessfulMechanicEntityClick(entity),
                () => _dependencies.ShrineService.InvalidateCache(),
                _dependencies.TryGetCursorDistanceSquaredToEntity,
                _dependencies.RefreshMechanicPriorityCaches,
                _dependencies.CreateMechanicPriorityContext,
                _dependencies.ShouldCaptureClickDebug,
                _dependencies.BuildLabelRangeRejectionDebugSummary,
                (stage, notes) => ClickDebugPublisher.PublishClickFlowDebugStage(stage, notes),
                () => _dependencies.RuntimeState.LastLeverKey,
                value => _dependencies.RuntimeState.LastLeverKey = value,
                () => _dependencies.RuntimeState.LastLeverClickTimestampMs,
                value => _dependencies.RuntimeState.LastLeverClickTimestampMs = value);

        private VisibleMechanicTargetSelectorDependencies CreateVisibleMechanicTargetSelectorDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.ShouldCaptureClickDebug,
                _dependencies.SetLatestClickDebug,
                _dependencies.DebugLog,
                _dependencies.IsInsideWindowInEitherSpace,
                _dependencies.IsClickableInEitherSpace,
                mechanicId => SettlersMechanicPolicy.IsEnabled(_dependencies.Settings, mechanicId),
                () => VisibleMechanics.CollectGroundLabelEntityAddresses());

        private VisibleMechanicCoordinatorDependencies CreateVisibleMechanicCoordinatorDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.ShrineService,
                VisibleMechanicSelection,
                _dependencies.PointIsInClickableArea,
                LabelInteraction.PerformMechanicClick,
                LabelInteraction.PerformMechanicInteraction,
                entity => OffscreenPathing.IsStickyTarget(entity),
                () => OffscreenPathing.ClearStickyOffscreenTarget(),
                () => _dependencies.ShrineService.InvalidateCache(),
                () => _dependencies.PathfindingService.ClearLatestPath(),
                _dependencies.DebugLog,
                _dependencies.HoldDebugTelemetryAfterSuccessfulInteraction,
                _dependencies.ShouldCaptureClickDebug,
                _dependencies.SetLatestClickDebug,
                _dependencies.IsInsideWindowInEitherSpace,
                _dependencies.IsClickableInEitherSpace);

        private MovementSkillCoordinatorDependencies CreateMovementSkillCoordinatorDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.PerformanceMonitor,
                OffscreenTargetResolver.GetRemainingOffscreenPathNodeCount,
                _dependencies.EnsureCursorInsideGameWindowForClick,
                _dependencies.PointIsInClickableArea,
                _dependencies.DebugLog,
                () => _dependencies.RuntimeState.LastMovementSkillUseTimestampMs,
                value => _dependencies.RuntimeState.LastMovementSkillUseTimestampMs = value,
                () => _dependencies.RuntimeState.MovementSkillPostCastClickBlockUntilTimestampMs,
                value => _dependencies.RuntimeState.MovementSkillPostCastClickBlockUntilTimestampMs = value,
                () => _dependencies.RuntimeState.MovementSkillStatusPollUntilTimestampMs,
                value => _dependencies.RuntimeState.MovementSkillStatusPollUntilTimestampMs = value,
                () => _dependencies.RuntimeState.LastUsedMovementSkillEntry,
                value => _dependencies.RuntimeState.LastUsedMovementSkillEntry = value);

        private OffscreenMechanicTargetSelectorDependencies CreateOffscreenMechanicTargetSelectorDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.ShrineService,
                AltarAutomation.HasClickableAltars,
                () => VisibleMechanics.ResolveNextShrineCandidate() != null,
                () =>
                {
                    VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
                    return (lostShipment.HasValue, settlers.HasValue);
                },
                (stage, notes) => ClickDebugPublisher.PublishClickFlowDebugStage(stage, notes),
                _dependencies.IsClickableInEitherSpace,
                _dependencies.IsInsideWindowInEitherSpace,
                _dependencies.ShouldSuppressPathfindingLabel,
                label => _dependencies.LabelFilterService.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                _dependencies.GetLabelsForOffscreenSelection,
                _dependencies.RefreshMechanicPriorityCaches,
                (distance, mechanicId) => CandidateRankingEngine.BuildRank(distance, mechanicId, _dependencies.CreateMechanicPriorityContext()));

        private OffscreenStickyTargetHandlerDependencies CreateOffscreenStickyTargetHandlerDependencies()
            => new(
                _dependencies.GameController,
                _dependencies.ShrineService,
                () => _dependencies.RuntimeState.StickyOffscreenTargetAddress,
                value => _dependencies.RuntimeState.StickyOffscreenTargetAddress = value,
                address => EntityQueryService.FindEntityByAddress(_dependencies.GameController, address),
                LabelInteraction.PerformMechanicClick,
                _dependencies.IsClickableInEitherSpace,
                _dependencies.ShouldSuppressPathfindingLabel,
                label => _dependencies.LabelFilterService.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                _dependencies.HoldDebugTelemetryAfterSuccessfulInteraction,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => _dependencies.ShrineService.InvalidateCache());

        private OffscreenPathingCoordinatorDependencies CreateOffscreenPathingCoordinatorDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.PathfindingService,
                OffscreenTargetSelection,
                OffscreenStickyTargets,
                () => _dependencies.RuntimeState.StickyOffscreenTargetAddress,
                _dependencies.DebugLog,
                _dependencies.HoldDebugTelemetryAfterSuccessfulInteraction,
                (stage, notes) => ClickDebugPublisher.PublishClickFlowDebugStage(stage, notes),
                () =>
                {
                    bool success = OffscreenTargetResolver.TryResolveOffscreenTargetScreenPointFromPath(out Vector2 targetScreen);
                    return (success, targetScreen);
                },
                target =>
                {
                    bool success = OffscreenTargetResolver.TryResolveOffscreenTargetScreenPoint(target, out Vector2 targetScreen);
                    return (success, targetScreen);
                },
                (targetPath, targetScreen, builtPath) =>
                {
                    bool success = MovementSkills.TryUseMovementSkillForOffscreenPathing(targetPath, targetScreen, builtPath, out Vector2 castPoint, out string debugReason);
                    return (success, castPoint, debugReason);
                },
                LabelInteraction.PerformMechanicClick,
                _dependencies.PointIsInClickableArea,
                _dependencies.IsClickableInEitherSpace,
                _dependencies.IsInsideWindowInEitherSpace,
                _dependencies.ShouldSuppressPathfindingLabel,
                label => _dependencies.LabelFilterService.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => _dependencies.ShrineService.InvalidateCache());

        private ClickRuntimeEngineDependencies CreateClickRuntimeEngineDependencies()
            => new(
                _dependencies.Settings,
                _dependencies.GameController,
                _dependencies.InputHandler,
                _dependencies.LabelFilterService,
                _dependencies.PathfindingService,
                TickContextFactory,
                VisibleMechanics,
                LabelSelection,
                ChestLootSettlement,
                OffscreenPathing,
                AltarAutomation.HasClickableAltars,
                AltarAutomation.ProcessAltarClicking,
                ClickDebugPublisher.PublishClickFlowDebugStage,
                _dependencies.ShouldCaptureClickDebug,
                _dependencies.BuildLabelSourceDebugSummary,
                _dependencies.BuildNoLabelDebugSummary,
                (entity, cursorAbsolute, windowTopLeft) => entity == null ? null : _dependencies.TryGetCursorDistanceSquaredToEntity(entity, cursorAbsolute, windowTopLeft),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                ClickDebugPublisher.PublishLabelClickDebug,
                _dependencies.HoldDebugTelemetryAfterSuccessfulInteraction,
                _dependencies.DebugLog);

        private ClickTickContextFactoryDependencies CreateClickTickContextFactoryDependencies()
            => new(
                getWindowRectangle: () => _dependencies.GameController.Window.GetWindowRectangleTimeCache,
                getCursorAbsolutePosition: ManualCursorSelectionMath.GetCursorAbsolutePosition,
                tryHandleUltimatumPanelUi: _dependencies.TryHandleUltimatumPanelUi,
                debugLog: _dependencies.DebugLog,
                getMovementSkillPostCastBlockState: GetMovementSkillPostCastBlockStateForTickContext,
                getChestLootSettlementBlockState: GetChestLootSettlementBlockStateForTickContext,
                getLabelsForRegularSelection: _dependencies.GetLabelsForRegularSelection,
                tryHandlePendingChestOpenConfirmation: TryHandlePendingChestOpenConfirmationForTickContext,
                resolveNextShrineCandidate: () => VisibleMechanics.ResolveNextShrineCandidate(),
                refreshMechanicPriorityCaches: _dependencies.RefreshMechanicPriorityCaches,
                createMechanicPriorityContext: _dependencies.CreateMechanicPriorityContext,
                groundItemsVisible: _dependencies.GroundItemsVisible,
                publishClickFlowDebugStage: ClickDebugPublisher.PublishClickFlowDebugStage);

        private MovementSkillPostCastBlockState GetMovementSkillPostCastBlockStateForTickContext(long now)
        {
            return MovementSkills.TryGetMovementSkillPostCastBlockState(now, out string reason)
                ? new MovementSkillPostCastBlockState(true, reason)
                : new MovementSkillPostCastBlockState(false, string.Empty);
        }

        private ChestLootSettlementBlockState GetChestLootSettlementBlockStateForTickContext(long now)
        {
            bool isBlocking = ChestLootSettlement.IsPostChestLootSettlementBlocking(now, out string reason);
            return new ChestLootSettlementBlockState(isBlocking, reason);
        }

        private bool TryHandlePendingChestOpenConfirmationForTickContext(Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
            => ChestLootSettlement.TryHandlePendingChestOpenConfirmation(windowTopLeft, allLabels);
    }

    internal sealed record ClickRuntimeCompositionHostDependencies(
        ClickItSettings Settings,
        GameController GameController,
        Utils.ErrorHandler ErrorHandler,
        AltarService AltarService,
        WeightCalculator WeightCalculator,
        Rendering.AltarDisplayRenderer AltarDisplayRenderer,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Utils.InputHandler InputHandler,
        LabelFilterService LabelFilterService,
        ShrineService ShrineService,
        PathfindingService PathfindingService,
        Func<bool> GroundItemsVisible,
        TimeCache<List<LabelOnGround>> CachedLabels,
        Utils.PerformanceMonitor PerformanceMonitor,
        LockedInteractionDispatcher LockedInteractionDispatcher,
        ChestLootSettlementState ChestLootSettlementState,
        ClickRuntimeState RuntimeState,
        Func<ClickLabelInteractionService> GetLabelInteraction,
        Func<IInteractionExecutionRuntime> GetInteractionExecutionRuntime,
        Func<string, bool> EnsureCursorInsideGameWindowForClick,
        Action<string> DebugLog,
        Func<bool> ShouldCaptureClickDebug,
        Action<ClickDebugSnapshot> SetLatestClickDebug,
        Func<bool> ShouldCaptureUltimatumDebug,
        Action<UltimatumDebugEvent> PublishUltimatumDebug,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        Func<LabelOnGround, Vector2, bool> TryCorruptEssence,
        Func<Entity?, Vector2, Vector2, float?> TryGetCursorDistanceSquaredToEntity,
        Func<IReadOnlyList<LabelOnGround>?, int, int, int, string> BuildLabelRangeRejectionDebugSummary,
        Func<IReadOnlyList<LabelOnGround>?, string> BuildLabelSourceDebugSummary,
        Func<IReadOnlyList<LabelOnGround>?, string> BuildNoLabelDebugSummary,
        Action RefreshMechanicPriorityCaches,
        Func<MechanicPriorityContext> CreateMechanicPriorityContext,
        Func<Vector2, bool> TryHandleUltimatumPanelUi,
        Func<IReadOnlyList<LabelOnGround>?> GetLabelsForRegularSelection,
        Func<IReadOnlyList<LabelOnGround>?> GetLabelsForOffscreenSelection,
        Func<LabelOnGround, bool> ShouldSuppressPathfindingLabel,
        Func<LabelOnGround, Vector2, bool> TryClickPreferredUltimatumModifier,
        Action<string> HoldDebugTelemetryAfterSuccessfulInteraction);
}