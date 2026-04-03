namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumGroundOptionSelector
    {
        internal static bool TryGetBest(IReadOnlyList<UltimatumGroundOptionCandidate> candidates, out UltimatumGroundOptionCandidate best)
        {
            best = default;
            int bestIndex = int.MaxValue;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                UltimatumGroundOptionCandidate candidate = candidates[i];
                if (candidate.PriorityIndex < bestIndex)
                {
                    bestIndex = candidate.PriorityIndex;
                    best = candidate;
                    found = true;
                }
            }

            return found && bestIndex != int.MaxValue;
        }

        internal static bool TryGetFirstSaturated(IReadOnlyList<UltimatumGroundOptionCandidate> candidates, out UltimatumGroundOptionCandidate saturated)
        {
            saturated = default;

            for (int i = 0; i < candidates.Count; i++)
            {
                UltimatumGroundOptionCandidate candidate = candidates[i];
                if (!candidate.IsSaturated)
                    continue;

                saturated = candidate;
                return true;
            }

            return false;
        }

        internal static bool TryGetSelected(
            IReadOnlyList<UltimatumGroundOptionCandidate> candidates,
            bool isGruelingGauntletActive,
            out UltimatumGroundOptionCandidate selected)
        {
            if (isGruelingGauntletActive && TryGetFirstSaturated(candidates, out selected))
                return true;

            return TryGetBest(candidates, out selected);
        }
    }
}