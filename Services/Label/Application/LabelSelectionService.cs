using ExileCore.PoEMemory.Elements;

namespace ClickIt.Services.Label.Application
{
    internal sealed class LabelSelectionService(
        Func<IReadOnlyList<LabelOnGround>?, int, int, LabelOnGround?> getNextLabelToClickCore,
        Func<LabelOnGround?, string?> getMechanicIdForLabelCore) : ILabelSelectionService
    {
        private readonly Func<IReadOnlyList<LabelOnGround>?, int, int, LabelOnGround?> _getNextLabelToClickCore = getNextLabelToClickCore;
        private readonly Func<LabelOnGround?, string?> _getMechanicIdForLabelCore = getMechanicIdForLabelCore;

        public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            => _getNextLabelToClickCore(allLabels, startIndex, maxCount);

        public string? GetMechanicIdForLabel(LabelOnGround? label)
            => _getMechanicIdForLabelCore(label);
    }
}