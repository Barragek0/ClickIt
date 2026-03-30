using System.Collections;
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
        private RegularClickCoordinator? _regularClickCoordinator;

        private LabelSelectionCoordinator LabelSelection => _labelSelectionCoordinator ??= new(CreateLabelSelectionCoordinatorDependencies());

        private ChestLootSettlementTracker ChestLootSettlement => _chestLootSettlementTracker ??= new(CreateChestLootSettlementTrackerDependencies());

        private VisibleMechanicCoordinator VisibleMechanics => _visibleMechanicCoordinator ??= new(CreateVisibleMechanicCoordinatorDependencies());

        private OffscreenPathingCoordinator OffscreenPathing => _offscreenPathingCoordinator ??= new(CreateOffscreenPathingCoordinatorDependencies());

        private MovementSkillCoordinator MovementSkills => _movementSkillCoordinator ??= new(CreateMovementSkillCoordinatorDependencies());

        private RegularClickCoordinator RegularClick => _regularClickCoordinator ??= new(CreateRegularClickCoordinatorDependencies());

        private ClickTickContextFactory TickContextFactory => _tickContextFactory ??= new(CreateClickTickContextFactoryDependencies());

        private ChestLootSettlementTrackerDependencies CreateChestLootSettlementTrackerDependencies()
            => new(
                settings,
                chestLootSettlementState,
                () => VisibleMechanics.CollectGroundLabelEntityAddresses(),
                PublishClickFlowDebugStage,
                TryResolveLabelClickPositionForChestLootSettlement,
                PerformLabelClickForChestLootSettlement);

        private LabelSelectionCoordinatorDependencies CreateLabelSelectionCoordinatorDependencies()
            => new(
                settings,
                gameController,
                labelFilterService,
                inputHandler,
                HasClickableAltars,
                TryClickManualCursorPreferredAltarOptionForLabelSelection,
                TryCorruptEssenceForLabelSelection,
                TryClickPreferredUltimatumModifierForLabelSelection,
                TryResolveLabelClickPositionForLabelSelection,
                PerformManualCursorInteractionForLabelSelection,
                MarkPendingChestOpenConfirmationForLabelSelection,
                () => pathfindingService.ClearLatestPath(),
                message => DebugLog(() => message),
                () => VisibleMechanics.ResolveNextShrineCandidate(),
                ResolveVisibleMechanicCandidatesForLabelSelection,
                entity => VisibleMechanics.HandleSuccessfulMechanicEntityClick(entity),
                () => shrineService.InvalidateCache(),
                TryGetCursorDistanceSquaredToEntityForLabelSelection,
                RefreshMechanicPriorityCaches,
                CreateMechanicPriorityContext,
                ShouldCaptureClickDebug,
                BuildLabelRangeRejectionDebugSummary,
                (stage, notes) => PublishClickFlowDebugStage(stage, notes),
                () => _lastLeverKey,
                value => _lastLeverKey = value,
                () => _lastLeverClickTimestampMs,
                value => _lastLeverClickTimestampMs = value);

        private VisibleMechanicCoordinatorDependencies CreateVisibleMechanicCoordinatorDependencies()
            => new(
                settings,
                gameController,
                shrineService,
                pointIsInClickableArea,
                PerformMechanicClickForVisibleMechanics,
                PerformMechanicInteractionForVisibleMechanics,
                entity => OffscreenPathing.IsStickyTarget(entity),
                () => OffscreenPathing.ClearStickyOffscreenTarget(),
                () => shrineService.InvalidateCache(),
                () => pathfindingService.ClearLatestPath(),
                message => DebugLog(() => message),
                ShouldCaptureClickDebug,
                SetLatestClickDebug,
                IsInsideWindowInEitherSpaceForVisibleMechanics,
                IsClickableInEitherSpaceForVisibleMechanics,
                BuildMechanicRankWithSharedEngine,
                IsSettlersMechanicEnabled);

        private MovementSkillCoordinatorDependencies CreateMovementSkillCoordinatorDependencies()
            => new(
                settings,
                gameController,
                performanceMonitor,
                GetRemainingOffscreenPathNodeCount,
                EnsureCursorInsideGameWindowForClick,
                pointIsInClickableArea,
                message => DebugLog(() => message),
                () => _lastMovementSkillUseTimestampMs,
                value => _lastMovementSkillUseTimestampMs = value,
                () => _movementSkillPostCastClickBlockUntilTimestampMs,
                value => _movementSkillPostCastClickBlockUntilTimestampMs = value,
                () => _movementSkillStatusPollUntilTimestampMs,
                value => _movementSkillStatusPollUntilTimestampMs = value,
                () => _lastUsedMovementSkillEntry,
                value => _lastUsedMovementSkillEntry = value);

        private OffscreenPathingCoordinatorDependencies CreateOffscreenPathingCoordinatorDependencies()
            => new(
                settings,
                gameController,
                labelFilterService,
                shrineService,
                pathfindingService,
                () => _stickyOffscreenTargetAddress,
                value => _stickyOffscreenTargetAddress = value,
                message => DebugLog(() => message),
                (stage, notes) => PublishClickFlowDebugStage(stage, notes),
                HasClickableAltars,
                () => VisibleMechanics.ResolveNextShrineCandidate(),
                ResolveVisibleMechanicCandidatesForOffscreenPathing,
                ResolveOffscreenTargetScreenPointFromPathForOffscreenPathing,
                ResolveOffscreenTargetScreenPointForOffscreenPathing,
                TryUseMovementSkillForOffscreenPathing,
                clickPos => PerformLabelClick(clickPos, null, gameController),
                pointIsInClickableArea,
                IsClickableInEitherSpaceForOffscreenPathing,
                IsInsideWindowInEitherSpaceForOffscreenPathing,
                ShouldSuppressPathfindingLabel,
                label => labelFilterService.GetMechanicIdForLabel(label),
                TryResolveLabelClickPositionForOffscreenPathing,
                ExecuteStickyLabelInteractionForOffscreenPathing,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => shrineService.InvalidateCache(),
                GetLabelsForOffscreenSelection,
                RefreshMechanicPriorityCaches,
                BuildMechanicRankWithSharedEngine);

        private RegularClickCoordinatorDependencies CreateRegularClickCoordinatorDependencies()
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
                ProcessAltarClickingForRegularClick,
                PublishClickFlowDebugStageForRegularClick,
                ShouldCaptureClickDebug,
                BuildLabelSourceDebugSummary,
                BuildNoLabelDebugSummary,
                TryGetCursorDistanceSquaredToEntityForRegularClick,
                TryResolveLabelClickPositionForRegularClick,
                ExecuteVisibleLabelInteractionForRegularClick,
                PublishLabelClickDebugForRegularClick,
                message => DebugLog(() => message));

        private bool PerformMechanicClickForVisibleMechanics(Vector2 clickPos)
            => PerformLabelClick(clickPos, null, gameController);

        private bool PerformMechanicInteractionForVisibleMechanics(Vector2 clickPos, bool useHoldClick)
            => ExecuteLabelInteraction(clickPos, null, gameController, useHoldClick, 0);

        private bool IsInsideWindowInEitherSpaceForVisibleMechanics(Vector2 point)
            => IsInsideWindowInEitherSpace(point);

        private bool IsClickableInEitherSpaceForVisibleMechanics(Vector2 point, string path)
            => IsClickableInEitherSpace(point, path);

        private bool TryClickManualCursorPreferredAltarOptionForLabelSelection(Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft);

        private bool TryCorruptEssenceForLabelSelection(LabelOnGround label, Vector2 windowTopLeft)
            => TryCorruptEssence(label, windowTopLeft);

        private bool TryClickPreferredUltimatumModifierForLabelSelection(LabelOnGround label, Vector2 windowTopLeft)
            => TryClickPreferredUltimatumModifier(label, windowTopLeft);

        private (bool Success, Vector2 ClickPos) TryResolveLabelClickPositionForLabelSelection(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels)
        {
            bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos);
            return (success, clickPos);
        }

        private bool PerformManualCursorInteractionForLabelSelection(Vector2 clickPos, bool useHoldClick)
            => ExecuteLabelInteraction(clickPos, null, gameController, useHoldClick, 0, false, true, true);

        private void MarkPendingChestOpenConfirmationForLabelSelection(string? mechanicId, LabelOnGround label)
            => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label);

        private (LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers) ResolveVisibleMechanicCandidatesForLabelSelection()
        {
            VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
            return (lostShipment, settlers);
        }

        private (LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers) ResolveVisibleMechanicCandidatesForOffscreenPathing()
        {
            VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
            return (lostShipment, settlers);
        }

        private (bool Success, Vector2 TargetScreen) ResolveOffscreenTargetScreenPointFromPathForOffscreenPathing()
        {
            bool success = TryResolveOffscreenTargetScreenPointFromPath(out Vector2 targetScreen);
            return (success, targetScreen);
        }

        private (bool Success, Vector2 TargetScreen) ResolveOffscreenTargetScreenPointForOffscreenPathing(Entity target)
        {
            bool success = TryResolveOffscreenTargetScreenPoint(target, out Vector2 targetScreen);
            return (success, targetScreen);
        }

        private (bool Success, Vector2 CastPoint, string DebugReason) TryUseMovementSkillForOffscreenPathing(string targetPath, Vector2 targetScreen, bool builtPath)
        {
            bool success = MovementSkills.TryUseMovementSkillForOffscreenPathing(targetPath, targetScreen, builtPath, out Vector2 castPoint, out string debugReason);
            return (success, castPoint, debugReason);
        }

        private bool IsClickableInEitherSpaceForOffscreenPathing(Vector2 point, string path)
            => IsClickableInEitherSpace(point, path);

        private bool IsInsideWindowInEitherSpaceForOffscreenPathing(Vector2 point)
            => IsInsideWindowInEitherSpace(point);

        private (bool Success, Vector2 ClickPos) TryResolveLabelClickPositionForOffscreenPathing(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            string? explicitPath)
        {
            bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos, explicitPath);
            return (success, clickPos);
        }

        private bool ExecuteStickyLabelInteractionForOffscreenPathing(Vector2 clickPos, LabelOnGround label, string mechanicId)
            => ExecuteLabelInteraction(
                clickPos,
                label.Label,
                gameController,
                ClickService.ShouldUseHoldClickForSettlersMechanic(mechanicId),
                0,
                ClickService.ShouldForceUiHoverVerificationForLabel(label));

        private IEnumerator ProcessAltarClickingForRegularClick()
            => ProcessAltarClicking();

        private void PublishClickFlowDebugStageForRegularClick(string stage, string notes, string? mechanicId)
            => PublishClickFlowDebugStage(stage, notes, mechanicId);

        private float? TryGetCursorDistanceSquaredToEntityForRegularClick(Entity? entity, Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => entity == null ? null : TryGetCursorDistanceSquaredToEntity(entity, cursorAbsolute, windowTopLeft);

        private (bool Success, Vector2 ClickPos) TryResolveLabelClickPositionForRegularClick(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels)
        {
            bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos);
            return (success, clickPos);
        }

        private bool ExecuteVisibleLabelInteractionForRegularClick(Vector2 clickPos, LabelOnGround label, string? mechanicId)
            => ExecuteLabelInteraction(
                clickPos,
                label.Label,
                gameController,
                ClickService.ShouldUseHoldClickForSettlersMechanic(mechanicId),
                0,
                ClickService.ShouldForceUiHoverVerificationForLabel(label));

        private void PublishLabelClickDebugForRegularClick(
            string stage,
            string? mechanicId,
            LabelOnGround label,
            Vector2 resolvedClickPos,
            bool resolved,
            string notes)
            => PublishLabelClickDebug(stage, mechanicId, label, resolvedClickPos, resolved, notes);

        private float? TryGetCursorDistanceSquaredToEntityForLabelSelection(Entity entity, Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => TryGetCursorDistanceSquaredToEntity(entity, cursorAbsolute, windowTopLeft);

        private (bool Success, Vector2 ClickPos) TryResolveLabelClickPositionForChestLootSettlement(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels)
        {
            bool success = TryResolveLabelClickPosition(label, mechanicId, windowTopLeft, allLabels, out Vector2 clickPos);
            return (success, clickPos);
        }

        private bool PerformLabelClickForChestLootSettlement(Vector2 clickPos, LabelOnGround? label, bool forceUiHoverVerification)
            => PerformLabelClick(clickPos, label?.Label, gameController, forceUiHoverVerification);

        private ClickTickContextFactoryDependencies CreateClickTickContextFactoryDependencies()
            => new(
                getWindowRectangle: () => gameController.Window.GetWindowRectangleTimeCache,
                getCursorAbsolutePosition: GetCursorAbsolutePosition,
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