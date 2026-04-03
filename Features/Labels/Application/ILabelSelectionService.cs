using ExileCore.PoEMemory.Elements;

namespace ClickIt.Features.Labels.Application
{
    internal interface ILabelSelectionService
    {
        LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount);
        string? GetMechanicIdForLabel(LabelOnGround? label);
    }
}