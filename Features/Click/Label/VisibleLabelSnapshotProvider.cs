namespace ClickIt.Features.Click.Label
{
    internal sealed class VisibleLabelSnapshotProvider(TimeCache<List<LabelOnGround>> cachedLabels)
    {
        private readonly TimeCache<List<LabelOnGround>> _cachedLabels = cachedLabels;

        internal IReadOnlyList<LabelOnGround>? GetCachedLabels()
            => _cachedLabels?.Value;
    }
}