namespace ClickIt.UI.Overlays.Common
{
    public class StrongboxRenderer(ClickItSettings settings, DeferredFrameQueue deferredFrameQueue)
    {
        private const string StrongboxUniqueIdentifier = "special:strongbox-unique";
        private readonly record struct StrongboxFrame(SharpDX.RectangleF Rect, Color Color);
        private readonly record struct StrongboxRenderState(
            bool ShowFrames,
            IReadOnlyList<string> ClickMetadata,
            IReadOnlyList<string> DontClickMetadata);

        private readonly DeferredFrameQueue _deferredFrameQueue = deferredFrameQueue;
        private readonly ClickItSettings _settings = settings;
        private IReadOnlyList<string> _cachedClickMetadata = [];
        private IReadOnlyList<string> _cachedDontClickMetadata = [];
        private HashSet<string>? _clickIdsSnapshot;
        private HashSet<string>? _dontClickIdsSnapshot;

        public void Render(GameController? gameController, object? state)
        {
            if (gameController == null) return;
            var labels = gameController.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null) return;

            // Cast via dynamic to avoid assembly type conflicts when the test project
            RenderFromLabels((IEnumerable<LabelOnGround>)(dynamic)labels, gameController.Window.GetWindowRectangleTimeCache);
        }

        public void RenderFromLabels(IEnumerable<LabelOnGround> labels, SharpDX.RectangleF windowArea)
        {
            if (labels == null) return;

            EnsureStrongboxMetadataCache();

            StrongboxRenderState renderState = ResolveRenderState();
            if (!ShouldRenderAnyStrongboxes(renderState))
            {
                return;
            }

            RenderStrongboxFrames(labels, windowArea, renderState);
        }

        private void RenderStrongboxFrames(
            IEnumerable<LabelOnGround> labels,
            SharpDX.RectangleF windowArea,
            StrongboxRenderState renderState)
        {
            foreach (var label in labels)
            {
                if (!TryResolveStrongboxFrame(label, windowArea, renderState, out StrongboxFrame frame))
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
            LabelOnGround? label,
            SharpDX.RectangleF windowArea,
            StrongboxRenderState renderState,
            out StrongboxFrame frame)
        {
            frame = default;

            if (!renderState.ShowFrames)
                return false;

            if (!TryGetVisibleLabelRect(label, windowArea, out SharpDX.RectangleF rect, out string? itemPathRaw))
                return false;

            string renderName = label?.ItemOnGround?.RenderName ?? string.Empty;
            bool isUniqueStrongbox = IsUniqueStrongbox(label);
            if (!IsStrongboxClickableBySettings(itemPathRaw!, renderName, renderState.ClickMetadata, renderState.DontClickMetadata, isUniqueStrongbox))
                return false;

            frame = new StrongboxFrame(rect, ResolveStrongboxFrameColor(label));
            return true;
        }

        private static Color ResolveStrongboxFrameColor(LabelOnGround? label)
        {
            var chestComp = label?.ItemOnGround?.GetComponent<Chest>();
            bool chestLocked = chestComp?.IsLocked == true;
            return chestLocked ? Color.Red : Color.LawnGreen;
        }

        private static bool TryGetVisibleLabelRect(LabelOnGround? label, SharpDX.RectangleF windowArea, out SharpDX.RectangleF rect, out string? itemPathRaw)
        {
            rect = new SharpDX.RectangleF();
            itemPathRaw = label?.ItemOnGround?.Path;
            if (string.IsNullOrEmpty(itemPathRaw)) return false;
            if (itemPathRaw.IndexOf("strongbox", StringComparison.OrdinalIgnoreCase) < 0) return false;

            var elem = label?.Label;
            if (elem == null || !elem.IsValid) return false;

            // Tests can surface a non-RectangleF return here through assembly-shim boundaries.
            object? maybeRectObj = elem.GetClientRect();
            if (maybeRectObj == null) return false;

            if (maybeRectObj is SharpDX.RectangleF rectVal)
            {
                rect = rectVal;
            }
            else
            {
                return false;
            }
            if (rect.Width <= 0 || rect.Height <= 0) return false;

            var rectAbs = new SharpDX.RectangleF(rect.X + windowArea.X, rect.Y + windowArea.Y, rect.Width, rect.Height);
            if (!rectAbs.Intersects(windowArea)) return false;

            return true;
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

        private static bool IsUniqueStrongbox(LabelOnGround? label)
        {
            return label?.ItemOnGround?.Rarity == MonsterRarity.Unique;
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

            var currentClickIds = _settings.StrongboxClickIds ?? [];
            var currentDontClickIds = _settings.StrongboxDontClickIds ?? [];
            _clickIdsSnapshot = new HashSet<string>(currentClickIds, StringComparer.OrdinalIgnoreCase);
            _dontClickIdsSnapshot = new HashSet<string>(currentDontClickIds, StringComparer.OrdinalIgnoreCase);
        }

    }
}
