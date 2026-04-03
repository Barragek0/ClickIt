namespace ClickIt.Features.Click
{
    public sealed partial class ClickAutomationPort
    {
        private IInteractionExecutionRuntime InteractionExecutionRuntime => _interactionExecutionRuntime ??= new InteractionExecutionRuntime(
            new InteractionExecutionRuntimeDependencies(
                EnsureCursorInsideGameWindowForClick,
                _lockedInteractionDispatcher.PerformClick,
                _lockedInteractionDispatcher.PerformHoldClick,
                _performanceMonitor.RecordClickInterval));

        private AltarAutomationService AltarAutomation => _altarAutomationService ??= new(CreateAltarAutomationServiceDependencies());

        private ClickDebugPublicationService ClickDebugPublisher => _clickDebugPublicationService ??= new(new ClickDebugPublicationServiceDependencies(
            _gameController,
            ShouldCaptureClickDebug,
            PublishClickSnapshot,
            IsClickableInEitherSpace,
            IsInsideWindowInEitherSpace));

        private VisibleLabelSnapshotProvider VisibleLabelSnapshots => _visibleLabelSnapshotProvider ??= new(_gameController, _cachedLabels);

        private LabelSelectionCoordinator LabelSelection => _labelSelectionCoordinator ??= new(CreateLabelSelectionCoordinatorDependencies());

        private ChestLootSettlementTracker ChestLootSettlement => _chestLootSettlementTracker ??= new(CreateChestLootSettlementTrackerDependencies());

        private VisibleMechanicCoordinator VisibleMechanics => _visibleMechanicCoordinator ??= new(CreateVisibleMechanicCoordinatorDependencies());

        private OffscreenPathingCoordinator OffscreenPathing => _offscreenPathingCoordinator ??= new(CreateOffscreenPathingCoordinatorDependencies());

        private ClickRuntimeEngine RegularClick => _clickRuntimeEngine ??= new(CreateClickRuntimeEngineDependencies());

        private VisibleMechanicTargetSelector VisibleMechanicSelection => _visibleMechanicTargetSelector ??= new(CreateVisibleMechanicTargetSelectorDependencies());

        private OffscreenStickyTargetHandler OffscreenStickyTargets => _offscreenStickyTargetHandler ??= new(CreateOffscreenStickyTargetHandlerDependencies());

        private OffscreenMechanicTargetSelector OffscreenTargetSelection => _offscreenMechanicTargetSelector ??= new(CreateOffscreenMechanicTargetSelectorDependencies());

        private MovementSkillCoordinator MovementSkills => _movementSkillCoordinator ??= new(CreateMovementSkillCoordinatorDependencies());

        private OffscreenTargetResolver OffscreenTargetResolver => _offscreenTargetResolver ??= new(_gameController, _pathfindingService);

        private ClickTickContextFactory TickContextFactory => _tickContextFactory ??= new(CreateClickTickContextFactoryDependencies());

        private UltimatumAutomationService UltimatumAutomation => _ultimatumAutomationService ??= new(
            new UltimatumAutomationServiceDependencies(
                _settings,
                _gameController,
                _cachedLabels,
                EnsureCursorInsideGameWindowForClick,
                IsClickableInEitherSpace,
                messageFactory => DebugLog(messageFactory()),
                (clickPos, clickElement) => _lockedInteractionDispatcher.PerformClick(clickPos, clickElement, _gameController),
                _performanceMonitor.RecordClickInterval,
                ShouldCaptureUltimatumDebug,
                PublishUltimatumDebug));

        private ClickLabelInteractionService LabelInteraction => _labelInteractionService ??= new(
            new ClickLabelInteractionServiceDependencies(
                _settings,
                _gameController,
                _inputHandler,
                _labelInteractionPort,
                IsClickableInEitherSpace,
                IsInsideWindowInEitherSpace,
                InteractionExecutionRuntime.Execute,
                _groundItemsVisible,
                messageFactory => DebugLog(messageFactory())));

        private AltarAutomationServiceDependencies CreateAltarAutomationServiceDependencies()
            => new(
                _settings,
                _gameController,
                _altarService.GetAltarComponentsReadOnly,
                _altarService.RemoveAltarComponentsByElement,
                pc => _weightCalculator.CalculateAltarWeights(pc),
                (altar, weights, topModsRect, bottomModsRect, topModsTopLeft) => _altarDisplayRenderer.DetermineAltarChoice(altar, weights, topModsRect, bottomModsRect, topModsTopLeft),
                IsClickableInEitherSpace,
                EnsureCursorInsideGameWindowForClick,
                InteractionExecutionRuntime.Execute,
                DebugLog,
                _errorHandler.LogError,
                _lockedInteractionDispatcher.ElementLock);

        private ChestLootSettlementTrackerDependencies CreateChestLootSettlementTrackerDependencies()
            => new(
                _settings,
                _chestLootSettlementState,
                () => VisibleMechanics.CollectGroundLabelEntityAddresses(),
                ClickDebugPublisher.PublishClickFlowDebugStage,
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformTrackedLabelClick);

        private LabelSelectionCoordinatorDependencies CreateLabelSelectionCoordinatorDependencies()
            => new(
                _settings,
                _gameController,
                _labelInteractionPort,
                _inputHandler,
                AltarAutomation.HasClickableAltars,
                AltarAutomation.TryClickManualCursorPreferredAltarOption,
                LabelInteraction.TryCorruptEssence,
                TryClickPreferredUltimatumModifier,
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformManualCursorInteraction,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => _pathfindingService.ClearLatestPath(),
                DebugLog,
                () => VisibleMechanics.ResolveNextShrineCandidate(),
                () =>
                {
                    VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
                    return (lostShipment, settlers);
                },
                entity => VisibleMechanics.HandleSuccessfulMechanicEntityClick(entity),
                () => _shrineService.InvalidateCache(),
                LabelInteraction.TryGetCursorDistanceSquaredToEntity,
                _mechanicPriorityContextProvider.Refresh,
                _mechanicPriorityContextProvider.CreateContext,
                ShouldCaptureClickDebug,
                LabelInteraction.BuildLabelRangeRejectionDebugSummary,
                (stage, notes) => ClickDebugPublisher.PublishClickFlowDebugStage(stage, notes),
                () => _runtimeState.LastLeverKey,
                value => _runtimeState.LastLeverKey = value,
                () => _runtimeState.LastLeverClickTimestampMs,
                value => _runtimeState.LastLeverClickTimestampMs = value);

        private VisibleMechanicTargetSelectorDependencies CreateVisibleMechanicTargetSelectorDependencies()
            => new(
                _settings,
                _gameController,
                ShouldCaptureClickDebug,
                PublishClickSnapshot,
                DebugLog,
                IsInsideWindowInEitherSpace,
                IsClickableInEitherSpace,
                mechanicId => SettlersMechanicPolicy.IsEnabled(_settings, mechanicId),
                () => VisibleMechanics.CollectGroundLabelEntityAddresses());

        private VisibleMechanicCoordinatorDependencies CreateVisibleMechanicCoordinatorDependencies()
            => new(
                _settings,
                _gameController,
                _shrineService,
                VisibleMechanicSelection,
                _pointIsInClickableArea,
                LabelInteraction.PerformMechanicClick,
                LabelInteraction.PerformMechanicInteraction,
                entity => OffscreenPathing.IsStickyTarget(entity),
                () => OffscreenPathing.ClearStickyOffscreenTarget(),
                () => _shrineService.InvalidateCache(),
                () => _pathfindingService.ClearLatestPath(),
                DebugLog,
                HoldDebugTelemetryAfterSuccessfulInteraction,
                ShouldCaptureClickDebug,
                PublishClickSnapshot,
                IsInsideWindowInEitherSpace,
                IsClickableInEitherSpace);

        private MovementSkillCoordinatorDependencies CreateMovementSkillCoordinatorDependencies()
            => new(
                _settings,
                _gameController,
                _performanceMonitor,
                OffscreenTargetResolver.GetRemainingOffscreenPathNodeCount,
                EnsureCursorInsideGameWindowForClick,
                _pointIsInClickableArea,
                DebugLog,
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
                _settings,
                _gameController,
                _shrineService,
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
                label => ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(
                    LabelSelection.ShouldSuppressLeverClick(label),
                    UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(label)),
                label => _labelInteractionPort.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                GetLabelsForOffscreenSelection,
                _mechanicPriorityContextProvider.Refresh,
                (distance, mechanicId) => CandidateRankingEngine.BuildRank(distance, mechanicId, _mechanicPriorityContextProvider.CreateContext()));

        private OffscreenStickyTargetHandlerDependencies CreateOffscreenStickyTargetHandlerDependencies()
            => new(
                _gameController,
                _shrineService,
                () => _runtimeState.StickyOffscreenTargetAddress,
                value => _runtimeState.StickyOffscreenTargetAddress = value,
                address => EntityQueryService.FindEntityByAddress(_gameController, address),
                LabelInteraction.PerformMechanicClick,
                IsClickableInEitherSpace,
                label => ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(
                    LabelSelection.ShouldSuppressLeverClick(label),
                    UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(label)),
                label => _labelInteractionPort.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                HoldDebugTelemetryAfterSuccessfulInteraction,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => _shrineService.InvalidateCache());

        private OffscreenPathingCoordinatorDependencies CreateOffscreenPathingCoordinatorDependencies()
            => new(
                _settings,
                _gameController,
                _pathfindingService,
                OffscreenTargetSelection,
                OffscreenStickyTargets,
                () => _runtimeState.StickyOffscreenTargetAddress,
                DebugLog,
                HoldDebugTelemetryAfterSuccessfulInteraction,
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
                _pointIsInClickableArea,
                IsClickableInEitherSpace,
                IsInsideWindowInEitherSpace,
                label => ClickLabelSelectionMath.ShouldSuppressPathfindingLabel(
                    LabelSelection.ShouldSuppressLeverClick(label),
                    UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel(label)),
                label => _labelInteractionPort.GetMechanicIdForLabel(label),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                (mechanicId, label) => ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, label),
                () => _shrineService.InvalidateCache());

        private ClickRuntimeEngineDependencies CreateClickRuntimeEngineDependencies()
            => new(
                _settings,
                _gameController,
                _inputHandler,
                _labelInteractionPort,
                _pathfindingService,
                TickContextFactory,
                VisibleMechanics,
                LabelSelection,
                ChestLootSettlement,
                OffscreenPathing,
                AltarAutomation.HasClickableAltars,
                AltarAutomation.ProcessAltarClicking,
                ClickDebugPublisher.PublishClickFlowDebugStage,
                ShouldCaptureClickDebug,
                LabelInteraction.BuildLabelSourceDebugSummary,
                LabelInteraction.BuildNoLabelDebugSummary,
                (entity, cursorAbsolute, windowTopLeft) => entity == null ? null : LabelInteraction.TryGetCursorDistanceSquaredToEntity(entity, cursorAbsolute, windowTopLeft),
                LabelInteraction.TryResolveLabelClickPositionResult,
                LabelInteraction.PerformResolvedLabelInteraction,
                ClickDebugPublisher.PublishLabelClickDebug,
                HoldDebugTelemetryAfterSuccessfulInteraction,
                DebugLog);

        private ClickTickContextFactoryDependencies CreateClickTickContextFactoryDependencies()
            => new(
                getWindowRectangle: () => _gameController.Window.GetWindowRectangleTimeCache,
                getCursorAbsolutePosition: ManualCursorSelectionMath.GetCursorAbsolutePosition,
                tryHandleUltimatumPanelUi: TryHandleUltimatumPanelUi,
                debugLog: DebugLog,
                getMovementSkillPostCastBlockState: GetMovementSkillPostCastBlockStateForTickContext,
                getChestLootSettlementBlockState: GetChestLootSettlementBlockStateForTickContext,
                getLabelsForRegularSelection: GetLabelsForRegularSelection,
                tryHandlePendingChestOpenConfirmation: TryHandlePendingChestOpenConfirmationForTickContext,
                resolveNextShrineCandidate: () => VisibleMechanics.ResolveNextShrineCandidate(),
                refreshMechanicPriorityCaches: _mechanicPriorityContextProvider.Refresh,
                createMechanicPriorityContext: _mechanicPriorityContextProvider.CreateContext,
                groundItemsVisible: _groundItemsVisible,
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