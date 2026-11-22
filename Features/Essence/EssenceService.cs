namespace ClickIt.Features.Essence
{
    public class EssenceService(ClickItSettings settings)
    {
        // The corruption-only essences: the ones a corruption can add, and the ones a second corruption can ruin.
        private static readonly string[] SpecialEssenceNames =
        [
            "Essence of Hysteria", "Essence of Insanity", "Essence of Horror",
            "Essence of Delirium", "Essence of Desolation", "Essence of Trickery"
        ];

        private readonly ClickItSettings _settings = settings;

        public bool ShouldCorruptEssence(Element? label)
            => ShouldCorruptEssence(label == null ? null : new ElementAdapter(label));

        /// <summary>
        /// Whether the corruption table selects an essence, ignoring whether it already reads "Corrupted". Used by the
        /// second-corruption path, which deliberately revisits an essence it corrupted once.
        /// </summary>
        public static bool IsSelectedForCorruption(ClickItSettings settings, IElementAdapter? label)
        {
            if (label == null)
                return false;

            if (settings.CorruptAllEssences.Value)
                return true;

            IReadOnlyList<string> selectedEssences = settings.GetCorruptEssenceNames();
            return selectedEssences.Count > 0
                && LabelElementSearch.ElementContainsAnyStringsCore(label, selectedEssences);
        }

        /// <summary>
        /// Whether the corruption safeguards rule an essence out. They apply to every corruption, the first as well as
        /// the second: with them on, an essence that reads Deafening, or that is one of the special essences itself, is
        /// never corrupted. Kept as one rule so the first and second corruption paths cannot disagree.
        /// </summary>
        public static bool IsBlockedBySafeguard(ClickItSettings settings, IElementAdapter? label)
        {
            // A missing label is not the safeguard's to reject: the corruption check itself declines a null label.
            if (label == null)
                return false;

            if (settings.EssenceCorruptionSkipIfDeafening.Value
                && LabelElementSearch.ElementContainsAnyStringsCore(label, ["Deafening"]))
                return true;

            return settings.EssenceCorruptionSkipIfSpecialEssence.Value
                && LabelElementSearch.ElementContainsAnyStringsCore(label, SpecialEssenceNames);
        }

        private bool ShouldCorruptEssence(IElementAdapter? label)
        {
            if (label == null)
                return false;
            if (LabelElementSearch.ElementContainsAnyStringsCore(label, ["Corrupted"]))
                return false;
            if (IsBlockedBySafeguard(_settings, label))
                return false;

            return IsSelectedForCorruption(_settings, label);
        }
    }
}
