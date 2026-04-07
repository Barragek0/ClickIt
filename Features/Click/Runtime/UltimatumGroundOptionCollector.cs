namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumGroundOptionCollector
    {
        internal static bool TryCollectCandidates(
            IReadOnlyList<(Element OptionElement, string ModifierName)> options,
            IReadOnlyList<string> priorities,
            bool includeSaturation,
            bool logFailures,
            Action<string> debugLog,
            out List<UltimatumGroundOptionCandidate> candidates)
        {
            candidates = [];

            for (int i = 0; i < options.Count; i++)
            {
                Element optionElement = options[i].OptionElement;
                if (optionElement == null || !optionElement.IsValid)
                {
                    if (logFailures)
                        debugLog($"[TryClickPreferredUltimatumModifier] Option[{i}] ignored - valid={optionElement?.IsValid ?? false}");
                    continue;
                }

                string modifierName = UltimatumUiTreeResolver.ResolveUltimatumModifierName(optionElement, options[i].ModifierName);

                int priorityIndex = UltimatumModifierPriorityMatcher.GetModifierPriorityIndex(modifierName, priorities);

                bool isSaturated = false;
                if (includeSaturation)
                {
                    bool hasSaturationState = UltimatumGruelingGauntletPolicy.TryReadChoiceSaturation(optionElement, out bool saturatedState);
                    isSaturated = UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(hasSaturationState, saturatedState, optionElement.IsVisible);
                }

                if (logFailures)
                {
                    debugLog($"[TryClickPreferredUltimatumModifier] Option[{i}] modifier='{modifierName}', priority={priorityIndex}, saturated={isSaturated}, visible={optionElement.IsVisible}, valid={optionElement.IsValid}");
                }

                candidates.Add(new UltimatumGroundOptionCandidate(optionElement, modifierName, priorityIndex, isSaturated));
            }

            return candidates.Count > 0;
        }
    }
}