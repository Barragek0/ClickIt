namespace ClickIt.Features.Click
{
        public sealed partial class ClickAutomationPort
        {
                private readonly record struct LabelSelectionServices(
                    LabelSelectionScanEngine Scan,
                    ManualCursorLabelSelector ManualCursorLabels,
                    ManualCursorVisibleMechanicSelector ManualCursorVisibleMechanics,
                    SpecialLabelInteractionHandler SpecialLabelInteraction,
                    ManualCursorLabelInteractionHandler ManualCursorLabelInteraction);

                private readonly record struct VisibleMechanicServices(
                    LostShipmentTargetSelector LostShipmentTargets,
                    SettlersOreTargetSelector SettlersOreTargets,
                    ClickLabelInteractionService LabelInteraction,
                    OffscreenStickyTargetHandler OffscreenStickyTargets,
                    ClickDebugPublicationService ClickDebugPublisher);

                private SpecialLabelInteractionHandler SpecialLabelInteraction => _specialLabelInteractionHandler ??= new(CreateSpecialLabelInteractionHandlerDependencies());

                private ManualCursorLabelInteractionHandler ManualCursorLabelInteraction => _manualCursorLabelInteractionHandler ??= new(CreateManualCursorLabelInteractionHandlerDependencies());

                private LabelSelectionScanEngine LabelSelectionScan => _labelSelectionScanEngine ??= new(CreateLabelSelectionScanEngineDependencies());

                private ManualCursorLabelSelector ManualCursorLabels => _manualCursorLabelSelector ??= new(CreateManualCursorLabelSelectorDependencies());

                private ManualCursorVisibleMechanicSelector ManualCursorVisibleMechanics => _manualCursorVisibleMechanicSelector ??= new(CreateManualCursorVisibleMechanicSelectorDependencies());

                private LabelSelectionCoordinator LabelSelection => _labelSelectionCoordinator ??= new(CreateLabelSelectionCoordinatorDependencies());

                private LostShipmentTargetSelector LostShipmentTargets => _lostShipmentTargetSelector ??= new(CreateLostShipmentTargetSelectorDependencies());

                private SettlersOreTargetSelector SettlersOreTargets => _settlersOreTargetSelector ??= new(CreateSettlersOreTargetSelectorDependencies());

                private VisibleMechanicCoordinator VisibleMechanics => _visibleMechanicCoordinator ??= new(CreateVisibleMechanicCoordinatorDependencies());

                private SpecialLabelInteractionHandlerDependencies CreateSpecialLabelInteractionHandlerDependencies()
                    => new(
                        _settings,
                        AltarAutomation,
                        LabelInteraction,
                        UltimatumAutomation,
                        _support.DebugLog);

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
                        _labelClickPointResolver,
                        PathfindingLabelSuppression.ShouldSuppressLeverClick,
                        UltimatumLabelMath.ShouldSuppressInactiveUltimatumLabel,
                        LabelInteraction,
                        _mechanicPriorityContextProvider,
                        ClickDebugPublisher,
                        _support.DebugLog);

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

                private LabelSelectionCoordinatorDependencies CreateLabelSelectionCoordinatorDependencies()
                {
                        LabelSelectionServices services = ResolveLabelSelectionServices();
                        return new(
                            _gameController,
                            services.Scan,
                            services.ManualCursorLabels,
                            services.ManualCursorVisibleMechanics,
                            services.SpecialLabelInteraction,
                            services.ManualCursorLabelInteraction);
                }

                private LostShipmentTargetSelectorDependencies CreateLostShipmentTargetSelectorDependencies()
                    => new(
                        _settings,
                        _gameController,
                        _support.DebugLog,
                        _support.IsInsideWindowInEitherSpace,
                        _support.IsClickableInEitherSpace);

                private SettlersOreTargetSelectorDependencies CreateSettlersOreTargetSelectorDependencies()
                    => new(
                        _settings,
                        _gameController,
                        ClickDebugPublisher,
                        _support.DebugLog,
                        _support.IsInsideWindowInEitherSpace,
                        _support.IsClickableInEitherSpace,
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
                            _pointIsInClickableArea,
                            services.LabelInteraction,
                            services.OffscreenStickyTargets,
                            _pathfindingService,
                            _support.DebugLog,
                            _support.HoldDebugTelemetryAfterSuccessfulInteraction,
                            services.ClickDebugPublisher);
                }

                private LabelSelectionServices ResolveLabelSelectionServices()
                    => new(
                        Scan: LabelSelectionScan,
                        ManualCursorLabels: ManualCursorLabels,
                        ManualCursorVisibleMechanics: ManualCursorVisibleMechanics,
                        SpecialLabelInteraction: SpecialLabelInteraction,
                        ManualCursorLabelInteraction: ManualCursorLabelInteraction);

                private VisibleMechanicServices ResolveVisibleMechanicServices()
                    => new(
                        LostShipmentTargets: LostShipmentTargets,
                        SettlersOreTargets: SettlersOreTargets,
                        LabelInteraction: LabelInteraction,
                        OffscreenStickyTargets: OffscreenStickyTargets,
                        ClickDebugPublisher: ClickDebugPublisher);
        }
}