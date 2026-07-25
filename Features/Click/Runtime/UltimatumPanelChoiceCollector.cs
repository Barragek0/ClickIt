namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumPanelChoiceCollector
    {
        internal static bool TryCollectCandidates(
            UltimatumPanel panelObj,
            IReadOnlyList<string> priorities,
            bool isGruelingGauntletActive,
            bool logFailures,
            Action<string> debugLog,
            out List<UltimatumPanelChoiceCandidate> candidates)
        {
            candidates = [];

            if (!TryGetChoiceElements(panelObj, logFailures, debugLog, out object? choiceElementsObj))
                return false;

            IReadOnlyList<string> modifierNamesByIndex = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(panelObj.Modifiers);

            int seen = 0;
            foreach (object? choiceObj in UltimatumUiTreeResolver.EnumerateObjects(choiceElementsObj))
            {
                if (TryCreateCandidate(
                    choiceObj,
                    seen,
                    modifierNamesByIndex,
                    priorities,
                    isGruelingGauntletActive,
                    logFailures,
                    debugLog,
                    out UltimatumPanelChoiceCandidate candidate))
                {
                    candidates.Add(candidate);
                }

                seen++;
            }

            return candidates.Count > 0;
        }

        internal static void ResolveGruelingSaturation(
            IReadOnlyList<UltimatumPanelChoiceCandidate> candidates,
            Func<string, bool> shouldTakeRewardForModifier,
            out bool hasSaturatedChoice,
            out string saturatedModifier,
            out bool shouldTakeReward,
            out int saturatedCount)
        {
            saturatedCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].IsSaturated)
                    saturatedCount++;
            }

            hasSaturatedChoice = UltimatumPanelChoiceSelector.TryGetFirstSaturated(candidates, out UltimatumPanelChoiceCandidate saturatedChoice);
            saturatedModifier = hasSaturatedChoice ? saturatedChoice.ModifierName : string.Empty;
            shouldTakeReward = hasSaturatedChoice && shouldTakeRewardForModifier(saturatedModifier);
        }

        private static bool TryGetChoiceElements(
            UltimatumPanel panelObj,
            bool logFailures,
            Action<string> debugLog,
            out object? choiceElementsObj)
        {
            choiceElementsObj = null;

            UltimatumChoicePanel choicesPanelObj = panelObj.ChoicesPanel;
            if (choicesPanelObj == null)
            {
                if (logFailures)
                    debugLog("[TryClickUltimatumPanelChoice] ChoicesPanel missing.");
                return false;
            }

            choiceElementsObj = choicesPanelObj.ChoiceElements;
            if (choiceElementsObj == null)
            {
                if (logFailures)
                    debugLog("[TryClickUltimatumPanelChoice] ChoiceElements missing.");
                return false;
            }

            return true;
        }

        private static bool TryCreateCandidate(
            object? choiceObj,
            int seen,
            IReadOnlyList<string> modifierNamesByIndex,
            IReadOnlyList<string> priorities,
            bool isGruelingGauntletActive,
            bool logFailures,
            Action<string> debugLog,
            out UltimatumPanelChoiceCandidate candidate)
        {
            candidate = default;

            if (!TryGetUsableChoiceElement(choiceObj, seen, logFailures, debugLog, out Element? choiceEl, out RectangleF choiceRect)
                || choiceEl == null)
                return false;

            string modifierName = UltimatumUiTreeResolver.ResolveUltimatumModifierName(choiceEl, seen, modifierNamesByIndex);
            int priorityIndex = UltimatumModifierPriorityMatcher.GetModifierPriorityIndex(modifierName, priorities);

            bool saturatedForSelection = ResolveSaturationState(choiceEl, isGruelingGauntletActive);

            if (logFailures)
            {
                debugLog($"[TryClickUltimatumPanelChoice] Choice[{seen}] modifier='{modifierName}', priority={priorityIndex}, saturated={saturatedForSelection}, center={choiceRect.Center}, visible={choiceEl.IsVisible}, valid={choiceEl.IsValid}");
            }

            if (isGruelingGauntletActive && !saturatedForSelection)
            {
                if (logFailures)
                    debugLog($"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored in Grueling Gauntlet mode because it is not saturated.");
                return false;
            }

            candidate = new UltimatumPanelChoiceCandidate(choiceEl, modifierName, priorityIndex, saturatedForSelection);
            return true;
        }

        private static bool TryGetUsableChoiceElement(
            object? choiceObj,
            int seen,
            bool logFailures,
            Action<string> debugLog,
            out Element? choiceEl,
            out RectangleF choiceRect)
        {
            choiceRect = default;

            if (!UltimatumUiTreeResolver.TryExtractElement(choiceObj, out choiceEl) || choiceEl == null)
            {
                if (logFailures)
                    debugLog($"[TryClickUltimatumPanelChoice] Choice[{seen}] is not an Element.");
                return false;
            }

            if (!choiceEl.IsValid)
            {
                if (logFailures)
                    debugLog($"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored - valid={choiceEl.IsValid}");
                return false;
            }

            choiceRect = choiceEl.GetClientRect();
            if (choiceRect.Width <= 0 || choiceRect.Height <= 0)
            {
                if (logFailures)
                    debugLog($"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored - empty rect {choiceRect}.");
                return false;
            }

            return true;
        }

        private static bool ResolveSaturationState(Element choiceEl, bool isGruelingGauntletActive)
        {
            if (!isGruelingGauntletActive)
                return false;

            bool hasSaturationState = UltimatumGruelingGauntletPolicy.TryReadChoiceSaturation(choiceEl, out bool isSaturated);
            return UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(hasSaturationState, isSaturated, choiceEl.IsVisible);
        }
    }
}