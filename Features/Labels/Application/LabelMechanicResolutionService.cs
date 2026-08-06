namespace ClickIt.Features.Labels.Application
{
    internal sealed class LabelMechanicResolutionService(
        GameController? gameController,
        Func<IReadOnlyList<LabelOnGround>?, ClickSettings> createClickSettings,
        IWorldItemMetadataPolicy worldItemMetadataPolicy,
        InventoryInteractionPolicy inventoryInteractionPolicy)
    {
        private readonly GameController? _gameController = gameController;
        private readonly Func<IReadOnlyList<LabelOnGround>?, ClickSettings> _createClickSettings = createClickSettings;
        private readonly IWorldItemMetadataPolicy _worldItemMetadataPolicy = worldItemMetadataPolicy;
        private readonly InventoryInteractionPolicy _inventoryInteractionPolicy = inventoryInteractionPolicy;

        public string? GetMechanicIdForLabel(LabelOnGround? label)
        {
            Entity? item = label?.ItemOnGround;
            if (label == null || item == null)
                return null;
            if (!LabelTargetabilityPolicy.IsEntityTargetableForClick(label, item))
                return null;

            ClickSettings clickSettings = _createClickSettings(null);
            return ResolveMechanicId(label, item, clickSettings);
        }

        public string? ResolveMechanicId(LabelOnGround label, Entity item, ClickSettings clickSettings)
            => MechanicClassifier.GetClickableMechanicId(label, item, clickSettings, _gameController, _worldItemMetadataPolicy, _inventoryInteractionPolicy);
    }
}