namespace ClickIt.Features.Labels
{
    public sealed partial class LabelFilterPort
    {
        private LabelClickSettingsService ClickSettingsService
            => _clickSettingsService ??= new LabelClickSettingsService(
                _settings,
                _mechanicPrioritySnapshotService,
                LazyModeBlockerService.HasRestrictedItemsOnScreen,
                Keyboard.IsKeyDown);

        internal InventoryInteractionPolicy InventoryInteractionPolicy
        {
            get
            {
                EnsureInventoryDomainServices();
                return _inventoryDomainServices!.Value.InteractionPolicy;
            }
        }

        internal InventoryProbeService InventoryProbeService
        {
            get
            {
                EnsureInventoryDomainServices();
                return _inventoryDomainServices!.Value.ProbeService;
            }
        }

        private LabelInteractionRuleService InteractionRuleService
            => _interactionRuleService ??= new LabelInteractionRuleService(
                _worldItemMetadataPolicy,
                InventoryInteractionPolicy);

        private LabelCandidateBuilderService CandidateBuilderService
            => _candidateBuilderService ??= new LabelCandidateBuilderService(LabelMechanicResolutionService);

        private ILabelSelectionService LabelSelectionService
            => _labelSelectionService ??= new LabelSelectionService(new LabelSelectionServiceDependencies(
                _gameController,
                ClickSettingsService.Create,
                ShouldCaptureLabelDebug,
                _labelSelectionDiagnostics.PublishEvent,
                CandidateBuilderService.TryBuildCandidate,
                LabelMechanicResolutionService.GetMechanicIdForLabel));

        internal LabelDebugService LabelDebugService
            => _labelDebugService ??= new LabelDebugService(
                _settings,
                _errorHandler,
                _gameController,
                ClickSettingsService.Create,
                InteractionRuleService.ShouldAllowWorldItemByMetadata,
                LabelMechanicResolutionService,
                _labelSelectionDiagnostics);

        private LabelMechanicResolutionService LabelMechanicResolutionService
            => _labelMechanicResolutionService ??= new LabelMechanicResolutionService(
                _gameController,
                ClickSettingsService.Create,
                () => ClassificationDependencies);

        internal LazyModeBlockerService LazyModeBlockerService
            => _lazyModeBlockerService ??= new LazyModeBlockerService(
                _settings,
                _gameController,
                reason => _errorHandler.LogMessage(true, true, reason, 5));

        private MechanicClassifierDependencies ClassificationDependencies
            => _classificationDependencies ??= MechanicClassifierDependenciesFactory.Create(
                _worldItemMetadataPolicy,
                InteractionRuleService);

        private void EnsureInventoryDomainServices()
        {
            if (_inventoryDomainServices.HasValue)
                return;

            _inventoryDomainServices = InventoryDomainFactory.Create(new InventoryDomainFactoryDependencies(
                _worldItemMetadataPolicy.GetWorldItemBaseName,
                InventoryMetadataIdentifiers.StoneOfPassage));
        }
    }
}