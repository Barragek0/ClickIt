namespace ClickIt.Features.Click.Selection
{
    internal static class MechanicCandidateResolver
    {
        internal static bool TryPromoteClickableCandidate<TCandidate>(
            ref TCandidate? best,
            in TCandidate candidate,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            Func<TCandidate, float> distanceSelector,
            Func<TCandidate, Vector2> clickPositionSelector)
            where TCandidate : struct
        {
            if (!best.HasValue)
            {
                best = candidate;
                return true;
            }

            TCandidate bestValue = best.Value;
            if (!ShouldPromoteByDistanceAndCursor(
                    distanceSelector(candidate),
                    distanceSelector(bestValue),
                    clickPositionSelector(candidate),
                    clickPositionSelector(bestValue),
                    cursorAbsolute,
                    windowTopLeft))
                return false;


            best = candidate;
            return true;
        }

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
    }
}
