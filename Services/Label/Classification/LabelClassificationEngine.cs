using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Label.Classification
{
    internal sealed class LabelClassificationEngine(MechanicClassifierDependencies dependencies)
    {
        private readonly MechanicClassifierDependencies _dependencies = dependencies;

        internal string? GetClickableMechanicId(
            LabelOnGround label,
            Entity item,
            ClickSettings settings,
            ExileCore.GameController? gameController)
            => MechanicClassifier.GetClickableMechanicId(label, item, settings, gameController, _dependencies);

        internal bool TryGetSettlersOreMechanicId(string? path, out string? mechanicId)
            => MechanicClassifier.TryGetSettlersOreMechanicId(path, out mechanicId);
    }
}