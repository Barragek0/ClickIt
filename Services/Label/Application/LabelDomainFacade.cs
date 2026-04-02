using ClickIt.Services.Label.Classification;
using ClickIt.Services.Label.Diagnostics;
using ClickIt.Services.Label.Inventory;
using ClickIt.Services.Label.Selection;
using ClickIt.Services.Mechanics;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using System.Windows.Forms;

namespace ClickIt.Services.Label.Application
{
    internal sealed class LabelDomainFacade(LabelDomainFacadeDependencies dependencies)
    {
        private readonly LabelDomainFacadeDependencies _dependencies = dependencies;
        private LabelClickSettingsService? _clickSettingsService;
        private ILabelSelectionService? _labelSelectionService;
        private LabelDebugService? _labelDebugService;
        private LabelMechanicResolutionService? _labelMechanicResolutionService;
        private LazyModeBlockerService? _lazyModeBlockerService;
        private LabelCandidateBuilderService? _candidateBuilderService;
        private LabelInteractionRuleService? _interactionRuleService;
        private MechanicClassifierDependencies? _classificationDependencies;
        private InventoryDomainFacade? _inventoryDomain;

        internal LabelClickSettingsService ClickSettingsService
            => _clickSettingsService ??= new LabelClickSettingsService(
                _dependencies.Settings,
                _dependencies.MechanicPrioritySnapshotProvider,
                LazyModeBlockerService.HasRestrictedItemsOnScreen,
                _dependencies.IsClickHotkeyHeld);

        internal LabelInteractionRuleService InteractionRuleService
            => _interactionRuleService ??= new LabelInteractionRuleService(
                _dependencies.WorldItemMetadataPolicy,
                InventoryDomain,
                _dependencies.StoneOfPassageMetadataIdentifier);

        internal LabelCandidateBuilderService CandidateBuilderService
            => _candidateBuilderService ??= new LabelCandidateBuilderService(MechanicResolutionService);

        internal ILabelSelectionService SelectionService
            => _labelSelectionService ??= new LabelSelectionService(new LabelSelectionServiceDependencies(
                _dependencies.GameController,
                ClickSettingsService.Create,
                _dependencies.ShouldCaptureLabelDebug,
                _dependencies.LabelSelectionDiagnostics.PublishEvent,
                CandidateBuilderService.TryBuildCandidate,
                MechanicResolutionService.GetMechanicIdForLabel));

        internal LabelDebugService DebugService
            => _labelDebugService ??= new LabelDebugService(
                _dependencies.Settings,
                _dependencies.ErrorHandler,
                _dependencies.GameController,
                ClickSettingsService.Create,
                InteractionRuleService.ShouldAllowWorldItemByMetadata,
                MechanicResolutionService);

        internal LabelMechanicResolutionService MechanicResolutionService
            => _labelMechanicResolutionService ??= new LabelMechanicResolutionService(
                _dependencies.GameController,
                ClickSettingsService.Create,
                () => ClassificationDependencies);

        internal LazyModeBlockerService LazyModeBlockerService
            => _lazyModeBlockerService ??= new LazyModeBlockerService(
                _dependencies.Settings,
                _dependencies.GameController,
                reason => _dependencies.ErrorHandler.LogMessage(true, true, reason, 5));

        internal InventoryDomainFacade InventoryDomain
            => _inventoryDomain ??= InventoryDomainComposition.Create(
                new InventoryDomainCompositionDependencies(_dependencies.WorldItemMetadataPolicy.GetWorldItemBaseName));

        private MechanicClassifierDependencies ClassificationDependencies
            => _classificationDependencies ??= new MechanicClassifierDependencies(
                _dependencies.WorldItemMetadataPolicy.GetWorldItemMetadataPath,
                InteractionRuleService.ShouldAllowWorldItemByMetadata,
                LabelInteractionRuleService.ShouldClickStrongbox,
                LabelInteractionRuleService.ShouldClickEssence,
                LabelInteractionRuleService.GetRitualMechanicId,
                InteractionRuleService.ShouldAllowClosedDoorPastMechanic);
    }

    internal readonly record struct LabelDomainFacadeDependencies(
        ClickItSettings Settings,
        Utils.ErrorHandler ErrorHandler,
        GameController? GameController,
        IWorldItemMetadataPolicy WorldItemMetadataPolicy,
        IMechanicPrioritySnapshotProvider MechanicPrioritySnapshotProvider,
        LabelSelectionDiagnostics LabelSelectionDiagnostics,
        Func<Keys, bool> IsClickHotkeyHeld,
        Func<bool> ShouldCaptureLabelDebug,
        string StoneOfPassageMetadataIdentifier);
}