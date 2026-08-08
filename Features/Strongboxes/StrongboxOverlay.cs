namespace ClickIt.Features.Strongboxes
{
    /// <summary>
    /// Owns the strongbox frame overlay end-to-end: every-frame refresh cadence (host coroutine)
    /// with a label-snapshot guard so the off-frame scan only runs when labels or settings change,
    /// and the per-frame draw of cached frames around each label's Child[0]. Decision helpers are
    /// internal static so the classifier and tests share the same rules.
    /// </summary>
    public sealed class StrongboxOverlay : IOverlay
    {
        private const string StrongboxUniqueIdentifier = "special:strongbox-unique";

        private readonly record struct StrongboxFrame(RectangleF Rect, Color Color);
        private readonly record struct StrongboxRenderState(
            bool ShowFrames,
            IReadOnlyList<string> ClickMetadata,
            IReadOnlyList<string> DontClickMetadata);
        private readonly record struct StrongboxLabelMetadata(Element? Label, Chest? Chest, string Path, string RenderName, bool IsUnique, Element? ChildLabel = null);
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
            EnsureStrongboxMetadataCache(ctx.Settings);

            StrongboxRenderState renderState = ResolveRenderState(ctx.Settings);
            if (!ShouldRenderAnyStrongboxes(renderState))
            {
                _snapshot.Replace(s_emptyStrongboxes);
                _lastScannedLabels = null;
                return;
            }

            // The refresh runs every frame; the expensive scan only re-runs when the label snapshot
            // or the render state (settings) actually changed.
            if (ReferenceEquals(_lastScannedLabels, ctx.Labels)
                && _lastScannedCount == (ctx.Labels?.Count ?? 0)
                && _lastScannedRenderState == renderState)
            {
                return;
            }

            _lastScannedLabels = ctx.Labels;
            _lastScannedCount = ctx.Labels?.Count ?? 0;
            _lastScannedRenderState = renderState;
            _snapshot.Replace(ScanStrongboxes(ctx.Labels, ctx.WindowArea, renderState));
        }

        // Render thread — cached data + fresh per-frame projection, enqueue only.
        public void Draw(OverlayRenderContext ctx)
        {
            StrongboxRenderState renderState = ResolveRenderState(ctx.Settings);
            List<CachedStrongbox> cached = _snapshot.Current;
            for (int i = 0; i < cached.Count; i++)
            {
                CachedStrongbox sb = cached[i];
                // The label's actual text frame is Child[0]; the parent label rect is larger, so the
                // box is projected from the child (cached during the scan, rect resolved per frame).
                if (!TryResolveStrongboxRect(sb, ctx.WindowArea, out RectangleF rect))
                    continue;

                if (!TryResolveStrongboxFrame(rect, renderState, sb.Metadata, out StrongboxFrame frame))
                    continue;

                ctx.FrameQueue.Enqueue(frame.Rect, frame.Color, 2);
            }
        }

        private static bool TryResolveStrongboxRect(CachedStrongbox sb, RectangleF windowArea, out RectangleF rect)
            => sb.Metadata.ChildLabel is { } child
                ? LabelGeometry.TryGetElementRectOnScreen(child, windowArea, out rect)
                : LabelGeometry.TryGetLabelRectOnScreen(sb.Label, windowArea, out rect);

        private static List<CachedStrongbox> ScanStrongboxes(
            IReadOnlyList<LabelOnGround>? labels,
            RectangleF windowArea,
            StrongboxRenderState renderState)
        {
            if (labels == null)
                return [];

            List<CachedStrongbox> strongboxes = [];
            foreach (LabelOnGround label in labels)
            {
                if (!LabelGeometry.TryGetLabelRectOnScreen(label, windowArea, out _))
                    continue;

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
            RectangleF rect,
            StrongboxRenderState renderState,
            StrongboxLabelMetadata metadata,
            out StrongboxFrame frame)
        {
            frame = default;

            if (!renderState.ShowFrames)
                return false;

            string itemPathRaw = metadata.Path;
            if (string.IsNullOrEmpty(itemPathRaw) || itemPathRaw.IndexOf("strongbox", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (!IsStrongboxClickableBySettings(itemPathRaw, metadata.RenderName, renderState.ClickMetadata, renderState.DontClickMetadata, metadata.IsUnique))
                return false;

            frame = new StrongboxFrame(rect, ResolveStrongboxFrameColor(metadata));
            return true;
        }

        private static Color ResolveStrongboxFrameColor(StrongboxLabelMetadata metadata)
        {
            bool chestLocked = metadata.Chest?.IsLocked == true;
            return chestLocked ? Color.Red : Color.LawnGreen;
        }

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
            Element? labelElement = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                ? rawLabel as Element
                : null;

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

            // The box is drawn around the label's text frame (Child[0]); resolved once per scan so
            // the per-frame draw only reads its cached rect.
            Element? child0 = DynamicAccess.TryGetChildAtIndex(labelElement, 0, out object? rawChild0)
                ? rawChild0 as Element
                : null;

            return new StrongboxLabelMetadata(labelElement, chest, path, renderName, isUnique, child0);
        }
    }
}
