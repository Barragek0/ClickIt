using ExileCore.PoEMemory.MemoryObjects;
using ClickIt.Services.Click.Runtime;
using ClickIt.Services.Click.Ranking;

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
            if (candidate == null || !candidate.IsValid || candidate.IsHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(candidate) || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            MechanicRank rank = buildRank(candidate.DistancePlayer, mechanicId);
            return OffscreenTargetRanker.TryPromoteRankedCandidate(
                ref best,
                ref bestMechanicId,
                ref bestRank,
                ref hasBest,
                candidate,
                mechanicId,
                rank);
        }
    }
}