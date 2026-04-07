namespace ClickIt.Features.Click
{
        public sealed partial class ClickAutomationPort
        {
                private ChestLootSettlementTracker ChestLootSettlement => _chestLootSettlementTracker ??= new(CreateChestLootSettlementDependencies());

                private OffscreenStickyTargetHandler OffscreenStickyTargets => _offscreenStickyTargetHandler ??= new(CreateOffscreenStickyTargetHandlerDependencies());

                private OnscreenMechanicPathingBlocker OnscreenMechanicPathingBlocker => _onscreenMechanicPathingBlocker ??= new(CreateOnscreenMechanicPathingBlockerDependencies());

                private OffscreenTraversalTargetResolver OffscreenTraversalTargets => _offscreenTraversalTargetResolver ??= new(CreateOffscreenTraversalTargetResolverDependencies());

                private OffscreenPathingCoordinator OffscreenPathing => _offscreenPathingCoordinator ??= new(CreateOffscreenPathingCoordinatorDependencies());

                private ClickRuntimeEngine RegularClick => _clickRuntimeEngine ??= new(CreateClickRuntimeEngineDependencies());

                private MovementSkillCoordinator MovementSkills => _movementSkillCoordinator ??= new(CreateMovementSkillCoordinatorDependencies());

                private OffscreenTargetResolver OffscreenTargetResolver => _offscreenTargetResolver ??= new(_gameController, _pathfindingService);

                private ClickTickContextFactory TickContextFactory => _tickContextFactory ??= new(CreateClickTickContextFactoryDependencies());

                internal ClickAutomationSupport ClickAutomationSupport
                        => _support;

                internal LockedInteractionDispatcher LockedInteractionDispatcher
                        => _lockedInteractionDispatcher;

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
                                _support.IsClickableInEitherSpace,
                                PathfindingLabelSuppression,
                                _labelInteractionPort,
                                _support.HoldDebugTelemetryAfterSuccessfulInteraction);

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
                                _support.IsClickableInEitherSpace,
                                _support.IsInsideWindowInEitherSpace,
                                PathfindingLabelSuppression);

                private OffscreenPathingCoordinatorDependencies CreateOffscreenPathingCoordinatorDependencies()
                        => new(
                                _settings,
                                _gameController,
                                _pathfindingService,
                                OnscreenMechanicPathingBlocker,
                                OffscreenTraversalTargets,
                                OffscreenStickyTargets,
                                OffscreenTargetResolver,
                                MovementSkills,
                                LabelInteraction,
                                _support.DebugLog,
                                _support.HoldDebugTelemetryAfterSuccessfulInteraction,
                                ClickDebugPublisher,
                                _pointIsInClickableArea);

                private ClickRuntimeEngineDependencies CreateClickRuntimeEngineDependencies()
                        => new(
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
                                _inputHandler);

                private MovementSkillCoordinatorDependencies CreateMovementSkillCoordinatorDependencies()
                        => new(
                                _settings,
                                _gameController,
                                _runtimeState,
                                _performanceMonitor,
                                OffscreenTargetResolver.GetRemainingOffscreenPathNodeCount,
                                _support.EnsureCursorInsideGameWindowForClick,
                                _pointIsInClickableArea,
                                _support.DebugLog);

                private ClickTickContextFactoryDependencies CreateClickTickContextFactoryDependencies()
                        => new(
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
                                clickDebugPublisher: ClickDebugPublisher);
        }
}