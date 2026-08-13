namespace ClickIt.Features.Strongboxes
{
    /// <summary>
    /// Owns the strongbox frame overlay end-to-end: every-frame refresh cadence (host coroutine)
    /// with a label-snapshot guard so the off-frame scan only runs when labels or settings change,
    /// and the per-frame draw of cached frames (green around the label's Child[0] text frame while
    /// unopened, red around the label element rect once opened). Decision helpers are
    /// internal static so the classifier and tests share the same rules.
    /// </summary>
    public sealed class StrongboxOverlay : IOverlay
    {
        private readonly BreakdownRecorder? _recordBreakdown;

        private const string StrongboxUniqueIdentifier = "special:strongbox-unique";
        private const float StrongboxCornerExclusion = 30f;
        private const float ChildLabelOverlapTolerance = 4f;

        public StrongboxOverlay(BreakdownRecorder? recordBreakdown = null)
        {
            _recordBreakdown = recordBreakdown;
        }

        private readonly record struct StrongboxFrame(RectangleF Rect, Color Color);
        private readonly record struct StrongboxRenderState(
            bool ShowFrames,
            IReadOnlyList<string> ClickMetadata,
            IReadOnlyList<string> DontClickMetadata);
        private readonly record struct StrongboxLabelMetadata(Chest? Chest, string Path, string RenderName, bool IsUnique);
        private readonly record struct CachedStrongbox(LabelOnGround Label, StrongboxLabelMetadata Metadata);

        private static readonly List<CachedStrongbox> s_emptyStrongboxes = [];

        private readonly OverlaySnapshot<List<CachedStrongbox>> _snapshot = new(s_emptyStrongboxes);

        private IReadOnlyList<string> _cachedClickMetadata = [];
        private IReadOnlyList<string> _cachedDontClickMetadata = [];
        private HashSet<string>? _clickIdsSnapshot;
        private HashSet<string>? _dontClickIdsSnapshot;
        private IReadOnlyList<LabelOnGround>? _lastScannedLabels;
        private int _lastScannedCount;
        private StrongboxRenderState _lastScannedRenderState;

        public string Name => "Strongbox";

        public RenderSection Section => RenderSection.StrongboxOverlay;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.Throttled(0);

        public TimingChannel? RefreshTimingChannel => TimingChannel.LabelOverlay;

        public ProcessingSection ProcessingSection => ProcessingSection.Strongbox;

        public bool IsEnabled(ClickItSettings settings)
            => settings.ShowStrongboxFrames.Value || (settings.StrongboxClickIds?.Count ?? 0) > 0;

        // Coroutine thread — the only place this overlay reads labels / game memory.
        public void Refresh(OverlayRefreshContext ctx)
        {
            long metaStart = Stopwatch.GetTimestamp();
            long metaAllocStart = GC.GetAllocatedBytesForCurrentThread();
            EnsureStrongboxMetadataCache(ctx.Settings);
            double metaMs = (Stopwatch.GetTimestamp() - metaStart) * 1000.0 / Stopwatch.Frequency;
            long metaBytes = GC.GetAllocatedBytesForCurrentThread() - metaAllocStart;

            long resolveStart = Stopwatch.GetTimestamp();
            long resolveAllocStart = GC.GetAllocatedBytesForCurrentThread();
            StrongboxRenderState renderState = ResolveRenderState(ctx.Settings);
            double resolveMs = (Stopwatch.GetTimestamp() - resolveStart) * 1000.0 / Stopwatch.Frequency;
            long resolveBytes = GC.GetAllocatedBytesForCurrentThread() - resolveAllocStart;

            if (!ShouldRenderAnyStrongboxes(renderState))
            {
                _snapshot.Replace(s_emptyStrongboxes);
                _lastScannedLabels = null;
                RecordStrongboxBreakdown(metaBytes, metaMs, resolveBytes, resolveMs, 0, 0);
                return;
            }

            // The refresh runs every frame; the expensive scan only re-runs when the label snapshot or the render state (settings) actually changed.
            if (ReferenceEquals(_lastScannedLabels, ctx.Labels)
                && _lastScannedCount == (ctx.Labels?.Count ?? 0)
                && _lastScannedRenderState == renderState)
            {
                RecordStrongboxBreakdown(metaBytes, metaMs, resolveBytes, resolveMs, 0, 0);
                return;
            }

            _lastScannedLabels = ctx.Labels;
            _lastScannedCount = ctx.Labels?.Count ?? 0;
            _lastScannedRenderState = renderState;

            long scanStart = Stopwatch.GetTimestamp();
            long scanAllocStart = GC.GetAllocatedBytesForCurrentThread();
            _snapshot.Replace(ScanStrongboxes(ctx.Labels, renderState));
            double scanMs = (Stopwatch.GetTimestamp() - scanStart) * 1000.0 / Stopwatch.Frequency;
            long scanBytes = GC.GetAllocatedBytesForCurrentThread() - scanAllocStart;

            RecordStrongboxBreakdown(metaBytes, metaMs, resolveBytes, resolveMs, scanBytes, scanMs);
        }

        private void RecordStrongboxBreakdown(
            long metaBytes, double metaMs,
            long resolveBytes, double resolveMs,
            long scanBytes, double scanMs)
        {
            if (_recordBreakdown == null)
                return;
            Span<long> bytes = stackalloc long[3];
            Span<double> ms = stackalloc double[3];
            bytes[0] = metaBytes; ms[0] = metaMs;
            bytes[1] = resolveBytes; ms[1] = resolveMs;
            bytes[2] = scanBytes; ms[2] = scanMs;
            _recordBreakdown.Invoke(bytes, ms);
        }

        // Render thread — cached data + fresh per-frame projection, enqueue only.
        public void Draw(OverlayRenderContext ctx)
        {
            StrongboxRenderState renderState = ResolveRenderState(ctx.Settings);
            List<CachedStrongbox> cached = _snapshot.Current;
            for (int i = 0; i < cached.Count; i++)
            {
                CachedStrongbox sb = cached[i];
                if (!TryResolveStrongboxFrame(sb, ctx.WindowArea, renderState, out StrongboxFrame frame))
                    continue;

                ctx.FrameQueue.Enqueue(frame.Rect, frame.Color, 2);
            }
        }

        private static bool TryResolveStrongboxRect(CachedStrongbox sb, RectangleF windowArea, bool chestLocked, out RectangleF rect)
        {
            // Unopened (green) boxes hug the label's Child[0] text frame, resolved fresh each frame so the box tracks the moving label with no scan latency; opened (red) boxes use the label element rect because the child frame is rebuilt when the strongbox opens. A child that is still being laid out (label entering/leaving the screen) reports its local rect instead of its on-screen position — reject it unless it sits within the label element.
            if (!chestLocked
                && TryResolveFreshChildLabelRect(sb.Label, windowArea, out rect)
                && IsPositionedRect(rect)
                && LabelGeometry.TryGetLabelRectOnScreen(sb.Label, windowArea, out RectangleF labelRect)
                && IsPositionedRect(labelRect)
                && IsChildFrameWithinLabel(rect, labelRect))
            {
                return true;
            }

            // A mid-layout label element briefly reports a rect in the window's top-left corner; skip it so the frame doesn't flash there before the layout settles.
            return LabelGeometry.TryGetLabelRectOnScreen(sb.Label, windowArea, out rect)
                && IsPositionedRect(rect);
        }

        private static bool IsPositionedRect(RectangleF rect)
            => rect.X >= StrongboxCornerExclusion || rect.Y >= StrongboxCornerExclusion;

        private static bool TryResolveFreshChildLabelRect(LabelOnGround label, RectangleF windowArea, out RectangleF rect)
        {
            rect = default;
            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                || rawLabel is not Element labelElement
                || !DynamicAccess.TryGetChildAtIndex(labelElement, 0, out object? rawChild)
                || rawChild is not Element child)
            {
                return false;
            }

            return LabelGeometry.TryGetElementRectOnScreen(child, windowArea, out rect);
        }

        private static bool IsChildFrameWithinLabel(RectangleF childRect, RectangleF labelRect)
        {
            RectangleF expanded = new(
                labelRect.X - ChildLabelOverlapTolerance,
                labelRect.Y - ChildLabelOverlapTolerance,
                labelRect.Width + (ChildLabelOverlapTolerance * 2f),
                labelRect.Height + (ChildLabelOverlapTolerance * 2f));
            return expanded.Intersects(childRect);
        }

        // The scan caches strongboxes by label identity (position-independent); the per-frame draw projects each cached label's rect and culls to the window, so any partially visible strongbox renders the moment its label is on screen — no rescan required.
        private static List<CachedStrongbox> ScanStrongboxes(
            IReadOnlyList<LabelOnGround>? labels,
            StrongboxRenderState renderState)
        {
            if (labels == null)
                return [];

            List<CachedStrongbox> strongboxes = [];
            foreach (LabelOnGround label in labels)
            {
                StrongboxLabelMetadata metadata = ResolveStrongboxLabelMetadata(label);
                if (string.IsNullOrEmpty(metadata.Path) || metadata.Path.IndexOf("strongbox", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!IsStrongboxClickableBySettings(metadata.Path, metadata.RenderName, renderState.ClickMetadata, renderState.DontClickMetadata, metadata.IsUnique))
                    continue;

                strongboxes.Add(new CachedStrongbox(label, metadata));
            }

            return strongboxes;
        }

        private StrongboxRenderState ResolveRenderState(ClickItSettings settings)
            => new(
                settings.ShowStrongboxFrames.Value,
                _cachedClickMetadata,
                _cachedDontClickMetadata);

        private static bool ShouldRenderAnyStrongboxes(StrongboxRenderState renderState)
            => renderState.ShowFrames || renderState.ClickMetadata.Count > 0;

        private static bool TryResolveStrongboxFrame(
            CachedStrongbox sb,
            RectangleF windowArea,
            StrongboxRenderState renderState,
            out StrongboxFrame frame)
        {
            frame = default;

            if (!renderState.ShowFrames)
                return false;

            StrongboxLabelMetadata metadata = sb.Metadata;
            string itemPathRaw = metadata.Path;
            if (string.IsNullOrEmpty(itemPathRaw) || itemPathRaw.IndexOf("strongbox", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (!IsStrongboxClickableBySettings(itemPathRaw, metadata.RenderName, renderState.ClickMetadata, renderState.DontClickMetadata, metadata.IsUnique))
                return false;

            bool chestLocked = TryReadChestLocked(metadata.Chest);
            if (!TryResolveStrongboxRect(sb, windowArea, chestLocked, out RectangleF rect))
                return false;

            frame = new StrongboxFrame(rect, chestLocked ? Color.Red : Color.LawnGreen);
            return true;
        }

        private static bool TryReadChestLocked(Chest? chest)
            => chest != null && DynamicAccess.TryReadBool(chest, DynamicAccessProfiles.IsLocked, out bool isLocked) && isLocked;

        internal static bool ContainsStrongboxUniqueIdentifier(IReadOnlyList<string>? metadataIdentifiers)
        {
            if (metadataIdentifiers == null || metadataIdentifiers.Count == 0)
                return false;

            for (int i = 0; i < metadataIdentifiers.Count; i++)
            {
                if (string.Equals(metadataIdentifiers[i], StrongboxUniqueIdentifier, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static bool IsStrongboxClickableBySettings(string path, string itemName, IReadOnlyList<string> clickMetadata, IReadOnlyList<string> dontClickMetadata, bool isUniqueStrongbox)
        {
            if (string.IsNullOrEmpty(path) || clickMetadata == null || clickMetadata.Count == 0)
                return false;

            if (isUniqueStrongbox)
            {
                if (ContainsStrongboxUniqueIdentifier(dontClickMetadata))
                    return false;

                return ContainsStrongboxUniqueIdentifier(clickMetadata);
            }

            bool dontClickMatch = MetadataIdentifierMatcher.ContainsAny(path, itemName, dontClickMetadata);

            if (dontClickMatch)
                return false;

            return MetadataIdentifierMatcher.ContainsAny(path, itemName, clickMetadata);
        }

        private static bool HasMatchingSnapshot(HashSet<string>? currentIds, HashSet<string>? snapshot)
        {
            if (currentIds == null)
                return snapshot == null || snapshot.Count == 0;

            if (snapshot == null)
                return false;

            return snapshot.SetEquals(currentIds);
        }

        private void EnsureStrongboxMetadataCache(ClickItSettings settings)
        {
            if (HasMatchingSnapshot(settings.StrongboxClickIds, _clickIdsSnapshot)
                && HasMatchingSnapshot(settings.StrongboxDontClickIds, _dontClickIdsSnapshot))
            {
                return;
            }

            _cachedClickMetadata = settings.GetStrongboxClickMetadataIdentifiers();
            _cachedDontClickMetadata = settings.GetStrongboxDontClickMetadataIdentifiers();

            HashSet<string> currentClickIds = settings.StrongboxClickIds ?? [];
            HashSet<string> currentDontClickIds = settings.StrongboxDontClickIds ?? [];
            _clickIdsSnapshot = new HashSet<string>(currentClickIds, StringComparer.OrdinalIgnoreCase);
            _dontClickIdsSnapshot = new HashSet<string>(currentDontClickIds, StringComparer.OrdinalIgnoreCase);
        }

        private static StrongboxLabelMetadata ResolveStrongboxLabelMetadata(LabelOnGround? label)
        {
            object? rawItem = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? itemValue)
                ? itemValue
                : null;

            string path = DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            string renderName = DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.RenderName, out string resolvedRenderName)
                ? resolvedRenderName
                : string.Empty;
            bool isUnique = DynamicAccess.TryGetDynamicValue(rawItem, DynamicAccessProfiles.Rarity, out object? rawRarity)
                && rawRarity is MonsterRarity rarity
                && rarity == MonsterRarity.Unique;
            Chest? chest = DynamicAccess.TryGetComponent(rawItem, out Chest? resolvedChest)
                ? resolvedChest
                : null;

            return new StrongboxLabelMetadata(chest, path, renderName, isUnique);
        }
    }
}
