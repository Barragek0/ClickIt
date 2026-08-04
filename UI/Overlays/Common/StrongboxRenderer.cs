namespace ClickIt.UI.Overlays.Common
{
    public class StrongboxRenderer(ClickItSettings settings, DeferredFrameQueue deferredFrameQueue)
    {
        private const string StrongboxUniqueIdentifier = "special:strongbox-unique";
        private readonly record struct StrongboxFrame(RectangleF Rect, Color Color);
        private readonly record struct StrongboxRenderState(
            bool ShowFrames,
            IReadOnlyList<string> ClickMetadata,
            IReadOnlyList<string> DontClickMetadata);

        private const int StrongboxScanIntervalMs = 100;
        private readonly record struct CachedStrongbox(LabelOnGround Label, StrongboxLabelMetadata Metadata);

        private readonly DeferredFrameQueue _deferredFrameQueue = deferredFrameQueue;
        private readonly ClickItSettings _settings = settings;
        private IReadOnlyList<string> _cachedClickMetadata = [];
        private IReadOnlyList<string> _cachedDontClickMetadata = [];
        private HashSet<string>? _clickIdsSnapshot;
        private HashSet<string>? _dontClickIdsSnapshot;
        private long _lastStrongboxScanMs;
        private List<CachedStrongbox> _cachedStrongboxes = [];

        public void Render(GameController? gameController)
        {
            if (gameController == null) return;
            IList<LabelOnGround>? labels = gameController.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null) return;

            // Cast via dynamic to avoid assembly type conflicts when the test project
            RenderFromLabels((IEnumerable<LabelOnGround>)(dynamic)labels, gameController.Window.GetWindowRectangleTimeCache);
        }

        public void RenderFromLabels(IEnumerable<LabelOnGround> labels, RectangleF windowArea)
        {
            if (labels == null) return;

            EnsureStrongboxMetadataCache();

            StrongboxRenderState renderState = ResolveRenderState();
            if (!ShouldRenderAnyStrongboxes(renderState))
            {
                _cachedStrongboxes.Clear();
                return;
            }

            // Metadata resolution is expensive (dynamic reads + GetClientRect per label), so the
            // full scan is throttled; per frame we only re-check the (usually zero) cached strongboxes.
            if (Environment.TickCount64 - _lastStrongboxScanMs >= StrongboxScanIntervalMs)
            {
                _lastStrongboxScanMs = Environment.TickCount64;
                _cachedStrongboxes = ScanStrongboxes(labels, windowArea, renderState);
            }

            RenderCachedStrongboxFrames(windowArea, renderState);
        }

        private static List<CachedStrongbox> ScanStrongboxes(
            IEnumerable<LabelOnGround> labels,
            RectangleF windowArea,
            StrongboxRenderState renderState)
        {
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

        private void RenderCachedStrongboxFrames(RectangleF windowArea, StrongboxRenderState renderState)
        {
            for (int i = 0; i < _cachedStrongboxes.Count; i++)
            {
                CachedStrongbox cached = _cachedStrongboxes[i];
                if (!LabelGeometry.TryGetLabelRectOnScreen(cached.Label, windowArea, out RectangleF rect))
                    continue;

                if (!TryResolveStrongboxFrame(rect, renderState, cached.Metadata, out StrongboxFrame frame))
                    continue;

                EnqueueStrongboxFrame(frame);
            }
        }

        private void EnqueueStrongboxFrame(StrongboxFrame frame)
            => _deferredFrameQueue.Enqueue(frame.Rect, frame.Color, 2);

        private StrongboxRenderState ResolveRenderState()
            => new(
                _settings.ShowStrongboxFrames.Value,
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

        private void EnsureStrongboxMetadataCache()
        {
            if (HasMatchingSnapshot(_settings.StrongboxClickIds, _clickIdsSnapshot)
                && HasMatchingSnapshot(_settings.StrongboxDontClickIds, _dontClickIdsSnapshot))
            {
                return;
            }

            _cachedClickMetadata = _settings.GetStrongboxClickMetadataIdentifiers();
            _cachedDontClickMetadata = _settings.GetStrongboxDontClickMetadataIdentifiers();

            HashSet<string> currentClickIds = _settings.StrongboxClickIds ?? [];
            HashSet<string> currentDontClickIds = _settings.StrongboxDontClickIds ?? [];
            _clickIdsSnapshot = new HashSet<string>(currentClickIds, StringComparer.OrdinalIgnoreCase);
            _dontClickIdsSnapshot = new HashSet<string>(currentDontClickIds, StringComparer.OrdinalIgnoreCase);
        }

        private readonly record struct StrongboxLabelMetadata(Element? Label, Chest? Chest, string Path, string RenderName, bool IsUnique);

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

            return new StrongboxLabelMetadata(labelElement, chest, path, renderName, isUnique);
        }

    }
}
