namespace ClickIt.Features.Click.Interaction
{
    internal sealed class LabelClickPointResolver(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Random _random = new();

        /**
        Keep this runtime wrapper so live overlap resolution still reads the real
        LabelOnGround geometry and Entity metadata. The internal overload keeps a
        bounded owner-level seam for click-point tests without fabricating
        brittle ExileCore label, element, and item graphs.
         */
        internal bool IsLabelFullyOverlapped(LabelOnGround label, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (!ShouldAvoidOverlaps())
                return false;

            if (!TryResolveLabelRect(label, out RectangleF rect))
                return false;

            List<RectangleF> potentialBlockers = CollectPotentialBlockingLabelRects(label, rect, allLabels);
            return IsLabelFullyOverlapped(rect, label.ItemOnGround.Type, label.ItemOnGround.Path, label.ItemOnGround.RenderName, potentialBlockers);
        }

        internal bool IsLabelFullyOverlapped(
            RectangleF rect,
            EntityType itemType,
            string? itemPath,
            string? renderName,
            IReadOnlyList<RectangleF> potentialBlockers)
        {
            if (potentialBlockers.Count == 0)
                return false;

            Vector2 preferredPoint = ResolvePreferredPoint(rect, itemType, itemPath, renderName);
            if (LabelClickPointSearch.HasUnblockedOverlapProbePoint(rect, preferredPoint, potentialBlockers))
                return false;

            List<RectangleF> blockedAreas = LabelClickPointSearch.BuildIntersectionOverlaps(rect, potentialBlockers);
            return !LabelClickPointSearch.TryResolveVisibleClickPoint(rect, preferredPoint, blockedAreas, out _);
        }

        /**
        Keep this runtime wrapper so live click-position resolution still derives
        geometry and overlap blockers from the real label. The internal overload
        preserves direct proof over the resolver's preferred-point, overlap, and
        jitter handling using already-resolved values.
         */
        internal Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels = null)
        {
            if (!TryResolveLabelRect(label, out RectangleF rect))
                throw new InvalidOperationException("Label element is invalid");

            bool avoidOverlapsEnabled = ShouldAvoidOverlaps();
            IReadOnlyList<RectangleF> blockedAreas = ResolveBlockedAreas(label, rect, allLabels, avoidOverlapsEnabled);

            return CalculateClickPosition(
                rect,
                label.ItemOnGround?.Type ?? EntityType.WorldItem,
                label.ItemOnGround?.Path,
                label.ItemOnGround?.RenderName,
                windowTopLeft,
                blockedAreas,
                avoidOverlapsEnabled);
        }

        internal Vector2 CalculateClickPosition(
            RectangleF rect,
            EntityType itemType,
            string? itemPath,
            string? renderName,
            Vector2 windowTopLeft,
            IReadOnlyList<RectangleF> blockedAreas,
            bool avoidOverlapsEnabled = true)
        {
            Vector2 preferredPoint = ResolvePreferredPoint(rect, itemType, itemPath, renderName);
            IReadOnlyList<RectangleF> effectiveBlockedAreas = avoidOverlapsEnabled ? blockedAreas : [];
            Vector2 resolvedPoint = avoidOverlapsEnabled
                ? LabelClickPointSearch.ResolveVisibleClickPoint(rect, preferredPoint, effectiveBlockedAreas)
                : preferredPoint;

            Vector2 jitteredPoint = ApplyJitterWithinRect(resolvedPoint, rect);
            if (effectiveBlockedAreas.Count != 0 && LabelClickPointSearch.IsPointBlocked(jitteredPoint, effectiveBlockedAreas))
                jitteredPoint = resolvedPoint;

            return jitteredPoint + windowTopLeft;
        }

        /**
        Keep this runtime wrapper so production still resolves label geometry and
        clickable overlap blockers from the real UI tree. The internal overload
        isolates the repo-owned visible-point search and jitter fallback logic
        from third-party label construction requirements.
         */
        internal bool TryCalculateClickPosition(
            LabelOnGround label,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            Func<Vector2, bool>? isClickableArea,
            out Vector2 clickPosition)
        {
            clickPosition = default;

            if (!TryResolveLabelRect(label, out RectangleF rect))
                return false;

            bool avoidOverlapsEnabled = ShouldAvoidOverlaps();
            IReadOnlyList<RectangleF> blockedAreas = ResolveBlockedAreas(label, rect, allLabels, avoidOverlapsEnabled);

            return TryCalculateClickPosition(
                rect,
                label.ItemOnGround?.Type ?? EntityType.WorldItem,
                label.ItemOnGround?.Path,
                label.ItemOnGround?.RenderName,
                windowTopLeft,
                blockedAreas,
                isClickableArea,
                out clickPosition,
                avoidOverlapsEnabled);
        }

        internal bool TryCalculateClickPosition(
            RectangleF rect,
            EntityType itemType,
            string? itemPath,
            string? renderName,
            Vector2 windowTopLeft,
            IReadOnlyList<RectangleF> blockedAreas,
            Func<Vector2, bool>? isClickableArea,
            out Vector2 clickPosition,
            bool avoidOverlapsEnabled = true)
        {
            clickPosition = default;

            Vector2 preferredPoint = ResolvePreferredPoint(rect, itemType, itemPath, renderName);
            IReadOnlyList<RectangleF> effectiveBlockedAreas = avoidOverlapsEnabled ? blockedAreas : [];
            if (!LabelClickPointSearch.TryResolveVisibleClickablePoint(rect, preferredPoint, effectiveBlockedAreas, isClickableArea, out Vector2 resolvedPoint))
                return false;

            Vector2 jitteredPoint = ApplyJitterWithinRect(resolvedPoint, rect);
            bool jitterStayedVisible = effectiveBlockedAreas.Count == 0
                || !LabelClickPointSearch.IsPointBlocked(jitteredPoint, effectiveBlockedAreas);
            if (!jitterStayedVisible || !LabelClickPointSearch.IsPointClickable(jitteredPoint, isClickableArea))
                jitteredPoint = resolvedPoint;

            clickPosition = jitteredPoint + windowTopLeft;
            return true;
        }

        private Vector2 ApplyJitterWithinRect(Vector2 resolvedPoint, RectangleF rect)
        {
            float jitterRange = 2f;
            float jitterX = (float)(_random.NextDouble() * (jitterRange * 2) - jitterRange);
            float jitterY = (float)(_random.NextDouble() * (jitterRange * 2) - jitterRange);
            Vector2 jitteredPoint = resolvedPoint + new Vector2(jitterX, jitterY);

            if (!LabelClickPointSearch.IsPointInsideRect(jitteredPoint, rect))
                return resolvedPoint;

            return jitteredPoint;
        }

        private Vector2 ResolvePreferredPoint(RectangleF rect, EntityType itemType, string? itemPath, string? renderName)
            => WorldItemUiHoverPolicy.ResolvePreferredLabelPoint(
                rect,
                itemType,
                _settings.ChestHeightOffset,
                itemPath,
                renderName);

        private bool ShouldAvoidOverlaps()
            => _settings.AvoidOverlappingLabelClickPoints?.Value != false;

        private static bool TryResolveLabelRect(LabelOnGround label, out RectangleF rect)
            => LabelGeometry.TryGetLabelRect(label, out rect);

        private static IReadOnlyList<RectangleF> ResolveBlockedAreas(
            LabelOnGround targetLabel,
            RectangleF targetRect,
            IReadOnlyList<LabelOnGround>? allLabels,
            bool avoidOverlapsEnabled)
            => avoidOverlapsEnabled
                ? CollectBlockingOverlaps(targetLabel, targetRect, allLabels)
                : [];

        internal static List<RectangleF> CollectPotentialBlockingLabelRects(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
            => LabelClickPointSearch.CollectPotentialBlockingLabelRects(targetLabel, targetRect, allLabels);

        private static List<RectangleF> CollectBlockingOverlaps(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
            => LabelClickPointSearch.BuildIntersectionOverlaps(targetRect, CollectPotentialBlockingLabelRects(targetLabel, targetRect, allLabels));
    }
}