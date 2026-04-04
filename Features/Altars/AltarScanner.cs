namespace ClickIt.Features.Altars
{
    public static class AltarScanner
    {
        internal static List<LabelOnGround> CollectVisibleAltarLabels(
            TimeCache<List<LabelOnGround>>? cachedLabels,
            bool includeExarch,
            bool includeEater,
            AltarServiceDebugInfo debugInfo)
        {
            List<LabelOnGround> altarLabels = [];
            List<LabelOnGround>? labelsFromCache = cachedLabels?.Value;
            if (labelsFromCache == null)
            {
                debugInfo.RecordScannedLabelCounts(0, 0);
                return altarLabels;
            }

            List<LabelOnGround> exarchLabels = includeExarch
                ? GetAltarLabels(labelsFromCache, Constants.CleansingFireAltar)
                : [];
            List<LabelOnGround> eaterLabels = includeEater
                ? GetAltarLabels(labelsFromCache, Constants.TangleAltar)
                : [];

            debugInfo.RecordScannedLabelCounts(exarchLabels.Count, eaterLabels.Count);

            if (exarchLabels.Count > 0)
                altarLabels.AddRange(exarchLabels);

            if (eaterLabels.Count > 0)
                altarLabels.AddRange(eaterLabels);

            return altarLabels;
        }

        public static List<(Element element, string path)> CollectElementsFromLabels(List<LabelOnGround>? altarLabels)
        {
            var elementsToProcess = new List<(Element element, string path)>((altarLabels?.Count ?? 0) * 2);

            if (altarLabels == null) return elementsToProcess;

            foreach (var label in altarLabels)
            {
                if (label == null) continue;
                var elements = LabelElementSearch.GetElementsByStringContains(label.Label, "valuedefault");
                if (elements == null || elements.Count == 0) continue;
                string path = label.ItemOnGround?.Path ?? string.Empty;

                foreach (var el in elements)
                {
                    if (el != null && el.IsVisible)
                        elementsToProcess.Add((el, path));
                }
            }

            return elementsToProcess;
        }

        private static List<LabelOnGround> GetAltarLabels(IReadOnlyList<LabelOnGround> labels, string altarPathToken)
        {
            List<LabelOnGround> result = [];
            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround label = labels[i];
                if (label.ItemOnGround?.Path == null || !label.Label.IsVisible)
                    continue;

                if (label.ItemOnGround.Path.Contains(altarPathToken, StringComparison.Ordinal))
                    result.Add(label);
            }

            return result;
        }

        internal static AltarType DetermineAltarType(string path)
        {
            if (string.IsNullOrEmpty(path))
                return AltarType.Unknown;

            if (path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
                return AltarType.SearingExarch;
            if (path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
                return AltarType.EaterOfWorlds;

            return AltarType.Unknown;
        }
    }
}
