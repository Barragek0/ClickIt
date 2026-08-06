namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumChoiceSelector<T>
        where T : struct, IUltimatumChoice
    {
        internal static bool TryGetBest(IReadOnlyList<T> candidates, out T best)
        {
            best = default;
            int bestIndex = int.MaxValue;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                if (candidate.PriorityIndex < bestIndex)
                {
                    bestIndex = candidate.PriorityIndex;
                    best = candidate;
                    found = true;
                }
            }

            return found && bestIndex != int.MaxValue;
        }

        internal static bool TryGetFirstSaturated(IReadOnlyList<T> candidates, out T saturated)
        {
            saturated = default;

            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                if (!candidate.IsSaturated)
                    continue;

                saturated = candidate;
                return true;
            }

            return false;
        }

        internal static bool TryGetSelected(
            IReadOnlyList<T> candidates,
            bool isGruelingGauntletActive,
            out T selected)
        {
            if (isGruelingGauntletActive && TryGetFirstSaturated(candidates, out selected))
                return true;

            return TryGetBest(candidates, out selected);
        }
    }
}
