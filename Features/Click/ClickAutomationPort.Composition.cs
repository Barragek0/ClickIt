namespace ClickIt.Features.Click
{
    public sealed partial class ClickAutomationPort
    {
        internal ClickAutomationSupport ClickAutomationSupport { get; }

        internal LockedInteractionDispatcher LockedInteractionDispatcher { get; }

        private IInteractionExecutionRuntime InteractionExecutionRuntime => field ??= new InteractionExecutionRuntime(new InteractionExecutionRuntimeDependencies(
            ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
            point => ForceRefreshPointIsInClickableArea(point, string.Empty),
            ClickAutomationSupport.DebugLog,
            LockedInteractionDispatcher.PerformClick,
            LockedInteractionDispatcher.PerformHoldClick,
            _performanceMonitor.RecordClickInterval));

        private AltarAutomationService AltarAutomation => field ??= new(new AltarAutomationServiceDependencies(
            _settings,
            _gameController,
            _altarService.GetAltarComponentsReadOnly,
            _altarService.RemoveAltarComponentsByElement,
            _weightCalculator.CalculateAltarWeights,
            (altar, weights, topModsRect, bottomModsRect, topModsTopLeft) => _altarChoiceEvaluator.DetermineChoiceElement(altar, weights, topModsRect, bottomModsRect, _altarService.DebugInfo.AddDebugStage),
            ClickAutomationSupport.IsClickableInEitherSpace,
            ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
            InteractionExecutionRuntime.Execute,
            ClickAutomationSupport.DebugLog,
            _errorHandler.LogError,
            LockedInteractionDispatcher.ElementLock,
            _altarService.DebugInfo.AddDebugStage));

        private ClickDebugPublicationService ClickDebugPublisher => field ??= new(new ClickDebugPublicationServiceDependencies(
            _gameController,
            ClickAutomationSupport.ShouldCaptureClickDebug,
            ClickAutomationSupport.PublishClickSnapshot,
            ClickAutomationSupport.IsClickableInEitherSpace,
            ClickAutomationSupport.IsInsideWindowInEitherSpace));

        private GroundLabelEntityAddressProvider GroundLabelEntityAddresses => field ??= new(() => _gameController.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels);

        private VisibleLabelSnapshotProvider VisibleLabelSnapshots => field ??= new(_cachedLabels);

        private PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression => field ??= new(new PathfindingLabelSuppressionEvaluatorDependencies(
            _settings,
            _runtimeState));

        private UltimatumAutomationService UltimatumAutomation => field ??= new(new UltimatumAutomationServiceDependencies(
            _settings,
            _gameController,
            _cachedLabels,
            ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
            ClickAutomationSupport.IsClickableInEitherSpace,
            ClickAutomationSupport.DebugLog,
            (clickPos, clickElement) => { LockedInteractionDispatcher.PerformClick(clickPos, clickElement, _gameController); },
            _performanceMonitor.RecordClickInterval,
            ClickAutomationSupport.ShouldCaptureUltimatumDebug,
            ClickAutomationSupport.PublishUltimatumEvent));

        private ClickLabelInteractionService LabelInteraction => field ??= new(new ClickLabelInteractionServiceDependencies(
            _settings,
            _gameController,
            _labelInteractionPort,
            (label, windowTopLeft, allLabels, isClickableArea) =>
                (_labelClickPointResolver.TryCalculateClickPosition(label, windowTopLeft, allLabels, isClickableArea, out Vector2 clickPos), clickPos),
            ClickAutomationSupport.IsClickableInEitherSpace,
            ClickAutomationSupport.IsInsideWindowInEitherSpace,
            InteractionExecutionRuntime.Execute,
            _groundItemsVisible,
            ClickAutomationSupport.DebugLog,
            BlightChestDebug,
            message => _errorHandler.LogError(message)));

        private ChestLootSettlementTracker ChestLootSettlement => field ??= new(new ChestLootSettlementTrackerDependencies(
            _settings,
            _chestLootSettlementState,
            GroundLabelEntityAddresses,
            ClickDebugPublisher,
            LabelInteraction,
            BlightChestTransitionSuppression.ShouldSuppressBlightChestClick));

        private OffscreenStickyTargetHandler OffscreenStickyTargets => field ??= new(new OffscreenStickyTargetHandlerDependencies(
            _gameController,
            _shrineService,
            _runtimeState,
            LabelInteraction,
            ChestLootSettlement,
            ClickAutomationSupport.IsClickableInEitherSpace,
            PathfindingLabelSuppression,
            _labelInteractionPort,
            ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction));

        private OnscreenMechanicPathingBlocker OnscreenMechanicPathingBlocker => field ??= new(new OnscreenMechanicPathingBlockerDependencies(
            _settings,
            AltarAutomation,
            VisibleMechanics,
            ClickDebugPublisher));

        private OffscreenTraversalTargetResolver OffscreenTraversalTargets => field ??= new(new OffscreenTraversalTargetResolverDependencies(
            _settings,
            _gameController,
            _mechanicPriorityContextProvider,
            LabelInteraction,
            _labelInteractionPort,
            VisibleLabelSnapshots,
            ClickAutomationSupport.IsClickableInEitherSpace,
            ClickAutomationSupport.IsInsideWindowInEitherSpace,
            PathfindingLabelSuppression,
            DebugLog: ClickAutomationSupport.DebugLog,
            IsLabelFullyOverlapped: _labelClickPointResolver.IsLabelFullyOverlapped));

        private OffscreenPathingCoordinator OffscreenPathing => field ??= new(new OffscreenPathingCoordinatorDependencies(
            _settings,
            _gameController,
            _pathfindingService,
            OnscreenMechanicPathingBlocker,
            OffscreenTraversalTargets,
            OffscreenStickyTargets,
            MovementSkills,
            LabelInteraction,
            ClickAutomationSupport.DebugLog,
            ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction,
            ClickDebugPublisher,
            PointIsInClickableArea,
            _clickSuccessAnchor,
            pos => IsBlightBuildOrUpgradeIconAt(pos)));

        private ClickRuntimeEngine RegularClick => field ??= new(new ClickRuntimeEngineDependencies(
            Telemetry: new ClickTelemetryDependencies(
                ClickDebugPublisher,
                ClickAutomationSupport.ShouldCaptureClickDebug,
                ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction,
                ClickAutomationSupport.DebugLog,
                breakdown => _performanceMonitor.RecordClickAllocation(breakdown),
                (index, bytes, ms) => _performanceMonitor.RecordBreakdownStage(ProcessingSection.Click, index, bytes, ms)),
            Policy: new ClickPolicyDependencies(_settings, _inputHandler, PointIsInClickableArea, _clickSuccessAnchor),
            Selection: new ClickSelectionDependencies(
                TickContextFactory, _labelInteractionPort, VisibleMechanics, LabelSelectionScan,
                SpecialLabelInteraction, LabelInteraction, ChestLootSettlement),
            Pathing: new ClickPathingDependencies(_pathfindingService, PathfindingLabelSuppression, OffscreenPathing),
            Mechanics: new ClickMechanicDependencies(
                AltarAutomation, GetHarvestLabelToClick, TryProgressBlightBuilding, GetBlightPathfindTarget, IsBlightEncounterActive)));

        private MovementSkillCoordinator MovementSkills => field ??= new(new MovementSkillCoordinatorDependencies(
            _settings,
            _gameController,
            _runtimeState,
            _performanceMonitor,
            () => OffscreenPathing.GetRemainingOffscreenPathNodeCount(),
            ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
            PointIsInClickableArea,
            ClickAutomationSupport.DebugLog));

        private ClickTickContextFactory TickContextFactory => field ??= new(new ClickTickContextFactoryDependencies(
            getWindowRectangle: () => _gameController.Window.GetWindowRectangleTimeCache,
            getCursorAbsolutePosition: ManualCursorSelectionMath.GetCursorAbsolutePosition,
            tryHandleUltimatumPanelUi: TryHandleUltimatumPanelUi,
            debugLog: ClickAutomationSupport.DebugLog,
            movementSkills: MovementSkills,
            chestLootSettlement: ChestLootSettlement,
            getLabelsForRegularSelection: GetLabelsForRegularSelection,
            visibleMechanics: VisibleMechanics,
            mechanicPriorityContextProvider: _mechanicPriorityContextProvider,
            groundItemsVisible: _groundItemsVisible,
            clickDebugPublisher: ClickDebugPublisher));

        private SpecialLabelInteractionHandler SpecialLabelInteraction => field ??= new(new SpecialLabelInteractionHandlerDependencies(
            _settings,
            AltarAutomation,
            LabelInteraction,
            UltimatumAutomation,
            ClickAutomationSupport.DebugLog));

        private ManualCursorLabelInteractionHandler ManualCursorLabelInteraction => field ??= new(new ManualCursorLabelInteractionHandlerDependencies(
            _settings,
            AltarAutomation,
            LabelInteraction,
            ChestLootSettlement,
            PathfindingLabelSuppression,
            _pathfindingService,
            UltimatumAutomation));

        private LabelSelectionScanEngine LabelSelectionScan => field ??= new(new LabelSelectionScanEngineDependencies(
            _gameController,
            _labelInteractionPort,
            _labelSelectionService,
            _labelClickPointResolver,
            PathfindingLabelSuppression.ShouldSuppressLeverClick,
            UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel,
            BlightChestTransitionSuppression.ShouldSuppressBlightChestClick,
            LabelInteraction,
            _mechanicPriorityContextProvider,
            ClickDebugPublisher,
            ClickAutomationSupport.DebugLog)
        {
            IsEssenceClickingEnabled = () => _settings.ClickEssences.Value,
            IsStrongboxClickingEnabled = () => _settings.ClickStrongboxes.Value,
            ShouldSuppressLockedStrongboxClick = LockedStrongboxLabelSuppression.ShouldSuppress
        });

        private BlightChestTransitionSuppression BlightChestTransitionSuppression => field ??= new();

        private ManualCursorLabelSelector ManualCursorLabels => field ??= new(new ManualCursorLabelSelectorDependencies(
            _gameController,
            _labelInteractionPort,
            PathfindingLabelSuppression,
            _labelClickPointResolver));

        private ManualCursorVisibleMechanicSelector ManualCursorVisibleMechanics => field ??= new(new ManualCursorVisibleMechanicSelectorDependencies(
            _gameController,
            VisibleMechanics,
            LabelInteraction));

        private LostShipmentTargetSelector LostShipmentTargets => field ??= new(new LostShipmentTargetSelectorDependencies(
            _settings,
            _gameController,
            ClickAutomationSupport.DebugLog,
            ClickAutomationSupport.IsInsideWindowInEitherSpace,
            ClickAutomationSupport.IsClickableInEitherSpace));

        private SettlersOreTargetSelector SettlersOreTargets => field ??= new(new SettlersOreTargetSelectorDependencies(
            _settings,
            _gameController,
            ClickDebugPublisher,
            ClickAutomationSupport.DebugLog,
            ClickAutomationSupport.IsInsideWindowInEitherSpace,
            ClickAutomationSupport.IsClickableInEitherSpace,
            GroundLabelEntityAddresses));

        private VisibleMechanicCoordinator VisibleMechanics => field ??= new(new VisibleMechanicCoordinatorDependencies(
            _settings,
            _gameController,
            _shrineService,
            LostShipmentTargets,
            SettlersOreTargets,
            PointIsInClickableArea,
            LabelInteraction,
            OffscreenStickyTargets,
            _pathfindingService,
            ClickAutomationSupport.DebugLog,
            ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction,
            ClickDebugPublisher));
    }
}