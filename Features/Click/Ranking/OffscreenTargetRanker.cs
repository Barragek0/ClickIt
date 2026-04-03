using ExileCore.PoEMemory.MemoryObjects;
using ClickIt.Features.Click.Runtime;

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
            if (candidate == null || !candidate.IsValid || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(candidate) || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            if (!ShouldPromoteCandidate(rank, bestRank, hasBest))
                return false;

            best = candidate;
            bestMechanicId = mechanicId;
            bestRank = rank;
            hasBest = true;
            return true;
        }
    }
}