namespace ClickIt.Features.Click
{
        public sealed partial class ClickAutomationPort
        {
                private ChestLootSettlementTracker ChestLootSettlement => _chestLootSettlementTracker ??= new(new ChestLootSettlementTrackerDependencies(
                        _settings,
                        _chestLootSettlementState,
                        GroundLabelEntityAddresses,
                        ClickDebugPublisher,
                        LabelInteraction));

                private OffscreenStickyTargetHandler OffscreenStickyTargets => _offscreenStickyTargetHandler ??= new(new OffscreenStickyTargetHandlerDependencies(
                        _gameController,
                        _shrineService,
                        _runtimeState,
                        LabelInteraction,
                        ChestLootSettlement,
                        _support.IsClickableInEitherSpace,
                        PathfindingLabelSuppression,
                        _labelInteractionPort,
                        _support.HoldDebugTelemetryAfterSuccessfulInteraction));

                private OffscreenPathingCoordinator OffscreenPathing => _offscreenPathingCoordinator ??= new(new OffscreenPathingCoordinatorDependencies(
                        _settings,
                        _gameController,
                        _pathfindingService,
                                        new OnscreenMechanicPathingBlocker(new OnscreenMechanicPathingBlockerDependencies(
                                                _settings,
                                                AltarAutomation,
                                                VisibleMechanics,
                                                ClickDebugPublisher)),
                                        new OffscreenTraversalTargetResolver(new OffscreenTraversalTargetResolverDependencies(
                                                _settings,
                                                _gameController,
                                                _mechanicPriorityContextProvider,
                                                LabelInteraction,
                                                _labelInteractionPort,
                                                VisibleLabelSnapshots,
                                                _support.IsClickableInEitherSpace,
                                                _support.IsInsideWindowInEitherSpace,
                                                PathfindingLabelSuppression)),
                        OffscreenStickyTargets,
                        OffscreenTargetResolver,
                        MovementSkills,
                        LabelInteraction,
                        _support.DebugLog,
                        _support.HoldDebugTelemetryAfterSuccessfulInteraction,
                        ClickDebugPublisher,
                        _pointIsInClickableArea));

                private ClickRuntimeEngine RegularClick => _clickRuntimeEngine ??= new(new ClickRuntimeEngineDependencies(
                        TickContextFactory,
                        AltarAutomation,
                        ClickDebugPublisher,
                        _settings,
                        _labelInteractionPort,
                        VisibleMechanics,
                        LabelSelection,
                        LabelInteraction,
                        _support.ShouldCaptureClickDebug,
                        _pathfindingService,
                        PathfindingLabelSuppression,
                        ChestLootSettlement,
                        OffscreenPathing,
                        _support.HoldDebugTelemetryAfterSuccessfulInteraction,
                        _support.DebugLog,
                        _inputHandler));

                private MovementSkillCoordinator MovementSkills => _movementSkillCoordinator ??= new(new MovementSkillCoordinatorDependencies(
                        _settings,
                        _gameController,
                        _runtimeState,
                        _performanceMonitor,
                        OffscreenTargetResolver.GetRemainingOffscreenPathNodeCount,
                        _support.EnsureCursorInsideGameWindowForClick,
                        _pointIsInClickableArea,
                        _support.DebugLog));

                private OffscreenTargetResolver OffscreenTargetResolver => _offscreenTargetResolver ??= new(_gameController, _pathfindingService);

                private ClickTickContextFactory TickContextFactory => _tickContextFactory ??= new(new ClickTickContextFactoryDependencies(
                        getWindowRectangle: () => _gameController.Window.GetWindowRectangleTimeCache,
                        getCursorAbsolutePosition: ManualCursorSelectionMath.GetCursorAbsolutePosition,
                        tryHandleUltimatumPanelUi: TryHandleUltimatumPanelUi,
                        debugLog: _support.DebugLog,
                        movementSkills: MovementSkills,
                        chestLootSettlement: ChestLootSettlement,
                        getLabelsForRegularSelection: GetLabelsForRegularSelection,
                        visibleMechanics: VisibleMechanics,
                        mechanicPriorityContextProvider: _mechanicPriorityContextProvider,
                        groundItemsVisible: _groundItemsVisible,
                        clickDebugPublisher: ClickDebugPublisher));
        }
}