namespace ClickIt.Features.Click
{
    public sealed partial class ClickAutomationPort
    {
        private readonly record struct OffscreenTraversalServices(
            OnscreenMechanicPathingBlocker PathingBlocker,
            OffscreenTraversalTargetResolver TraversalTargets,
            OffscreenStickyTargetHandler StickyTargets,
            OffscreenTargetResolver TargetResolver,
            MovementSkillCoordinator MovementSkills,
            ClickLabelInteractionService LabelInteraction,
            ClickDebugPublicationService ClickDebugPublisher);

        private readonly record struct RuntimeInteractionServices(
            ClickTickContextFactory TickContextFactory,
            AltarAutomationService AltarAutomation,
            ClickDebugPublicationService ClickDebugPublisher,
            VisibleMechanicCoordinator VisibleMechanics,
            LabelSelectionScanEngine LabelSelectionScan,
            SpecialLabelInteractionHandler SpecialLabelInteraction,
            ClickLabelInteractionService LabelInteraction,
            PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
            ChestLootSettlementTracker ChestLootSettlement,
            OffscreenPathingCoordinator OffscreenPathing);

        private readonly record struct VisibleMechanicServices(
            LostShipmentTargetSelector LostShipmentTargets,
            SettlersOreTargetSelector SettlersOreTargets,
            ClickLabelInteractionService LabelInteraction,
            OffscreenStickyTargetHandler OffscreenStickyTargets,
            ClickDebugPublicationService ClickDebugPublisher);

        internal ClickAutomationSupport ClickAutomationSupport { get; }

        internal LockedInteractionDispatcher LockedInteractionDispatcher { get; }

        private IInteractionExecutionRuntime InteractionExecutionRuntime => field ??= new InteractionExecutionRuntime(CreateInteractionExecutionRuntimeDependencies());

        private AltarAutomationService AltarAutomation => field ??= new(CreateAltarAutomationServiceDependencies());

        private ClickDebugPublicationService ClickDebugPublisher => field ??= new(CreateClickDebugPublicationServiceDependencies());

        private GroundLabelEntityAddressProvider GroundLabelEntityAddresses => field ??= new(() => _gameController.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels);

        private VisibleLabelSnapshotProvider VisibleLabelSnapshots => field ??= new(_cachedLabels);

        private PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression => field ??= new(CreatePathfindingLabelSuppressionEvaluatorDependencies());

        private UltimatumAutomationService UltimatumAutomation => field ??= new(CreateUltimatumAutomationServiceDependencies());

        private ClickLabelInteractionService LabelInteraction => field ??= new(CreateClickLabelInteractionServiceDependencies());

        private ChestLootSettlementTracker ChestLootSettlement => field ??= new(CreateChestLootSettlementDependencies());

        private OffscreenStickyTargetHandler OffscreenStickyTargets => field ??= new(CreateOffscreenStickyTargetHandlerDependencies());

        private OnscreenMechanicPathingBlocker OnscreenMechanicPathingBlocker => field ??= new(CreateOnscreenMechanicPathingBlockerDependencies());

        private OffscreenTraversalTargetResolver OffscreenTraversalTargets => field ??= new(CreateOffscreenTraversalTargetResolverDependencies());

        private OffscreenPathingCoordinator OffscreenPathing => field ??= new(CreateOffscreenPathingCoordinatorDependencies());

        private ClickRuntimeEngine RegularClick => field ??= new(CreateClickRuntimeEngineDependencies());

        private MovementSkillCoordinator MovementSkills => field ??= new(CreateMovementSkillCoordinatorDependencies());

        private OffscreenTargetResolver OffscreenTargetResolver => field ??= new(_gameController, _pathfindingService, pointIsInClickableArea: PointIsInClickableArea);

        private ClickTickContextFactory TickContextFactory => field ??= new(CreateClickTickContextFactoryDependencies());

        private SpecialLabelInteractionHandler SpecialLabelInteraction => field ??= new(CreateSpecialLabelInteractionHandlerDependencies());

        private ManualCursorLabelInteractionHandler ManualCursorLabelInteraction => field ??= new(CreateManualCursorLabelInteractionHandlerDependencies());

        private LabelSelectionScanEngine LabelSelectionScan => field ??= new(CreateLabelSelectionScanEngineDependencies());

        private BlightChestTransitionSuppression BlightChestTransitionSuppression => field ??= new();

        private ManualCursorLabelSelector ManualCursorLabels => field ??= new(CreateManualCursorLabelSelectorDependencies());

        private ManualCursorVisibleMechanicSelector ManualCursorVisibleMechanics => field ??= new(CreateManualCursorVisibleMechanicSelectorDependencies());

        private LostShipmentTargetSelector LostShipmentTargets => field ??= new(CreateLostShipmentTargetSelectorDependencies());

        private SettlersOreTargetSelector SettlersOreTargets => field ??= new(CreateSettlersOreTargetSelectorDependencies());

        private VisibleMechanicCoordinator VisibleMechanics => field ??= new(CreateVisibleMechanicCoordinatorDependencies());

        private InteractionExecutionRuntimeDependencies CreateInteractionExecutionRuntimeDependencies()
            => new(
                ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
                point => ForceRefreshPointIsInClickableArea(point, string.Empty),
                ClickAutomationSupport.DebugLog,
                LockedInteractionDispatcher.PerformClick,
                LockedInteractionDispatcher.PerformHoldClick,
                _performanceMonitor.RecordClickInterval);

        private AltarAutomationServiceDependencies CreateAltarAutomationServiceDependencies()
            => new(
                _settings,
                _gameController,
                _altarService.GetAltarComponentsReadOnly,
                _altarService.RemoveAltarComponentsByElement,
                _weightCalculator.CalculateAltarWeights,
                (altar, weights, topModsRect, bottomModsRect, topModsTopLeft) => _altarChoiceEvaluator.DetermineChoiceElement(altar, weights, topModsRect, bottomModsRect),
                ClickAutomationSupport.IsClickableInEitherSpace,
                ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
                InteractionExecutionRuntime.Execute,
                ClickAutomationSupport.DebugLog,
                _errorHandler.LogError,
                LockedInteractionDispatcher.ElementLock);

        private ClickDebugPublicationServiceDependencies CreateClickDebugPublicationServiceDependencies()
            => new(
                _gameController,
                ClickAutomationSupport.ShouldCaptureClickDebug,
                ClickAutomationSupport.PublishClickSnapshot,
                ClickAutomationSupport.IsClickableInEitherSpace,
                ClickAutomationSupport.IsInsideWindowInEitherSpace);

        private PathfindingLabelSuppressionEvaluatorDependencies CreatePathfindingLabelSuppressionEvaluatorDependencies()
            => new(
                _settings,
                _runtimeState);

        private UltimatumAutomationServiceDependencies CreateUltimatumAutomationServiceDependencies()
            => new(
                _settings,
                _gameController,
                _cachedLabels,
                ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
                ClickAutomationSupport.IsClickableInEitherSpace,
                messageFactory => ClickAutomationSupport.DebugLog(messageFactory()),
                (clickPos, clickElement) => { LockedInteractionDispatcher.PerformClick(clickPos, clickElement, _gameController); },
                _performanceMonitor.RecordClickInterval,
                ClickAutomationSupport.ShouldCaptureUltimatumDebug,
                ClickAutomationSupport.PublishUltimatumEvent);

        private ClickLabelInteractionServiceDependencies CreateClickLabelInteractionServiceDependencies()
            => new(
                _settings,
                _gameController,
                _labelInteractionPort,
                (label, windowTopLeft, allLabels, isClickableArea) =>
                    (_labelClickPointResolver.TryCalculateClickPosition(label, windowTopLeft, allLabels, isClickableArea, out Vector2 clickPos), clickPos),
                ClickAutomationSupport.IsClickableInEitherSpace,
                ClickAutomationSupport.IsInsideWindowInEitherSpace,
                InteractionExecutionRuntime.Execute,
                _groundItemsVisible,
                messageFactory => ClickAutomationSupport.DebugLog(messageFactory()),
                BlightChestDebug,
                message => _errorHandler.LogError(message));

        private ChestLootSettlementTrackerDependencies CreateChestLootSettlementDependencies()
            => new(
                _settings,
                _chestLootSettlementState,
                GroundLabelEntityAddresses,
                ClickDebugPublisher,
                LabelInteraction,
                BlightChestTransitionSuppression.ShouldSuppressBlightChestClick);

        private OffscreenStickyTargetHandlerDependencies CreateOffscreenStickyTargetHandlerDependencies()
            => new(
                _gameController,
                _shrineService,
                _runtimeState,
                LabelInteraction,
                ChestLootSettlement,
                ClickAutomationSupport.IsClickableInEitherSpace,
                PathfindingLabelSuppression,
                _labelInteractionPort,
                ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction);

        private OnscreenMechanicPathingBlockerDependencies CreateOnscreenMechanicPathingBlockerDependencies()
            => new(
                _settings,
                AltarAutomation,
                VisibleMechanics,
                ClickDebugPublisher);

        private OffscreenTraversalTargetResolverDependencies CreateOffscreenTraversalTargetResolverDependencies()
            => new(
                _settings,
                _gameController,
                _mechanicPriorityContextProvider,
                LabelInteraction,
                _labelInteractionPort,
                VisibleLabelSnapshots,
                ClickAutomationSupport.IsClickableInEitherSpace,
                ClickAutomationSupport.IsInsideWindowInEitherSpace,
                PathfindingLabelSuppression,
                DebugLog: ClickAutomationSupport.DebugLog);

        private OffscreenPathingCoordinatorDependencies CreateOffscreenPathingCoordinatorDependencies()
        {
            OffscreenTraversalServices services = ResolveOffscreenTraversalServices();
            return new(
                _settings,
                _gameController,
                _pathfindingService,
                services.PathingBlocker,
                services.TraversalTargets,
                services.StickyTargets,
                services.TargetResolver,
                services.MovementSkills,
                services.LabelInteraction,
                ClickAutomationSupport.DebugLog,
                ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction,
                services.ClickDebugPublisher,
                PointIsInClickableArea,
                pos => IsBlightBuildOrUpgradeIconAt(pos));
        }

        private ClickRuntimeEngineDependencies CreateClickRuntimeEngineDependencies()
        {
            RuntimeInteractionServices services = ResolveRuntimeInteractionServices();
            return new(
                services.TickContextFactory,
                services.AltarAutomation,
                services.ClickDebugPublisher,
                _settings,
                _labelInteractionPort,
                services.VisibleMechanics,
                services.LabelSelectionScan,
                services.SpecialLabelInteraction,
                services.LabelInteraction,
                PointIsInClickableArea,
                ClickAutomationSupport.ShouldCaptureClickDebug,
                _pathfindingService,
                services.PathfindingLabelSuppression,
                services.ChestLootSettlement,
                services.OffscreenPathing,
                ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction,
                ClickAutomationSupport.DebugLog,
                _inputHandler,
                GetHarvestLabelToClick,
                TryProgressBlightBuilding,
                GetBlightPathfindTarget,
                IsBlightEncounterActive);
        }

        private MovementSkillCoordinatorDependencies CreateMovementSkillCoordinatorDependencies()
            => new(
                _settings,
                _gameController,
                _runtimeState,
                _performanceMonitor,
                OffscreenTargetResolver.GetRemainingOffscreenPathNodeCount,
                ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
                PointIsInClickableArea,
                ClickAutomationSupport.DebugLog);

        private ClickTickContextFactoryDependencies CreateClickTickContextFactoryDependencies()
            => new(
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
                clickDebugPublisher: ClickDebugPublisher);

        private OffscreenTraversalServices ResolveOffscreenTraversalServices()
            => new(
                PathingBlocker: OnscreenMechanicPathingBlocker,
                TraversalTargets: OffscreenTraversalTargets,
                StickyTargets: OffscreenStickyTargets,
                TargetResolver: OffscreenTargetResolver,
                MovementSkills: MovementSkills,
                LabelInteraction: LabelInteraction,
                ClickDebugPublisher: ClickDebugPublisher);

        private RuntimeInteractionServices ResolveRuntimeInteractionServices()
            => new(
                TickContextFactory: TickContextFactory,
                AltarAutomation: AltarAutomation,
                ClickDebugPublisher: ClickDebugPublisher,
                VisibleMechanics: VisibleMechanics,
                LabelSelectionScan: LabelSelectionScan,
                SpecialLabelInteraction: SpecialLabelInteraction,
                LabelInteraction: LabelInteraction,
                PathfindingLabelSuppression: PathfindingLabelSuppression,
                ChestLootSettlement: ChestLootSettlement,
                OffscreenPathing: OffscreenPathing);

        private SpecialLabelInteractionHandlerDependencies CreateSpecialLabelInteractionHandlerDependencies()
            => new(
                _settings,
                AltarAutomation,
                LabelInteraction,
                UltimatumAutomation,
                ClickAutomationSupport.DebugLog);

        private ManualCursorLabelInteractionHandlerDependencies CreateManualCursorLabelInteractionHandlerDependencies()
            => new(
                _settings,
                AltarAutomation,
                LabelInteraction,
                ChestLootSettlement,
                PathfindingLabelSuppression,
                _pathfindingService,
                UltimatumAutomation);

        private LabelSelectionScanEngineDependencies CreateLabelSelectionScanEngineDependencies()
            => new(
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
                ClickAutomationSupport.DebugLog);

        private ManualCursorLabelSelectorDependencies CreateManualCursorLabelSelectorDependencies()
            => new(
                _gameController,
                _labelInteractionPort,
                PathfindingLabelSuppression,
                _labelClickPointResolver);

        private ManualCursorVisibleMechanicSelectorDependencies CreateManualCursorVisibleMechanicSelectorDependencies()
            => new(
                _gameController,
                VisibleMechanics,
                LabelInteraction);

        private LostShipmentTargetSelectorDependencies CreateLostShipmentTargetSelectorDependencies()
            => new(
                _settings,
                _gameController,
                ClickAutomationSupport.DebugLog,
                ClickAutomationSupport.IsInsideWindowInEitherSpace,
                ClickAutomationSupport.IsClickableInEitherSpace);

        private SettlersOreTargetSelectorDependencies CreateSettlersOreTargetSelectorDependencies()
            => new(
                _settings,
                _gameController,
                ClickDebugPublisher,
                ClickAutomationSupport.DebugLog,
                ClickAutomationSupport.IsInsideWindowInEitherSpace,
                ClickAutomationSupport.IsClickableInEitherSpace,
                GroundLabelEntityAddresses);

        private VisibleMechanicCoordinatorDependencies CreateVisibleMechanicCoordinatorDependencies()
        {
            VisibleMechanicServices services = ResolveVisibleMechanicServices();
            return new(
                _settings,
                _gameController,
                _shrineService,
                services.LostShipmentTargets,
                services.SettlersOreTargets,
                PointIsInClickableArea,
                services.LabelInteraction,
                services.OffscreenStickyTargets,
                _pathfindingService,
                ClickAutomationSupport.DebugLog,
                ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction,
                services.ClickDebugPublisher);
        }

        private VisibleMechanicServices ResolveVisibleMechanicServices()
            => new(
                LostShipmentTargets: LostShipmentTargets,
                SettlersOreTargets: SettlersOreTargets,
                LabelInteraction: LabelInteraction,
                OffscreenStickyTargets: OffscreenStickyTargets,
                ClickDebugPublisher: ClickDebugPublisher);
    }
}
