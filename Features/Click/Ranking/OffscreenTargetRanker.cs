namespace ClickIt.Features.Click.Ranking
{
    internal static class OffscreenTargetRanker
    {
        internal static bool ShouldPromoteCandidate(MechanicRank candidateRank, MechanicRank bestRank, bool hasBest)
            => !hasBest || CandidateRankingEngine.CompareRanks(candidateRank, bestRank) < 0;

        internal static bool TryPromoteRankedCandidate(
            ref Entity? best,
            ref string? bestMechanicId,
            ref MechanicRank bestRank,
            ref bool hasBest,
            Entity? candidate,
            string? mechanicId,
            MechanicRank rank)
        {
            if (candidate == null || !DynamicAccess.TryReadBool(candidate, DynamicAccessProfiles.IsValid, out bool isValid)
                || !isValid || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(candidate) || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            int tier = MechanicIds.GetFallbackTier(mechanicId);
            int bestTier = MechanicIds.GetFallbackTier(bestMechanicId);
            if (hasBest && (tier != bestTier ? tier > bestTier : !ShouldPromoteCandidate(rank, bestRank, true)))
                return false;

            best = candidate;
            bestMechanicId = mechanicId;
            bestRank = rank;
            hasBest = true;
            return true;
        }
    }
}
