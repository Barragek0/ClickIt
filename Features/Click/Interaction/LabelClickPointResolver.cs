namespace ClickIt.Features.Click.Interaction
{
    internal sealed class LabelClickPointResolver(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        // Same label's metadata (ItemOnGround/Type/Path/RenderName — 4 DLR reads) is resolved for overlap checks and click-position resolution 2-3x per tick; keyed on the label ADDRESS (stable across snapshots even when wrapper instances differ) with a short TTL.
        private readonly Dictionary<long, ResolvedLabelMetadata> _metadataCache = [];
        private long _metadataCacheWindowStartMs;
        private const long MetadataCacheWindowMs = 250;

        // Overlap-blocker collection reads every other label's rect (3 DLR reads each) per Execute tick; cache per label address so the reads happen once per window.
        private readonly Dictionary<long, (RectangleF Rect, bool HasRect)> _rectCache = [];
        private long _rectCacheWindowStartMs;
        private const long RectCacheWindowMs = 250;

        // Spatial grid (cell -> label addresses) over on-screen label rects so overlap-blocker collection only
        // inspects labels whose cells intersect the target, instead of scanning all N labels per examined label
        // (O(N x K) on dense fields with K examined labels). Rebuilt when the label-set reference changes or the
        // TTL expires; the grid stores ADDRESSES so it stays valid across list-instance churn.
        private IReadOnlyList<LabelOnGround>? _gridLabelsRef;
        private Dictionary<long, List<long>>? _gridCells;
        private long _gridBuiltAtMs;
        private const long BlockerGridWindowMs = 250;
        private const float BlockerGridCellSize = 64f;

        // The click coroutine and the manual-hover coroutine share this resolver and, with CoroutineMultiThreading enabled, can run on different threads. Both caches are plain dictionaries mutated from those paths, so they are guarded by a lock (the same corruption class previously fixed in LabelReadModelService).
        private readonly Lock _cacheLock = new();

        private readonly record struct ResolvedLabelMetadata(EntityType ItemType, string? ItemPath, string? RenderName);

        // Runtime wrapper over the resolved-value overload so production reads real label geometry.
        internal bool IsLabelFullyOverlapped(LabelOnGround label, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (!ShouldAvoidOverlaps())
                return false;

            if (!TryResolveLabelRect(label, out RectangleF rect))
                return false;

            ResolveLabelMetadataCached(label, out EntityType itemType, out string? itemPath, out string? renderName);

            List<RectangleF> potentialBlockers = CollectPotentialBlockingLabelRectsCached(label, rect, allLabels);
            return IsLabelFullyOverlapped(rect, itemType, itemPath, renderName, potentialBlockers);
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

        // Runtime wrapper over the resolved-value overload so production reads real label geometry.
        internal Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels = null)
        {
            if (!TryResolveLabelRect(label, out RectangleF rect))
                throw new InvalidOperationException("Label element is invalid");

            ResolveLabelMetadataCached(label, out EntityType itemType, out string? itemPath, out string? renderName);

            bool avoidOverlapsEnabled = ShouldAvoidOverlaps();
            IReadOnlyList<RectangleF> blockedAreas = ResolveBlockedAreas(label, rect, allLabels, avoidOverlapsEnabled);

            return CalculateClickPosition(
                rect,
                itemType,
                itemPath,
                renderName,
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

        // Runtime wrapper over the resolved-value overload so production reads real label geometry.
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

            ResolveLabelMetadataCached(label, out EntityType itemType, out string? itemPath, out string? renderName);

            bool avoidOverlapsEnabled = ShouldAvoidOverlaps();
            IReadOnlyList<RectangleF> blockedAreas = ResolveBlockedAreas(label, rect, allLabels, avoidOverlapsEnabled);

            return TryCalculateClickPosition(
                rect,
                itemType,
                itemPath,
                renderName,
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
            float jitterX = (float)((Random.Shared.NextDouble() * (jitterRange * 2)) - jitterRange);
            float jitterY = (float)((Random.Shared.NextDouble() * (jitterRange * 2)) - jitterRange);
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

        private List<RectangleF> ResolveBlockedAreas(
            LabelOnGround targetLabel,
            RectangleF targetRect,
            IReadOnlyList<LabelOnGround>? allLabels,
            bool avoidOverlapsEnabled)
            => avoidOverlapsEnabled
                ? CollectBlockingOverlaps(targetLabel, targetRect, allLabels)
                : [];

        internal static List<RectangleF> CollectPotentialBlockingLabelRects(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
            => LabelClickPointSearch.CollectPotentialBlockingLabelRects(targetLabel, targetRect, allLabels);

        private List<RectangleF> CollectBlockingOverlaps(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
            => LabelClickPointSearch.BuildIntersectionOverlaps(
                targetRect,
                CollectPotentialBlockingLabelRectsCached(targetLabel, targetRect, allLabels));

        // Grid + cached-rect blocker collection: builds the cell grid once per label set, then for the target
        // label only inspects labels in the cells its rect spans (each rect from the address cache).
        private List<RectangleF> CollectPotentialBlockingLabelRectsCached(
            LabelOnGround targetLabel,
            RectangleF targetRect,
            IReadOnlyList<LabelOnGround>? allLabels)
        {
            List<RectangleF> potentialBlockers = LabelClickPointSearch.GetBlockerRects();
            potentialBlockers.Clear();
            if (allLabels == null || allLabels.Count == 0)
                return potentialBlockers;

            List<long> candidates = CollectPotentialBlockerAddresses(targetLabel, targetRect, allLabels);
            for (int i = 0; i < candidates.Count; i++)
            {
                (RectangleF otherRect, bool hasRect) = ResolveLabelRectByAddressCached(candidates[i]);
                if (!hasRect)
                    continue;
                if (otherRect.Right <= targetRect.Left
                    || otherRect.Left >= targetRect.Right
                    || otherRect.Bottom <= targetRect.Top
                    || otherRect.Top >= targetRect.Bottom)
                    continue;
                potentialBlockers.Add(otherRect);
            }
            return potentialBlockers;
        }

        private List<long> CollectPotentialBlockerAddresses(
            LabelOnGround targetLabel,
            RectangleF targetRect,
            IReadOnlyList<LabelOnGround>? allLabels)
        {
            long targetAddress = targetLabel.Address;
            List<long> candidates = GetBlockerAddressScratch();
            candidates.Clear();

            int minX = CellIndex(targetRect.Left);
            int maxX = CellIndex(targetRect.Right);
            int minY = CellIndex(targetRect.Top);
            int maxY = CellIndex(targetRect.Bottom);
            for (int cy = minY; cy <= maxY; cy++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    if (!GetOrBuildBlockerGrid(allLabels).TryGetValue(CellKey(cx, cy), out List<long>? cell))
                        continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        long address = cell[i];
                        if (address != targetAddress)
                            candidates.Add(address);
                    }
                }
            }
            return candidates;
        }

        private Dictionary<long, List<long>> GetOrBuildBlockerGrid(IReadOnlyList<LabelOnGround>? allLabels)
        {
            long now = Environment.TickCount64;
            lock (_cacheLock)
            {
                if (_gridCells != null
                    && ReferenceEquals(_gridLabelsRef, allLabels)
                    && now - _gridBuiltAtMs < BlockerGridWindowMs)
                    return _gridCells;

                Dictionary<long, List<long>> cells = [];
                if (allLabels != null)
                {
                    for (int i = 0; i < allLabels.Count; i++)
                    {
                        LabelOnGround? other = allLabels[i];
                        if (other == null)
                            continue;
                        (RectangleF otherRect, bool hasRect) = ResolveLabelRectCached(other);
                        if (!hasRect)
                            continue;
                        int minX = CellIndex(otherRect.Left);
                        int maxX = CellIndex(otherRect.Right);
                        int minY = CellIndex(otherRect.Top);
                        int maxY = CellIndex(otherRect.Bottom);
                        for (int cy = minY; cy <= maxY; cy++)
                        {
                            for (int cx = minX; cx <= maxX; cx++)
                            {
                                long key = CellKey(cx, cy);
                                if (!cells.TryGetValue(key, out List<long>? cell))
                                {
                                    cell = [];
                                    cells[key] = cell;
                                }
                                cell.Add(other.Address);
                            }
                        }
                    }
                }
                _gridCells = cells;
                _gridLabelsRef = allLabels;
                _gridBuiltAtMs = now;
                return _gridCells;
            }
        }

        private (RectangleF Rect, bool HasRect) ResolveLabelRectByAddressCached(long address)
        {
            lock (_cacheLock)
            {
                if (_rectCache.TryGetValue(address, out (RectangleF Rect, bool HasRect) cached))
                    return cached;
                return (default, false);
            }
        }

        [ThreadStatic]
        private static List<long>? s_blockerAddressScratch;
        private static List<long> GetBlockerAddressScratch()
            => s_blockerAddressScratch ??= [];

        private static int CellIndex(float value)
            => (int)MathF.Floor(value / BlockerGridCellSize);

        private static long CellKey(int cellX, int cellY)
            => ((long)cellX << 32) | (uint)cellY;

        private (RectangleF Rect, bool HasRect) ResolveLabelRectCached(LabelOnGround label)
        {
            long address = label.Address;
            long now = Environment.TickCount64;
            lock (_cacheLock)
            {
                if (now - _rectCacheWindowStartMs >= RectCacheWindowMs)
                {
                    _rectCache.Clear();
                    _rectCacheWindowStartMs = now;
                }

                if (_rectCache.TryGetValue(address, out (RectangleF Rect, bool HasRect) cached))
                    return cached;

                bool hasRect = LabelGeometry.TryGetLabelRect(label, out RectangleF rect);
                (RectangleF Rect, bool HasRect) result = (rect, hasRect);
                _rectCache[address] = result;
                return result;
            }
        }

        private void ResolveLabelMetadataCached(LabelOnGround? label, out EntityType itemType, out string? itemPath, out string? renderName)
        {
            if (label == null)
            {
                itemType = EntityType.WorldItem;
                itemPath = null;
                renderName = null;
                return;
            }

            long address = label.Address;
            long now = Environment.TickCount64;
            lock (_cacheLock)
            {
                if (now - _metadataCacheWindowStartMs >= MetadataCacheWindowMs)
                {
                    _metadataCache.Clear();
                    _metadataCacheWindowStartMs = now;
                }
                if (_metadataCache.TryGetValue(address, out ResolvedLabelMetadata cached))
                {
                    itemType = cached.ItemType;
                    itemPath = cached.ItemPath;
                    renderName = cached.RenderName;
                    return;
                }

                ResolveLabelMetadata(label, out itemType, out itemPath, out renderName);
                _metadataCache[address] = new ResolvedLabelMetadata(itemType, itemPath, renderName);
            }
        }

        private static void ResolveLabelMetadata(LabelOnGround? label, out EntityType itemType, out string? itemPath, out string? renderName)
        {
            itemType = EntityType.WorldItem;
            itemPath = null;
            renderName = null;

            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem) || rawItem == null)
                return;

            if (DynamicAccess.TryGetDynamicValue(rawItem, DynamicAccessProfiles.Type, out object? rawType) && rawType != null)
                itemType = rawType switch
                {
                    EntityType entityType => entityType,
                    int entityTypeValue => (EntityType)entityTypeValue,
                    _ => EntityType.WorldItem,
                };

            if (DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.Path, out string resolvedPath))
                itemPath = resolvedPath;

            if (DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.RenderName, out string resolvedRenderName))
                renderName = resolvedRenderName;
        }
    }
}
