namespace ClickIt.Features.Labels
{
    public sealed partial class LabelFilterPort
    {
        /**
        Keep `LabelFilterPort` merge-first and lazy: settings and root collaborators arrive eagerly in the constructor, then the composition layer fans out in a stable order from click settings and inventory policy into classification, candidate building, selection, debug, and lazy-mode blocking. This ordering matters for followability because the later owners intentionally reuse the earlier ones instead of each recreating their own view of metadata, mechanics, or inventory rules.
         */
        private LabelClickSettingsService ClickSettingsService
            => field ??= new LabelClickSettingsService(
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
            => field ??= new LabelInteractionRuleService(
                _worldItemMetadataPolicy,
                InventoryInteractionPolicy);

        private LabelCandidateBuilderService CandidateBuilderService
            => field ??= new LabelCandidateBuilderService(LabelMechanicResolutionService);

        private ILabelSelectionService LabelSelectionService
            => field ??= new LabelSelectionService(CreateLabelSelectionServiceDependencies());

        internal LabelDebugService LabelDebugService
            => field ??= new LabelDebugService(
                _settings,
                _errorHandler,
                _gameController,
                ClickSettingsService.Create,
                InteractionRuleService.ShouldAllowWorldItemByMetadata,
                LabelMechanicResolutionService,
                _labelSelectionDiagnostics);

        private LabelMechanicResolutionService LabelMechanicResolutionService
            => field ??= new LabelMechanicResolutionService(
                _gameController,
                ClickSettingsService.Create,
                () => ClassificationDependencies);

        internal LazyModeBlockerService LazyModeBlockerService
            => field ??= new LazyModeBlockerService(
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

            _inventoryDomainServices = InventoryDomainFactory.Create(CreateInventoryDomainFactoryDependencies());
        }

        private LabelSelectionServiceDependencies CreateLabelSelectionServiceDependencies()
            => new(
                _gameController,
                ClickSettingsService.Create,
                ShouldCaptureLabelDebug,
                _labelSelectionDiagnostics.PublishEvent,
                CandidateBuilderService.TryBuildCandidate,
                LabelMechanicResolutionService.GetMechanicIdForLabel);

        private InventoryDomainFactoryDependencies CreateInventoryDomainFactoryDependencies()
            => new(
                _worldItemMetadataPolicy.GetWorldItemBaseName,
                InventoryMetadataIdentifiers.StoneOfPassage);
    }
}