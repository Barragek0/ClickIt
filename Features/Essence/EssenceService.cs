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
    }
}
