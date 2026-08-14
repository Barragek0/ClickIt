namespace ClickIt.Features.Labels.Application
{
    internal interface ILabelSelectionService
    {
        LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount);

        // Suppression-gated selection: skips labels the caller's predicate rejects INLINE so the scan is a
        // single O(n) pass even when many labels are suppressed. Default delegates to the ungated selection.
        LabelOnGround? GetNextLabelToClick(
            IReadOnlyList<LabelOnGround>? allLabels,
            int startIndex,
            int maxCount,
            Func<LabelOnGround, LabelCandidateBuildResult, bool>? isAcceptable)
            => GetNextLabelToClick(allLabels, startIndex, maxCount);

        string? GetMechanicIdForLabel(LabelOnGround? label);
    }
}