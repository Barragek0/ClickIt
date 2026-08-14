namespace ClickIt.Features.Click.Interaction
{
    internal static class LabelClickPointSearch
    {
        [ThreadStatic]
        private static List<RectangleF>? s_blockerRects;
        [ThreadStatic]
        private static List<RectangleF>? s_intersectionRects;

        internal static List<RectangleF> GetBlockerRects()
            => s_blockerRects ??= [];

        internal static List<RectangleF> GetIntersectionRects()
            => s_intersectionRects ??= [];

        internal static bool IsPointInsideRect(Vector2 point, RectangleF rect)
            => point.X >= rect.Left && point.X <= rect.Right && point.Y >= rect.Top && point.Y <= rect.Bottom;

        internal static bool IsPointClickable(Vector2 point, Func<Vector2, bool>? isClickableArea)
            => isClickableArea == null || isClickableArea(point);

        internal static bool HasUnblockedOverlapProbePoint(RectangleF targetRect, Vector2 preferredPoint, IReadOnlyList<RectangleF> potentialBlockers)
        {
            if (potentialBlockers == null || potentialBlockers.Count == 0)
                return true;

            const float inset = 1f;
            float left = targetRect.Left + inset;
            float right = targetRect.Right - inset;
            float top = targetRect.Top + inset;
            float bottom = targetRect.Bottom - inset;
            float centerY = targetRect.Top + (targetRect.Height * 0.5f);

            Span<Vector2> probePoints = stackalloc Vector2[9];
            probePoints[0] = ClampPointToRect(preferredPoint, targetRect);
            probePoints[1] = new Vector2(left, top);
            probePoints[2] = new Vector2(right, top);
            probePoints[3] = new Vector2(left, bottom);
            probePoints[4] = new Vector2(right, bottom);
            probePoints[5] = new Vector2(targetRect.Center.X, top);
            probePoints[6] = new Vector2(targetRect.Center.X, bottom);
            probePoints[7] = new Vector2(left, centerY);
            probePoints[8] = new Vector2(right, centerY);

            for (int i = 0; i < probePoints.Length; i++)
            {
                if (!IsPointBlocked(probePoints[i], potentialBlockers))
                    return true;
            }

            return false;
        }

        internal static RectangleF GetVirtualScreenBounds()
        {
            System.Drawing.Rectangle vs = SystemInformation.VirtualScreen;
            return new RectangleF(vs.Left, vs.Top, vs.Width, vs.Height);
        }

        internal static bool IsSafeAutomationPoint(Vector2 point, RectangleF gameWindowRect, RectangleF virtualScreenRect)
        {
            if (!IsPointInsideRect(point, virtualScreenRect))
                return false;

            if (gameWindowRect.Width <= 0 || gameWindowRect.Height <= 0)
                return true;

            if (!TryGetIntersection(gameWindowRect, virtualScreenRect, out RectangleF allowedRect))
                return false;

            return IsPointInsideRect(point, allowedRect);
        }

        internal static bool TryValidateAutomationScreenPoint(Vector2 point, GameController? gameController, out string reason)
        {
            RectangleF virtualScreenRect = GetVirtualScreenBounds();
            if (!IsPointInsideRect(point, virtualScreenRect))
            {
                reason = $"outside virtual screen bounds {virtualScreenRect}";
                return false;
            }

            RectangleF gameWindowRect = gameController?.Window?.GetWindowRectangleTimeCache ?? RectangleF.Empty;
            if (!IsSafeAutomationPoint(point, gameWindowRect, virtualScreenRect))
            {
                reason = $"outside safe game window bounds {gameWindowRect}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal static bool TryResolveVisibleClickPoint(RectangleF targetRect, Vector2 preferredPoint, IReadOnlyList<RectangleF> blockedAreas, out Vector2 resolvedPoint)
        {
            return TryResolveVisiblePoint(targetRect, preferredPoint, blockedAreas, null, out resolvedPoint);
        }

        internal static bool TryResolveVisibleClickablePoint(
            RectangleF targetRect,
            Vector2 preferredPoint,
            IReadOnlyList<RectangleF> blockedAreas,
            Func<Vector2, bool>? isClickableArea,
            out Vector2 resolvedPoint)
        {
            return TryResolveVisiblePoint(targetRect, preferredPoint, blockedAreas, isClickableArea, out resolvedPoint);
        }

        internal static Vector2 ResolveVisibleClickPoint(RectangleF targetRect, Vector2 preferredPoint, IReadOnlyList<RectangleF> blockedAreas)
        {
            TryResolveVisibleClickPoint(targetRect, preferredPoint, blockedAreas, out Vector2 resolvedPoint);
            return resolvedPoint;
        }

        internal static List<RectangleF> CollectPotentialBlockingLabelRects(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
            => CollectPotentialBlockingLabelRects(
                targetLabel,
                targetRect,
                allLabels,
                static other =>
                {
                    bool hasRect = LabelGeometry.TryGetLabelRect(other, out RectangleF rect);
                    return (rect, hasRect);
                });

        // Rect-resolver overload lets callers supply cached per-label rects (overlap checks run per Execute tick and would otherwise re-read every other label's rect each time).
        internal static List<RectangleF> CollectPotentialBlockingLabelRects(
            LabelOnGround targetLabel,
            RectangleF targetRect,
            IReadOnlyList<LabelOnGround>? allLabels,
            Func<LabelOnGround, (RectangleF Rect, bool HasRect)> rectResolver)
        {
            List<RectangleF> potentialBlockers = GetBlockerRects();
            potentialBlockers.Clear();
            if (allLabels == null || allLabels.Count == 0)
                return potentialBlockers;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? other = allLabels[i];
                if (other == null || ReferenceEquals(other, targetLabel))
                    continue;

                (RectangleF otherRect, bool hasRect) = rectResolver(other);
                if (!hasRect)
                    continue;

                if (otherRect.Right <= targetRect.Left
                    || otherRect.Left >= targetRect.Right
                    || otherRect.Bottom <= targetRect.Top
                    || otherRect.Top >= targetRect.Bottom)
                {
                    continue;
                }

                potentialBlockers.Add(otherRect);
            }

            return potentialBlockers;
        }

        internal static List<RectangleF> BuildIntersectionOverlaps(RectangleF targetRect, IReadOnlyList<RectangleF> potentialBlockers)
        {
            List<RectangleF> blockedAreas = GetIntersectionRects();
            blockedAreas.Clear();
            if (potentialBlockers == null || potentialBlockers.Count == 0)
                return blockedAreas;

            for (int i = 0; i < potentialBlockers.Count; i++)
            {
                if (TryGetIntersection(targetRect, potentialBlockers[i], out RectangleF overlap))
                    blockedAreas.Add(overlap);
            }

            return blockedAreas;
        }

        private static bool TryResolveVisiblePoint(
            RectangleF targetRect,
            Vector2 preferredPoint,
            IReadOnlyList<RectangleF> blockedAreas,
            Func<Vector2, bool>? isAllowedPoint,
            out Vector2 resolvedPoint)
        {
            Vector2 clampedPreferred = ClampPointToRect(preferredPoint, targetRect);
            if ((blockedAreas == null || blockedAreas.Count == 0 || !IsPointBlocked(clampedPreferred, blockedAreas))
                && (isAllowedPoint == null || isAllowedPoint(clampedPreferred)))
            {
                resolvedPoint = clampedPreferred;
                return true;
            }

            const int cols = 7;
            const int rows = 5;
            float stepX = targetRect.Width / cols;
            float stepY = targetRect.Height / rows;

            Vector2 best = clampedPreferred;
            float bestDistanceSq = float.MaxValue;

            for (int y = 0; y < rows; y++)
            {
                float sampleY = targetRect.Top + ((y + 0.5f) * stepY);
                for (int x = 0; x < cols; x++)
                {
                    float sampleX = targetRect.Left + ((x + 0.5f) * stepX);
                    Vector2 candidate = new(sampleX, sampleY);

                    if (blockedAreas != null && blockedAreas.Count > 0 && IsPointBlocked(candidate, blockedAreas))
                        continue;

                    if (isAllowedPoint != null && !isAllowedPoint(candidate))
                        continue;

                    float dx = candidate.X - clampedPreferred.X;
                    float dy = candidate.Y - clampedPreferred.Y;
                    float distanceSq = (dx * dx) + (dy * dy);
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        best = candidate;
                    }
                }
            }

            if (bestDistanceSq < float.MaxValue)
            {
                resolvedPoint = best;
                return true;
            }

            resolvedPoint = clampedPreferred;
            return false;
        }

        private static Vector2 ClampPointToRect(Vector2 point, RectangleF rect)
            => new(SystemMath.Clamp(point.X, rect.Left, rect.Right), SystemMath.Clamp(point.Y, rect.Top, rect.Bottom));

        internal static bool IsPointBlocked(Vector2 point, IReadOnlyList<RectangleF> blockedAreas)
        {
            for (int i = 0; i < blockedAreas.Count; i++)
            {
                if (IsPointInsideRect(point, blockedAreas[i]))
                    return true;
            }

            return false;
        }

        private static bool TryGetIntersection(RectangleF a, RectangleF b, out RectangleF intersection)
        {
            intersection = default;

            float left = SystemMath.Max(a.Left, b.Left);
            float top = SystemMath.Max(a.Top, b.Top);
            float right = SystemMath.Min(a.Right, b.Right);
            float bottom = SystemMath.Min(a.Bottom, b.Bottom);

            if (right <= left || bottom <= top)
                return false;

            intersection = new RectangleF(left, top, right - left, bottom - top);
            return true;
        }
    }
}
