using ExileCore.PoEMemory.Elements;

namespace ClickIt.Services.Label.Application
{
    internal interface ILabelSelectionService
    {
        LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount);
        string? GetMechanicIdForLabel(LabelOnGround? label);
    }
}