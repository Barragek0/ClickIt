namespace ClickIt.Features.Labels.Classification
{
    internal static class MechanicClassifierDependenciesFactory
    {
        internal static MechanicClassifierDependencies Create(
            IWorldItemMetadataPolicy worldItemMetadataPolicy,
            LabelInteractionRuleService interactionRuleService)
        {
            return new MechanicClassifierDependencies(
                worldItemMetadataPolicy.GetWorldItemMetadataPath,
                interactionRuleService.ShouldAllowWorldItemByMetadata,
                LabelInteractionRuleService.ShouldClickStrongbox,
                LabelInteractionRuleService.ShouldClickEssence,
                LabelInteractionRuleService.GetRitualMechanicId,
                interactionRuleService.ShouldAllowClosedDoorPastMechanic);
        }
    }
}