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
                LabelSelectionCoordinator LabelSelection,
                ClickLabelInteractionService LabelInteraction,
                PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
                ChestLootSettlementTracker ChestLootSettlement,
                OffscreenPathingCoordinator OffscreenPathing);

        private ChestLootSettlementTracker ChestLootSettlement => field ??= new(CreateChestLootSettlementDependencies());

        private OffscreenStickyTargetHandler OffscreenStickyTargets => field ??= new(CreateOffscreenStickyTargetHandlerDependencies());

        private OnscreenMechanicPathingBlocker OnscreenMechanicPathingBlocker => field ??= new(CreateOnscreenMechanicPathingBlockerDependencies());

        private OffscreenTraversalTargetResolver OffscreenTraversalTargets => field ??= new(CreateOffscreenTraversalTargetResolverDependencies());

        private OffscreenPathingCoordinator OffscreenPathing => field ??= new(CreateOffscreenPathingCoordinatorDependencies());

        private ClickRuntimeEngine RegularClick => field ??= new(CreateClickRuntimeEngineDependencies());

        private MovementSkillCoordinator MovementSkills => field ??= new(CreateMovementSkillCoordinatorDependencies());

        private OffscreenTargetResolver OffscreenTargetResolver => field ??= new(_gameController, _pathfindingService);

        private ClickTickContextFactory TickContextFactory => field ??= new(CreateClickTickContextFactoryDependencies());

        internal ClickAutomationSupport ClickAutomationSupport { get; }

        internal LockedInteractionDispatcher LockedInteractionDispatcher { get; }

        private ChestLootSettlementTrackerDependencies CreateChestLootSettlementDependencies()
                        => new(
                                _settings,
                                _chestLootSettlementState,
                                GroundLabelEntityAddresses,
                                ClickDebugPublisher,
                                LabelInteraction);

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
                        PathfindingLabelSuppression);

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
                    _pointIsInClickableArea);
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
                    services.LabelSelection,
                    services.LabelInteraction,
                    ClickAutomationSupport.ShouldCaptureClickDebug,
                    _pathfindingService,
                    services.PathfindingLabelSuppression,
                    services.ChestLootSettlement,
                    services.OffscreenPathing,
                    ClickAutomationSupport.HoldDebugTelemetryAfterSuccessfulInteraction,
                    ClickAutomationSupport.DebugLog,
                    _inputHandler);
        }

        private MovementSkillCoordinatorDependencies CreateMovementSkillCoordinatorDependencies()
                => new(
                        _settings,
                        _gameController,
                        _runtimeState,
                        _performanceMonitor,
                        OffscreenTargetResolver.GetRemainingOffscreenPathNodeCount,
                        ClickAutomationSupport.EnsureCursorInsideGameWindowForClick,
                        _pointIsInClickableArea,
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
                        LabelSelection: LabelSelection,
                        LabelInteraction: LabelInteraction,
                        PathfindingLabelSuppression: PathfindingLabelSuppression,
                        ChestLootSettlement: ChestLootSettlement,
                        OffscreenPathing: OffscreenPathing);
    }
}