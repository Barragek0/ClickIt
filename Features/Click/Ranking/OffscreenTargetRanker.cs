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

            bool crawler = mechanicId == MechanicIds.DelveCrawler;
            bool bestCrawler = bestMechanicId == MechanicIds.DelveCrawler;
            if (hasBest && (crawler != bestCrawler ? crawler : !ShouldPromoteCandidate(rank, bestRank, true)))
                return false;

            best = candidate;
            bestMechanicId = mechanicId;
            bestRank = rank;
            hasBest = true;
            return true;
        }
    }
}
