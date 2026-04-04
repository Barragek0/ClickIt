namespace ClickIt.Features.Click.Interaction
{
    internal sealed class LabelClickPointResolver(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Random _random = new();

        internal bool IsLabelFullyOverlapped(LabelOnGround label, IReadOnlyList<LabelOnGround>? allLabels)
        {
            bool avoidOverlapsEnabled = _settings.AvoidOverlappingLabelClickPoints?.Value != false;
            if (!avoidOverlapsEnabled)
                return false;

            if (!LabelGeometry.TryGetLabelRect(label, out RectangleF rect))
                return false;

            Vector2 preferredPoint = WorldItemUiHoverPolicy.ResolvePreferredLabelPoint(
                rect,
                label.ItemOnGround.Type,
                _settings.ChestHeightOffset,
                label.ItemOnGround.Path,
                label.ItemOnGround.RenderName);

            List<RectangleF> potentialBlockers = CollectPotentialBlockingLabelRects(label, rect, allLabels);
            if (potentialBlockers.Count == 0)
                return false;

            if (LabelClickPointSearch.HasUnblockedOverlapProbePoint(rect, preferredPoint, potentialBlockers))
                return false;

            List<RectangleF> blockedAreas = LabelClickPointSearch.BuildIntersectionOverlaps(rect, potentialBlockers);
            return !LabelClickPointSearch.TryResolveVisibleClickPoint(rect, preferredPoint, blockedAreas, out _);
        }

        internal Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels = null)
        {
            if (!LabelGeometry.TryGetLabelRect(label, out RectangleF rect))
                throw new InvalidOperationException("Label element is invalid");

            Vector2 preferredPoint = WorldItemUiHoverPolicy.ResolvePreferredLabelPoint(
                rect,
                label.ItemOnGround?.Type ?? EntityType.WorldItem,
                _settings.ChestHeightOffset,
                label.ItemOnGround?.Path,
                label.ItemOnGround?.RenderName);

            Vector2 resolvedPoint = preferredPoint;
            bool avoidOverlapsEnabled = _settings.AvoidOverlappingLabelClickPoints?.Value != false;
            if (avoidOverlapsEnabled)
            {
                List<RectangleF> blockedAreas = CollectBlockingOverlaps(label, rect, allLabels);
                resolvedPoint = LabelClickPointSearch.ResolveVisibleClickPoint(rect, preferredPoint, blockedAreas);
            }

            Vector2 jitteredPoint = ApplyJitterWithinRect(resolvedPoint, rect);
            return jitteredPoint + windowTopLeft;
        }

        internal bool TryCalculateClickPosition(
            LabelOnGround label,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            Func<Vector2, bool>? isClickableArea,
            out Vector2 clickPosition)
        {
            clickPosition = default;

            if (!LabelGeometry.TryGetLabelRect(label, out RectangleF rect))
                return false;

            Vector2 preferredPoint = WorldItemUiHoverPolicy.ResolvePreferredLabelPoint(
                rect,
                label.ItemOnGround?.Type ?? EntityType.WorldItem,
                _settings.ChestHeightOffset,
                label.ItemOnGround?.Path,
                label.ItemOnGround?.RenderName);

            List<RectangleF> blockedAreas = [];
            bool avoidOverlapsEnabled = _settings.AvoidOverlappingLabelClickPoints?.Value != false;
            if (avoidOverlapsEnabled)
                blockedAreas = CollectBlockingOverlaps(label, rect, allLabels);

            if (!LabelClickPointSearch.TryResolveVisibleClickablePoint(rect, preferredPoint, blockedAreas, isClickableArea, out Vector2 resolvedPoint))
                return false;

            Vector2 jitteredPoint = ApplyJitterWithinRect(resolvedPoint, rect);
            if (!LabelClickPointSearch.IsPointClickable(jitteredPoint, isClickableArea))
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

        internal static List<RectangleF> CollectPotentialBlockingLabelRects(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
            => LabelClickPointSearch.CollectPotentialBlockingLabelRects(targetLabel, targetRect, allLabels);

        private static List<RectangleF> CollectBlockingOverlaps(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
            => LabelClickPointSearch.BuildIntersectionOverlaps(targetRect, CollectPotentialBlockingLabelRects(targetLabel, targetRect, allLabels));
    }
}