namespace ClickIt.Features.Click.Runtime
{
    internal static class ManualCursorSelectionMath
    {
        internal const float TargetSnapDistancePx = 34f;
        internal const float GroundProjectionSnapDistancePx = 44f;

        internal static bool ShouldAttemptManualCursorAltarClick(bool isAltarLabel, bool hasClickableAltars)
            => isAltarLabel && hasClickableAltars;

        internal static bool ShouldUseManualGroundProjectionForCandidate(bool hasBackingEntity, bool isWorldItem)
            => hasBackingEntity && !isWorldItem;

        internal static bool ShouldTreatManualCursorAsHoveringCandidate(bool cursorInsideLabelRect, bool cursorNearGroundProjection)
            => cursorInsideLabelRect || cursorNearGroundProjection;

        internal static bool IsPointInsideRectInEitherSpace(RectangleF rect, Vector2 absolutePoint, Vector2 windowTopLeft)
        {
            if (rect.Contains(absolutePoint.X, absolutePoint.Y))
                return true;

            Vector2 clientPoint = absolutePoint - windowTopLeft;
            return rect.Contains(clientPoint.X, clientPoint.Y);
        }

        internal static bool IsWithinManualCursorMatchDistanceInEitherSpace(
            Vector2 cursorAbsolute,
            Vector2 candidatePoint,
            Vector2 windowTopLeft,
            float maxDistancePx)
        {
            if (maxDistancePx <= 0f)
                return false;

            float maxDistanceSq = maxDistancePx * maxDistancePx;
            float distanceSq = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, candidatePoint, windowTopLeft);
            return distanceSq <= maxDistanceSq;
        }

        internal static float GetManualCursorDistanceSquaredInEitherSpace(Vector2 cursorAbsolute, Vector2 candidatePoint, Vector2 windowTopLeft)
            => CoordinateSpace.DistanceSquaredInEitherSpace(cursorAbsolute, candidatePoint, windowTopLeft);

        internal static float GetManualCursorLabelHitScore(RectangleF rect, Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, rect.Center, windowTopLeft);

        internal static Vector2 GetCursorAbsolutePosition()
        {
            var cursor = Mouse.GetCursorPosition();
            return new Vector2(cursor.X, cursor.Y);
        }

        internal static float GetCursorDistanceSquaredToPoint(Vector2 point, Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, point, windowTopLeft);

        internal static float? TryGetCursorDistanceSquaredToLabel(LabelOnGround? label, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            if (!LabelGeometry.TryGetLabelRect(label, out RectangleF rect))
                return null;

            return GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, rect.Center, windowTopLeft);
        }

        internal static bool ShouldPreferHoveredEssenceLabel(
            bool hoveredIsEssence,
            bool hoveredHasOverlappingEssence,
            bool nextIsEssence,
            bool hoveredDiffersFromNext)
        {
            if (!hoveredIsEssence)
                return false;

            if (!hoveredDiffersFromNext)
                return false;

            if (hoveredHasOverlappingEssence)
                return true;

            return nextIsEssence;
        }
    }
}