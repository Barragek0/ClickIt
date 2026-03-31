using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    internal static class OffscreenCandidateRankingEngine
    {
        internal static bool TryPromote(
            ref Entity? best,
            ref string? bestMechanicId,
            ref MechanicRank bestRank,
            ref bool hasBest,
            Entity? candidate,
            string? mechanicId,
            Func<float, string?, MechanicRank> buildRank)
        {
            if (candidate == null || !candidate.IsValid || candidate.IsHidden || ClickService.IsEntityHiddenByMinimapIcon(candidate) || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            MechanicRank rank = buildRank(candidate.DistancePlayer, mechanicId);
            if (hasBest && CandidateRankingEngine.CompareRanks(rank, bestRank) >= 0)
                return false;

            best = candidate;
            bestMechanicId = mechanicId;
            bestRank = rank;
            hasBest = true;
            return true;
        }
    }
}