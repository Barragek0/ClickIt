namespace ClickIt.Features.Labels.Selection
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

                string path = TryGetLabelItemPath(label);
                if (path.Contains("Harvest/Irrigator", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("Harvest/Extractor", StringComparison.OrdinalIgnoreCase))
                    result.Add(label);

            }

            if (result.Count > 1)
                result.Sort(static (a, b) => TryGetLabelDistance(a).CompareTo(TryGetLabelDistance(b)));

            return result;
        }

        private static bool TryGetClickableLabelRectCenter(LabelOnGround? label, out Vector2 center)
        {
            center = default;
            Element? element = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                ? rawLabel as Element
                : null;
            if (element == null || !DynamicAccess.TryReadBool(element, DynamicAccessProfiles.IsValid, out bool isValid) || !isValid)
                return false;

            RectangleF rect = element.GetClientRect();
            center = rect.Center;
            return rect.Width > 0f && rect.Height > 0f;
        }

        private static string TryGetLabelItemPath(LabelOnGround? label)
        {
            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                && DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.Path, out string path)
                ? path
                : string.Empty;
        }

        private static float TryGetLabelDistance(LabelOnGround? label)
        {
            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                && DynamicAccess.TryReadFloat(rawItem, DynamicAccessProfiles.DistancePlayer, out float distance)
                ? distance
                : float.MaxValue;
        }
    }
}