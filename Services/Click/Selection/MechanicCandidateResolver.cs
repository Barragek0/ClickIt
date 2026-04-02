using SharpDX;
using ClickIt.Services.Click.Runtime;
using ClickIt.Services.Click.Ranking;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Click.Selection
{
    internal static class MechanicCandidateResolver
    {
        internal static bool ShouldPromoteByDistanceAndCursor(
            float candidateDistance,
            float bestDistance,
            Vector2 candidateClickPosition,
            Vector2 bestClickPosition,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft)
        {
            return candidateDistance < bestDistance
                || (VisibleMechanicSelectionPolicy.ArePlayerDistancesEquivalent(candidateDistance, bestDistance)
                    && VisibleMechanicSelectionPolicy.IsFirstCandidateCloserToCursor(candidateClickPosition, bestClickPosition, cursorAbsolute, windowTopLeft));
        }

        internal static bool TryPromoteLostShipmentCandidate(
            ref LostShipmentCandidate? best,
            in LostShipmentCandidate candidate,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft)
        {
            if (!best.HasValue || ShouldPromoteByDistanceAndCursor(candidate.Distance, best.Value.Distance, candidate.ClickPosition, best.Value.ClickPosition, cursorAbsolute, windowTopLeft))
            {
                best = candidate;
                return true;
            }

            return false;
        }

        internal static bool TryPromoteSettlersCandidate(
            ref SettlersOreCandidate? best,
            in SettlersOreCandidate candidate,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft)
        {
            if (!best.HasValue || ShouldPromoteByDistanceAndCursor(candidate.Distance, best.Value.Distance, candidate.ClickPosition, best.Value.ClickPosition, cursorAbsolute, windowTopLeft))
            {
                best = candidate;
                return true;
            }

            return false;
        }

        internal static bool TryPromoteOffscreenCandidate(
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