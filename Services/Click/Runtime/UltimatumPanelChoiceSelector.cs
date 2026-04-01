namespace ClickIt.Services.Click.Runtime
{
    internal static class UltimatumPanelChoiceSelector
    {
        internal static bool TryGetBest(IReadOnlyList<UltimatumPanelChoiceCandidate> candidates, out UltimatumPanelChoiceCandidate best)
        {
            best = default;
            int bestIndex = int.MaxValue;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                UltimatumPanelChoiceCandidate candidate = candidates[i];
                if (candidate.PriorityIndex < bestIndex)
                {
                    bestIndex = candidate.PriorityIndex;
                    best = candidate;
                    found = true;
                }
            }

            return found && bestIndex != int.MaxValue;
        }

        internal static bool TryGetFirstSaturated(IReadOnlyList<UltimatumPanelChoiceCandidate> candidates, out UltimatumPanelChoiceCandidate best)
        {
            best = default;

            for (int i = 0; i < candidates.Count; i++)
            {
                UltimatumPanelChoiceCandidate candidate = candidates[i];
                if (!candidate.IsSaturated)
                    continue;

                best = candidate;
                return true;
            }

            return false;
        }

        internal static bool TryGetSelected(
            IReadOnlyList<UltimatumPanelChoiceCandidate> candidates,
            bool isGruelingGauntletActive,
            out UltimatumPanelChoiceCandidate best)
        {
            if (isGruelingGauntletActive && TryGetFirstSaturated(candidates, out best))
                return true;

            return TryGetBest(candidates, out best);
        }
    }
}