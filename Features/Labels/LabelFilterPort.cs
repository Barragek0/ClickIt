using ClickIt.Features.Labels.Application;
using ClickIt.Features.Labels.Classification;
using ClickIt.Features.Labels.Diagnostics;
using ClickIt.Features.Labels.Inventory;
using ClickIt.Features.Labels.Selection;
using ClickIt.Features.Mechanics;
using ClickIt.Shared;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Features.Labels
{
    public sealed class LabelFilterPort : ILabelInteractionPort
    {
        private readonly ClickItSettings _settings;
        private readonly EssenceService _essenceService;
        private readonly ErrorHandler _errorHandler;
        private readonly ExileCore.GameController? _gameController;
        private readonly IWorldItemMetadataPolicy _worldItemMetadataPolicy = new WorldItemMetadataPolicy();
        private readonly IMechanicPrioritySnapshotProvider _mechanicPrioritySnapshotService = new MechanicPrioritySnapshotService();
        private readonly LabelSelectionDiagnostics _labelSelectionDiagnostics = new(24);
        private LabelClickSettingsService? _clickSettingsService;
        private ILabelSelectionService? _labelSelectionService;
        private LabelDebugService? _labelDebugService;
        private LabelMechanicResolutionService? _labelMechanicResolutionService;
        private LazyModeBlockerService? _lazyModeBlockerService;
        private LabelCandidateBuilderService? _candidateBuilderService;
        private LabelInteractionRuleService? _interactionRuleService;
        private MechanicClassifierDependencies? _classificationDependencies;
        private InventoryDomainServices? _inventoryDomainServices;

        internal LabelFilterPort(ClickItSettings settings, EssenceService essenceService, ErrorHandler errorHandler, ExileCore.GameController? gameController)
        {
            _settings = settings;
            _essenceService = essenceService;
            _errorHandler = errorHandler;
            _gameController = gameController;
        }

        private LabelClickSettingsService ClickSettingsService
            => _clickSettingsService ??= new LabelClickSettingsService(
                _settings,
                _mechanicPrioritySnapshotService,
                LazyModeBlockerService.HasRestrictedItemsOnScreen,
                Keyboard.IsKeyDown);

        private InventoryInteractionPolicy InventoryInteractionPolicy
        {
            get
            {
                EnsureInventoryDomainServices();
                return _inventoryDomainServices!.Value.InteractionPolicy;
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

        private LabelDebugService LabelDebugService
            => _labelDebugService ??= new LabelDebugService(
                _settings,
                _errorHandler,
                _gameController,
                ClickSettingsService.Create,
                InteractionRuleService.ShouldAllowWorldItemByMetadata,
                LabelMechanicResolutionService);

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

        internal LazyModeBlockerService GetLazyModeBlockerService()
            => LazyModeBlockerService;

        private MechanicClassifierDependencies ClassificationDependencies
            => _classificationDependencies ??= new MechanicClassifierDependencies(
                _worldItemMetadataPolicy.GetWorldItemMetadataPath,
                InteractionRuleService.ShouldAllowWorldItemByMetadata,
                LabelInteractionRuleService.ShouldClickStrongbox,
                LabelInteractionRuleService.ShouldClickEssence,
                LabelInteractionRuleService.GetRitualMechanicId,
                InteractionRuleService.ShouldAllowClosedDoorPastMechanic);

        internal LabelDebugSnapshot GetLatestLabelDebug()
            => _labelSelectionDiagnostics.GetLatest();

        internal IReadOnlyList<string> GetLatestLabelDebugTrail()
            => _labelSelectionDiagnostics.GetTrail();

        public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => LabelSelectionService.GetNextLabelToClick(allLabels, startIndex, maxCount);

        public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => LabelDebugService.GetSelectionDebugSummary(allLabels, startIndex, maxCount);

        public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => LabelDebugService.LogSelectionDiagnostics(allLabels, startIndex, maxCount);

        public string? GetMechanicIdForLabel(LabelOnGround? label)
            => LabelSelectionService.GetMechanicIdForLabel(label);

        internal string? LastLazyModeRestrictionReason => LazyModeBlockerService.LastRestrictionReason;

        internal bool HasLazyModeRestrictedItemsOnScreen(IReadOnlyList<LabelOnGround>? allLabels)
            => LazyModeBlockerService.HasRestrictedItemsOnScreen(allLabels);

        internal InventoryDebugSnapshot GetLatestInventoryDebug()
            => InventoryInteractionPolicy.GetLatestDebug();

        internal IReadOnlyList<string> GetLatestInventoryDebugTrail()
            => InventoryInteractionPolicy.GetLatestDebugTrail();

        internal void ClearInventoryProbeCacheForShutdown()
            => InventoryInteractionPolicy.ClearForShutdown();

        public bool ShouldCorruptEssence(LabelOnGround label)
            => _essenceService.ShouldCorruptEssence(label.Label);

        private bool ShouldCaptureLabelDebug()
            => _settings.DebugMode.Value && _settings.DebugShowLabels.Value;

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