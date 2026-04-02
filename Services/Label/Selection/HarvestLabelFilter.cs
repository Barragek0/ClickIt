using ExileCore.PoEMemory.Elements;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services.Label.Selection
{
    internal static class HarvestLabelFilter
    {
        public static List<LabelOnGround> FilterClickableHarvestLabels(IReadOnlyList<LabelOnGround>? allLabels, Func<Vector2, bool> isInClickableArea)
        {
            List<LabelOnGround> result = [];
            if (allLabels == null)
                return result;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround label = allLabels[i];
                if (!TryGetClickableLabelRectCenter(label, out Vector2 center))
                    continue;
                if (!isInClickableArea(center))
                    continue;

                string path = label.ItemOnGround?.Path ?? string.Empty;
                if (path.Contains("Harvest/Irrigator", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("Harvest/Extractor", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(label);
                }
            }

            if (result.Count > 1)
                result.Sort(static (a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));

            return result;
        }

        private static bool TryGetClickableLabelRectCenter(LabelOnGround? label, out Vector2 center)
        {
            center = default;
            var element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            RectangleF rect = element.GetClientRect();
            center = rect.Center;
            return rect.Width > 0f && rect.Height > 0f;
        }
    }
}