using ClickIt.Features.Labels.Application;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Features.Labels
{
    internal interface ILabelInteractionPort
    {
        SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount);
        void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount);
        string? GetMechanicIdForLabel(LabelOnGround? label);
        LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount);
        bool ShouldCorruptEssence(LabelOnGround label);
    }
}