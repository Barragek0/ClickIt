using System.Collections;
using ClickIt.Services.Click.Application;
using ClickIt.Services.Click.Label;
using ClickIt.Services.Click.Selection;
using ClickIt.Services.Label.Classification.Policies;
using ClickIt.Services.Click.Runtime;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    public partial class ClickService
    {
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

        private AltarAutomationService AltarAutomation => _altarAutomationService ??= new(CreateAltarAutomationServiceDependencies());
        private ClickDebugPublicationService ClickDebugPublisher => _clickDebugPublicationService ??= new(new ClickDebugPublicationServiceDependencies(
            gameController,
            ShouldCaptureClickDebug,
            SetLatestClickDebug,
            IsClickableInEitherSpace,
            IsInsideWindowInEitherSpace));

        private VisibleLabelSnapshotProvider VisibleLabelSnapshots => _visibleLabelSnapshotProvider ??= new(gameController, cachedLabels);

        private LabelSelectionCoordinator LabelSelection => _labelSelectionCoordinator ??= new(CreateLabelSelectionCoordinatorDependencies());

        private ChestLootSettlementTracker ChestLootSettlement => _chestLootSettlementTracker ??= new(CreateChestLootSettlementTrackerDependencies());

        private VisibleMechanicTargetSelector VisibleMechanicSelection => _visibleMechanicTargetSelector ??= new(CreateVisibleMechanicTargetSelectorDependencies());

        private VisibleMechanicCoordinator VisibleMechanics => _visibleMechanicCoordinator ??= new(CreateVisibleMechanicCoordinatorDependencies());

        private OffscreenStickyTargetHandler OffscreenStickyTargets => _offscreenStickyTargetHandler ??= new(CreateOffscreenStickyTargetHandlerDependencies());

        private OffscreenPathingCoordinator OffscreenPathing => _offscreenPathingCoordinator ??= new(CreateOffscreenPathingCoordinatorDependencies());

        private OffscreenMechanicTargetSelector OffscreenTargetSelection => _offscreenMechanicTargetSelector ??= new(CreateOffscreenMechanicTargetSelectorDependencies());

        private MovementSkillCoordinator MovementSkills => _movementSkillCoordinator ??= new(CreateMovementSkillCoordinatorDependencies());

        private ClickRuntimeEngine RegularClick => _clickRuntimeEngine ??= new(CreateClickRuntimeEngineDependencies());

        private OffscreenTargetResolver OffscreenTargetResolver => _offscreenTargetResolver ??= new(gameController, pathfindingService);

        private ClickTickContextFactory TickContextFactory => _tickContextFactory ??= new(CreateClickTickContextFactoryDependencies());

        private AltarAutomationServiceDependencies CreateAltarAutomationServiceDependencies()
            => new(
                settings,
                gameController,
                altarService.GetAltarComponentsReadOnly,
                altarService.RemoveAltarComponentsByElement,
                pc => weightCalculator.CalculateAltarWeights(pc),
                (altar, weights, topModsRect, bottomModsRect, topModsTopLeft) => altarDisplayRenderer.DetermineAltarChoice(altar, weights, topModsRect, bottomModsRect, topModsTopLeft),
                IsClickableInEitherSpace,
                EnsureCursorInsideGameWindowForClick,
                InteractionExecutionRuntime.Execute,
                message => DebugLog(() => message),
                errorHandler.LogError,
                _elementAccessLock);

        private ChestLootSettlementTrackerDependencies CreateChestLootSettlementTrackerDependencies()
            => new(
                settings,
                chestLootSettlementState,
                () => VisibleMechanics.CollectGroundLabelEntityAddresses(),
                ClickDebugPublisher.PublishClickFlowDebugStage,
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformTrackedLabelClick);

        private LabelSelectionCoordinatorDependencies CreateLabelSelectionCoordinatorDependencies()
            => new(
                settings,
                gameController,
                labelFilterService,
                inputHandler,
                AltarAutomation.HasClickableAltars,
                AltarAutomation.TryClickManualCursorPreferredAltarOption,
                TryCorruptEssence,
                TryClickPreferredUltimatumModifier,
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformManualCursorInteraction,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => pathfindingService.ClearLatestPath(),
                message => DebugLog(() => message),
                () => VisibleMechanics.ResolveNextShrineCandidate(),
                () =>
                {
                    VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
                    return (lostShipment, settlers);
                },
                entity => VisibleMechanics.HandleSuccessfulMechanicEntityClick(entity),
                () => shrineService.InvalidateCache(),
                (entity, cursorAbsolute, windowTopLeft) => TryGetCursorDistanceSquaredToEntity(entity, cursorAbsolute, windowTopLeft),
                RefreshMechanicPriorityCaches,
                CreateMechanicPriorityContext,
                ShouldCaptureClickDebug,
                BuildLabelRangeRejectionDebugSummary,
                (stage, notes) => ClickDebugPublisher.PublishClickFlowDebugStage(stage, notes),
                () => _runtimeState.LastLeverKey,
                value => _runtimeState.LastLeverKey = value,
                () => _runtimeState.LastLeverClickTimestampMs,
                value => _runtimeState.LastLeverClickTimestampMs = value);

        private VisibleMechanicTargetSelectorDependencies CreateVisibleMechanicTargetSelectorDependencies()
            => new(
                settings,
                gameController,
                ShouldCaptureClickDebug,
                SetLatestClickDebug,
                message => DebugLog(() => message),
                IsInsideWindowInEitherSpace,
                IsClickableInEitherSpace,
                mechanicId => SettlersMechanicPolicy.IsEnabled(settings, mechanicId),
                () => VisibleMechanics.CollectGroundLabelEntityAddresses());

        private VisibleMechanicCoordinatorDependencies CreateVisibleMechanicCoordinatorDependencies()
            => new(
                settings,
                gameController,
                shrineService,
                VisibleMechanicSelection,
                pointIsInClickableArea,
                LabelInteraction.PerformMechanicClick,
                LabelInteraction.PerformMechanicInteraction,
                entity => OffscreenPathing.IsStickyTarget(entity),
                () => OffscreenPathing.ClearStickyOffscreenTarget(),
                () => shrineService.InvalidateCache(),
                () => pathfindingService.ClearLatestPath(),
                message => DebugLog(() => message),
                reason => HoldDebugTelemetryAfterSuccessfulInteraction(reason),
                ShouldCaptureClickDebug,
                SetLatestClickDebug,
                IsInsideWindowInEitherSpace,
                IsClickableInEitherSpace);

        private MovementSkillCoordinatorDependencies CreateMovementSkillCoordinatorDependencies()
            => new(
                settings,
                gameController,
                performanceMonitor,
                OffscreenTargetResolver.GetRemainingOffscreenPathNodeCount,
                EnsureCursorInsideGameWindowForClick,
                pointIsInClickableArea,
                message => DebugLog(() => message),
                () => _runtimeState.LastMovementSkillUseTimestampMs,
                value => _runtimeState.LastMovementSkillUseTimestampMs = value,
                () => _runtimeState.MovementSkillPostCastClickBlockUntilTimestampMs,
                value => _runtimeState.MovementSkillPostCastClickBlockUntilTimestampMs = value,
                () => _runtimeState.MovementSkillStatusPollUntilTimestampMs,
                value => _runtimeState.MovementSkillStatusPollUntilTimestampMs = value,
                () => _runtimeState.LastUsedMovementSkillEntry,
                value => _runtimeState.LastUsedMovementSkillEntry = value);

        private OffscreenMechanicTargetSelectorDependencies CreateOffscreenMechanicTargetSelectorDependencies()
            => new(
                settings,
                gameController,
                shrineService,
                AltarAutomation.HasClickableAltars,
                () => VisibleMechanics.ResolveNextShrineCandidate() != null,
                () =>
                {
                    VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
                    return (lostShipment.HasValue, settlers.HasValue);
                },
                (stage, notes) => ClickDebugPublisher.PublishClickFlowDebugStage(stage, notes),
                IsClickableInEitherSpace,
                IsInsideWindowInEitherSpace,
                ShouldSuppressPathfindingLabel,
                label => labelFilterService.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                GetLabelsForOffscreenSelection,
                RefreshMechanicPriorityCaches,
                (distance, mechanicId) => CandidateRankingEngine.BuildRank(distance, mechanicId, CreateMechanicPriorityContext()));

        private OffscreenStickyTargetHandlerDependencies CreateOffscreenStickyTargetHandlerDependencies()
            => new(
                gameController,
                shrineService,
                () => _runtimeState.StickyOffscreenTargetAddress,
                value => _runtimeState.StickyOffscreenTargetAddress = value,
                address => EntityQueryService.FindEntityByAddress(gameController, address),
                LabelInteraction.PerformMechanicClick,
                IsClickableInEitherSpace,
                ShouldSuppressPathfindingLabel,
                label => labelFilterService.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                reason => HoldDebugTelemetryAfterSuccessfulInteraction(reason),
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => shrineService.InvalidateCache());

        private OffscreenPathingCoordinatorDependencies CreateOffscreenPathingCoordinatorDependencies()
            => new(
                settings,
                gameController,
                pathfindingService,
                OffscreenTargetSelection,
                OffscreenStickyTargets,
                () => _runtimeState.StickyOffscreenTargetAddress,
                message => DebugLog(() => message),
                reason => HoldDebugTelemetryAfterSuccessfulInteraction(reason),
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
                pointIsInClickableArea,
                IsClickableInEitherSpace,
                IsInsideWindowInEitherSpace,
                ShouldSuppressPathfindingLabel,
                label => labelFilterService.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => shrineService.InvalidateCache());

        private ClickRuntimeEngineDependencies CreateClickRuntimeEngineDependencies()
            => new(
                settings,
                gameController,
                inputHandler,
                labelFilterService,
                pathfindingService,
                TickContextFactory,
                VisibleMechanics,
                LabelSelection,
                ChestLootSettlement,
                OffscreenPathing,
                AltarAutomation.HasClickableAltars,
                AltarAutomation.ProcessAltarClicking,
                ClickDebugPublisher.PublishClickFlowDebugStage,
                ShouldCaptureClickDebug,
                BuildLabelSourceDebugSummary,
                BuildNoLabelDebugSummary,
                (entity, cursorAbsolute, windowTopLeft) => entity == null ? null : TryGetCursorDistanceSquaredToEntity(entity, cursorAbsolute, windowTopLeft),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                ClickDebugPublisher.PublishLabelClickDebug,
                reason => HoldDebugTelemetryAfterSuccessfulInteraction(reason),
                message => DebugLog(() => message));

        private ClickTickContextFactoryDependencies CreateClickTickContextFactoryDependencies()
            => new(
                getWindowRectangle: () => gameController.Window.GetWindowRectangleTimeCache,
                getCursorAbsolutePosition: ManualCursorSelectionMath.GetCursorAbsolutePosition,
                tryHandleUltimatumPanelUi: TryHandleUltimatumPanelUi,
                debugLog: message => DebugLog(() => message),
                getMovementSkillPostCastBlockState: GetMovementSkillPostCastBlockStateForTickContext,
                getChestLootSettlementBlockState: GetChestLootSettlementBlockStateForTickContext,
                getLabelsForRegularSelection: GetLabelsForRegularSelection,
                tryHandlePendingChestOpenConfirmation: TryHandlePendingChestOpenConfirmationForTickContext,
                resolveNextShrineCandidate: () => VisibleMechanics.ResolveNextShrineCandidate(),
                refreshMechanicPriorityCaches: RefreshMechanicPriorityCaches,
                createMechanicPriorityContext: CreateMechanicPriorityContext,
                groundItemsVisible: groundItemsVisible,
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
}