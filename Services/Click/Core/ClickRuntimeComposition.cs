using System.Collections;
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
        private LabelSelectionCoordinator? _labelSelectionCoordinator;
        private ChestLootSettlementTracker? _chestLootSettlementTracker;
        private VisibleMechanicCoordinator? _visibleMechanicCoordinator;
        private OffscreenPathingCoordinator? _offscreenPathingCoordinator;
        private MovementSkillCoordinator? _movementSkillCoordinator;
        private ClickRuntimeEngine? _clickRuntimeEngine;
        private OffscreenTargetResolver? _offscreenTargetResolver;

        private LabelSelectionCoordinator LabelSelection => _labelSelectionCoordinator ??= new(CreateLabelSelectionCoordinatorDependencies());

        private ChestLootSettlementTracker ChestLootSettlement => _chestLootSettlementTracker ??= new(CreateChestLootSettlementTrackerDependencies());

        private VisibleMechanicCoordinator VisibleMechanics => _visibleMechanicCoordinator ??= new(CreateVisibleMechanicCoordinatorDependencies());

        private OffscreenPathingCoordinator OffscreenPathing => _offscreenPathingCoordinator ??= new(CreateOffscreenPathingCoordinatorDependencies());

        private MovementSkillCoordinator MovementSkills => _movementSkillCoordinator ??= new(CreateMovementSkillCoordinatorDependencies());

        private ClickRuntimeEngine RegularClick => _clickRuntimeEngine ??= new(CreateClickRuntimeEngineDependencies());

        private OffscreenTargetResolver OffscreenTargetResolver => _offscreenTargetResolver ??= new(gameController, pathfindingService);

        private ClickTickContextFactory TickContextFactory => _tickContextFactory ??= new(CreateClickTickContextFactoryDependencies());

        private ChestLootSettlementTrackerDependencies CreateChestLootSettlementTrackerDependencies()
            => new(
                settings,
                chestLootSettlementState,
                () => VisibleMechanics.CollectGroundLabelEntityAddresses(),
                PublishClickFlowDebugStage,
                (label, mechanicId, windowTopLeft, allLabels) =>
                {
                    bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos);
                    return (success, clickPos);
                },
                (clickPos, label, forceUiHoverVerification) => PerformLabelClick(clickPos, label?.Label, gameController, forceUiHoverVerification));

        private LabelSelectionCoordinatorDependencies CreateLabelSelectionCoordinatorDependencies()
            => new(
                settings,
                gameController,
                labelFilterService,
                inputHandler,
                HasClickableAltars,
                TryClickManualCursorPreferredAltarOption,
                TryCorruptEssence,
                TryClickPreferredUltimatumModifier,
                (label, mechanicId, windowTopLeft, allLabels) =>
                {
                    bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos);
                    return (success, clickPos);
                },
                (clickPos, useHoldClick) => ExecuteLabelInteraction(clickPos, null, gameController, useHoldClick, 0, false, true, true),
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
                (stage, notes) => PublishClickFlowDebugStage(stage, notes),
                () => _runtimeState.LastLeverKey,
                value => _runtimeState.LastLeverKey = value,
                () => _runtimeState.LastLeverClickTimestampMs,
                value => _runtimeState.LastLeverClickTimestampMs = value);

        private VisibleMechanicCoordinatorDependencies CreateVisibleMechanicCoordinatorDependencies()
            => new(
                settings,
                gameController,
                shrineService,
                pointIsInClickableArea,
                clickPos => PerformLabelClick(clickPos, null, gameController),
                (clickPos, useHoldClick) => ExecuteLabelInteraction(clickPos, null, gameController, useHoldClick, 0),
                entity => OffscreenPathing.IsStickyTarget(entity),
                () => OffscreenPathing.ClearStickyOffscreenTarget(),
                () => shrineService.InvalidateCache(),
                () => pathfindingService.ClearLatestPath(),
                message => DebugLog(() => message),
                reason => HoldDebugTelemetryAfterSuccessfulInteraction(reason),
                ShouldCaptureClickDebug,
                SetLatestClickDebug,
                IsInsideWindowInEitherSpace,
                IsClickableInEitherSpace,
                mechanicId => SettlersMechanicPolicy.IsEnabled(settings, mechanicId));

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

        private OffscreenPathingCoordinatorDependencies CreateOffscreenPathingCoordinatorDependencies()
            => new(
                settings,
                gameController,
                labelFilterService,
                shrineService,
                pathfindingService,
                () => _runtimeState.StickyOffscreenTargetAddress,
                value => _runtimeState.StickyOffscreenTargetAddress = value,
                message => DebugLog(() => message),
                reason => HoldDebugTelemetryAfterSuccessfulInteraction(reason),
                (stage, notes) => PublishClickFlowDebugStage(stage, notes),
                HasClickableAltars,
                () => VisibleMechanics.ResolveNextShrineCandidate(),
                () =>
                {
                    VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
                    return (lostShipment, settlers);
                },
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
                clickPos => PerformLabelClick(clickPos, null, gameController),
                pointIsInClickableArea,
                IsClickableInEitherSpace,
                IsInsideWindowInEitherSpace,
                ShouldSuppressPathfindingLabel,
                label => labelFilterService.GetMechanicIdForLabel(label),
                (label, mechanicId, windowTopLeft, allLabels, explicitPath) =>
                {
                    bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos, explicitPath);
                    return (success, clickPos);
                },
                (clickPos, label, mechanicId) => ExecuteLabelInteraction(
                    clickPos,
                    label.Label,
                    gameController,
                    SettlersMechanicPolicy.RequiresHoldClick(mechanicId),
                    0,
                    OffscreenPathingMath.ShouldForceUiHoverVerificationForLabel(label)),
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => shrineService.InvalidateCache(),
                GetLabelsForOffscreenSelection,
                RefreshMechanicPriorityCaches,
                (distance, mechanicId) => CandidateRankingEngine.BuildRank(distance, mechanicId, CreateMechanicPriorityContext()));

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
                HasClickableAltars,
                ProcessAltarClicking,
                PublishClickFlowDebugStage,
                ShouldCaptureClickDebug,
                BuildLabelSourceDebugSummary,
                BuildNoLabelDebugSummary,
                (entity, cursorAbsolute, windowTopLeft) => entity == null ? null : TryGetCursorDistanceSquaredToEntity(entity, cursorAbsolute, windowTopLeft),
                (label, mechanicId, windowTopLeft, allLabels) =>
                {
                    bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos);
                    return (success, clickPos);
                },
                (clickPos, label, mechanicId) => ExecuteLabelInteraction(
                    clickPos,
                    label.Label,
                    gameController,
                    SettlersMechanicPolicy.RequiresHoldClick(mechanicId),
                    0,
                    OffscreenPathingMath.ShouldForceUiHoverVerificationForLabel(label)),
                PublishLabelClickDebug,
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
                publishClickFlowDebugStage: PublishClickFlowDebugStage);

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