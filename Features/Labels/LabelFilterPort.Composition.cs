namespace ClickIt.Features.Labels
{
    public sealed partial class LabelFilterPort
    {
        // Keep LabelFilterPort merge-first and lazy: eager constructor collaborators, then the composition layer fans out in a stable order.
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

        internal ILabelSelectionService LabelSelectionService
            => field ??= new LabelSelectionService(CreateLabelSelectionServiceDependencies());

        internal LabelDebugService LabelDebugService
            => field ??= new LabelDebugService(
                _settings,
                _errorHandler,
                _gameController,
                ClickSettingsService.Create,
                (settings, item, gameController, label) => _worldItemMetadataPolicy.ShouldAllowWorldItemByMetadata(
                    settings,
                    item,
                    gameController,
                    label,
                    InventoryInteractionPolicy.ShouldAllowWorldItemWhenInventoryFull),
                LabelMechanicResolutionService,
                _labelSelectionDiagnostics);

        private LabelMechanicResolutionService LabelMechanicResolutionService
            => field ??= new LabelMechanicResolutionService(
                _gameController,
                ClickSettingsService.Create,
                _worldItemMetadataPolicy,
                InventoryInteractionPolicy);

        internal LazyModeBlockerService LazyModeBlockerService
            => field ??= new LazyModeBlockerService(
                _settings,
                _gameController,
                reason => _errorHandler.LogMessage(true, true, reason, 5));

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
                (LabelOnGround label, ClickSettings settings, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason)
                    => LabelEligibilityEngine.TryBuildCandidate(
                        label, settings,
                        LabelTargetabilityPolicy.IsEntityTargetableForClick,
                        LabelMechanicResolutionService.ResolveMechanicId,
                        out item, out mechanicId, out rejectReason),
                LabelMechanicResolutionService.GetMechanicIdForLabel);

        private InventoryDomainFactoryDependencies CreateInventoryDomainFactoryDependencies()
            => new(
                _worldItemMetadataPolicy.GetWorldItemBaseName,
                InventoryMetadataIdentifiers.StoneOfPassage);
    }
}
