namespace ClickIt.Features.Essence
{
    public class EssenceService(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public bool ShouldCorruptEssence(Element? label)
        {
            if (label == null)
                return false;
            if (LabelElementSearch.ElementContainsAnyStrings(label, ["Corrupted"]))
                return false;

            if (_settings.CorruptAllEssences.Value)
                return true;

            IReadOnlyList<string> selectedEssences = _settings.GetCorruptEssenceNames();
            if (selectedEssences.Count == 0)
                return false;

            return LabelElementSearch.ElementContainsAnyStrings(label, selectedEssences);
        }

        public static Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            Element? corruptElement = TryResolveCorruptionElement(label);
            if (corruptElement == null)
                return null;
            Random random = new();
            Vector2 offset = new(random.Next(0, 2), random.Next(0, 2));
            return corruptElement.GetClientRect().Center + windowTopLeft + offset;
        }

        private static Element? TryResolveCorruptionElement(LabelOnGround? label)
        {
            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel))
                return null;

            object? current = rawLabel;
            int[] childPath = [2, 0, 0];
            for (int i = 0; i < childPath.Length; i++)
            {
                if (!DynamicAccess.TryGetChildAtIndex(current, childPath[i], out current) || current == null)
                    return null;
            }

            return current as Element;
        }
    }
}
