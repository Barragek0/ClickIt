namespace ClickIt.Features.Click.Label
{
    internal sealed class VisibleLabelSnapshotProvider(GameController gameController, TimeCache<List<LabelOnGround>> cachedLabels)
    {
        private readonly GameController _gameController = gameController;
        private readonly TimeCache<List<LabelOnGround>> _cachedLabels = cachedLabels;

        internal IReadOnlyList<LabelOnGround>? GetVisibleOrCachedLabels()
        {
            try
            {
                IList<LabelOnGround>? raw = _gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
                IReadOnlyList<LabelOnGround>? visible = ClickLabelSelectionMath.ResolveVisibleLabelsWithoutForcedCopy(raw);
                if (visible != null)
                    return visible;
            }
            catch
            {
            }

            return _cachedLabels?.Value;
        }
    }
}