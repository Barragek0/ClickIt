namespace ClickIt.Features.Labels
{
    internal interface ILabelInteractionPort
    {
        SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount);
        void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount);
        string? GetMechanicIdForLabel(LabelOnGround? label);
        bool ShouldCorruptEssence(LabelOnGround label);
    }
}