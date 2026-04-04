namespace ClickIt.Features.Click
{
        public sealed partial class ClickAutomationPort
        {
                private SpecialLabelInteractionHandler SpecialLabelInteraction => _specialLabelInteractionHandler ??= new(new SpecialLabelInteractionHandlerDependencies(
                        _settings,
                        AltarAutomation,
                        LabelInteraction,
                        UltimatumAutomation,
                                        _support.DebugLog));

                private ManualCursorLabelInteractionHandler ManualCursorLabelInteraction => _manualCursorLabelInteractionHandler ??= new(new ManualCursorLabelInteractionHandlerDependencies(
                        _settings,
                        AltarAutomation,
                        LabelInteraction,
                        ChestLootSettlement,
                        PathfindingLabelSuppression,
                        _pathfindingService,
                        UltimatumAutomation));

                private LabelSelectionScanEngine LabelSelectionScan => _labelSelectionScanEngine ??= new(new LabelSelectionScanEngineDependencies(
                        _gameController,
                        _labelInteractionPort,
                        _labelClickPointResolver,
                        PathfindingLabelSuppression.ShouldSuppressLeverClick,
                        UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel,
                        LabelInteraction,
                        _mechanicPriorityContextProvider,
                        ClickDebugPublisher,
                        _support.DebugLog));

                private ManualCursorLabelSelector ManualCursorLabels => _manualCursorLabelSelector ??= new(new ManualCursorLabelSelectorDependencies(
                        _gameController,
                        _labelInteractionPort,
                        PathfindingLabelSuppression,
                        _labelClickPointResolver));

                private ManualCursorVisibleMechanicSelector ManualCursorVisibleMechanics => _manualCursorVisibleMechanicSelector ??= new(new ManualCursorVisibleMechanicSelectorDependencies(
                        _gameController,
                        VisibleMechanics,
                        LabelInteraction));

                private LabelSelectionCoordinator LabelSelection => _labelSelectionCoordinator ??= new(new LabelSelectionCoordinatorDependencies(
                        _gameController,
                        LabelSelectionScan,
                        ManualCursorLabels,
                        ManualCursorVisibleMechanics,
                        SpecialLabelInteraction,
                        ManualCursorLabelInteraction));

                private LostShipmentTargetSelector LostShipmentTargets => _lostShipmentTargetSelector ??= new(new LostShipmentTargetSelectorDependencies(
                        _settings,
                        _gameController,
                        _support.DebugLog,
                        _support.IsInsideWindowInEitherSpace,
                        _support.IsClickableInEitherSpace));

                private SettlersOreTargetSelector SettlersOreTargets => _settlersOreTargetSelector ??= new(new SettlersOreTargetSelectorDependencies(
                        _settings,
                        _gameController,
                        ClickDebugPublisher,
                        _support.DebugLog,
                        _support.IsInsideWindowInEitherSpace,
                        _support.IsClickableInEitherSpace,
                        GroundLabelEntityAddresses));

                private VisibleMechanicCoordinator VisibleMechanics => _visibleMechanicCoordinator ??= new(new VisibleMechanicCoordinatorDependencies(
                        _settings,
                        _gameController,
                        _shrineService,
                        LostShipmentTargets,
                        SettlersOreTargets,
                        _pointIsInClickableArea,
                        LabelInteraction,
                        OffscreenStickyTargets,
                        _pathfindingService,
                                        _support.DebugLog,
                                        _support.HoldDebugTelemetryAfterSuccessfulInteraction,
                        ClickDebugPublisher));

        }
}