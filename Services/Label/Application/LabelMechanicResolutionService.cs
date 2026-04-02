using ClickIt.Services.Label.Classification;
using ClickIt.Services.Label.Selection;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Label.Application
{
    internal sealed class LabelMechanicResolutionService(
        GameController? gameController,
        Func<IReadOnlyList<LabelOnGround>?, ClickSettings> createClickSettings,
        Func<MechanicClassifierDependencies> getClassificationDependencies)
    {
        private readonly GameController? _gameController = gameController;
        private readonly Func<IReadOnlyList<LabelOnGround>?, ClickSettings> _createClickSettings = createClickSettings;
        private readonly Func<MechanicClassifierDependencies> _getClassificationDependencies = getClassificationDependencies;

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
            => MechanicClassifier.GetClickableMechanicId(label, item, clickSettings, _gameController, _getClassificationDependencies());
    }
}