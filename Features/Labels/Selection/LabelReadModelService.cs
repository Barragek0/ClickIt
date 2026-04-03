using SharpDX;

namespace ClickIt.Features.Labels.Selection
{
    public sealed class LabelReadModelService
    {
        private readonly GameController _gameController;
        private readonly Func<Vector2, bool> _pointIsInClickableArea;

        public TimeCache<List<LabelOnGround>> CachedLabels { get; }

        public LabelReadModelService(GameController gameController, Func<Vector2, bool> pointIsInClickableArea)
        {
            _gameController = gameController;
            _pointIsInClickableArea = pointIsInClickableArea;
            CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, 50);
        }

        public bool GroundItemsVisible()
        {
            return CachedLabels?.Value?.Count > 0;
        }

        public List<LabelOnGround> UpdateLabelComponent()
        {
            IList<LabelOnGround>? groundLabels = _gameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible;

            if (groundLabels == null || groundLabels.Count == 0)
            {
                return [];
            }

            List<LabelOnGround> validLabels = new(Math.Min(groundLabels.Count, 1000));
            var win = _gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(win.X, win.Y);

            bool IsClickableInEitherSpace(Vector2 point)
            {
                return _pointIsInClickableArea(point) || _pointIsInClickableArea(point + windowTopLeft);
            }

            for (int i = 0; i < groundLabels.Count && validLabels.Count < 1000; i++)
            {
                LabelOnGround label = groundLabels[i];
                if (LabelUtils.IsValidClickableLabel(label, IsClickableInEitherSpace))
                {
                    validLabels.Add(label);
                }
            }

            LabelUtils.SortLabelsByDistance(validLabels);

            return validLabels;
        }
    }
}