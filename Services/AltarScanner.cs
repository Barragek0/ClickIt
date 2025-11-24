using ClickIt.Utils;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Services
{
    public static class AltarScanner
    {
        public static List<(Element element, string path)> CollectElementsFromLabels(List<LabelOnGround> altarLabels)
        {
            var elementsToProcess = new List<(Element element, string path)>((altarLabels?.Count ?? 0) * 2);

            if (altarLabels == null) return elementsToProcess;

            foreach (var label in altarLabels)
            {
                if (label == null) continue;
                var elements = LabelUtils.GetElementsByStringContains(label.Label, "valuedefault");
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
    }
}
