using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using SharpDX;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public class LabelService
    {
        private readonly GameController _gameController;
        private readonly Func<Vector2, bool> _pointIsInClickableArea;

        public TimeCache<List<LabelOnGround>> CachedLabels { get; }

        public LabelService(GameController gameController, Func<Vector2, bool> pointIsInClickableArea)
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

            List<LabelOnGround> validLabels = new(System.Math.Min(groundLabels.Count, 1000));

            for (int i = 0; i < groundLabels.Count && validLabels.Count < 1000; i++)
            {
                LabelOnGround label = groundLabels[i];
                if (LabelUtils.IsValidClickableLabel(label, _pointIsInClickableArea))
                {
                    validLabels.Add(label);
                }
            }

            LabelUtils.SortLabelsByDistance(validLabels);

            return validLabels;
        }

        public static List<ExileCore.PoEMemory.Element> GetElementsByStringContains(ExileCore.PoEMemory.Element? label, string str)
        {
            return LabelUtils.GetElementsByStringContains(label, str);
        }
    }
}
