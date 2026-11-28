namespace ClickIt.Features.Labels.Application
{
    internal interface ILabelSelectionService
    {
        // Suppression-gated selection: skips labels the caller's predicate rejects INLINE so the scan is a single O(n) pass even when many labels are suppressed. A null predicate selects ungated.
        LabelOnGround? GetNextLabelToClick(
            IReadOnlyList<LabelOnGround>? allLabels,
            int startIndex,
            int maxCount,
            Func<LabelOnGround, LabelCandidateBuildResult, bool>? isAcceptable);

        bool IsWithinConfiguredClickDistance(Entity? item) => true;
    }
}
