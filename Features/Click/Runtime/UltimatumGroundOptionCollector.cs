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
                bool isValid = optionElement != null && IsElementValid(optionElement);
                if (optionElement == null || !isValid)
                {
                    if (logFailures)
                        debugLog($"[TryClickPreferredUltimatumModifier] Option[{i}] ignored - valid={isValid}");


                    continue;
                }

                string modifierName = UltimatumUiTreeResolver.ResolveUltimatumModifierName(optionElement, options[i].ModifierName);

                int priorityIndex = UltimatumModifierPriorityMatcher.GetModifierPriorityIndex(modifierName, priorities);

                bool isSaturated = false;
                if (includeSaturation)
                {
                    bool hasSaturationState = UltimatumGruelingGauntletPolicy.TryReadChoiceSaturation(optionElement, out bool saturatedState);
                    isSaturated = UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(hasSaturationState, saturatedState, IsElementVisible(optionElement));
                }

                if (logFailures)
                    debugLog($"[TryClickPreferredUltimatumModifier] Option[{i}] modifier='{modifierName}', priority={priorityIndex}, saturated={isSaturated}, visible={IsElementVisible(optionElement)}, valid={isValid}");


                candidates.Add(new UltimatumGroundOptionCandidate(optionElement, modifierName, priorityIndex, isSaturated));
            }

            return candidates.Count > 0;
        }

        private static bool IsElementValid(Element element)
            => DynamicAccess.TryReadBool(element, DynamicAccessProfiles.IsValid, out bool isValid)
                ? isValid
                : element.IsValid;

        private static bool IsElementVisible(Element element)
            => DynamicAccess.TryReadBool(element, DynamicAccessProfiles.IsVisible, out bool isVisible)
                ? isVisible
                : element.IsVisible;
    }
}