using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using Color = SharpDX.Color;
using ClickIt.Utils;

namespace ClickIt.Rendering
{
    // Responsible for drawing debug frames around strongboxes (locked/unlocked)
    public class StrongboxRenderer(ClickItSettings settings, DeferredFrameQueue deferredFrameQueue)
    {
        private const string StrongboxUniqueIdentifier = "special:strongbox-unique";
        private readonly DeferredFrameQueue _deferredFrameQueue = deferredFrameQueue;
        private readonly ClickItSettings _settings = settings;
        private IReadOnlyList<string> _cachedClickMetadata = Array.Empty<string>();
        private IReadOnlyList<string> _cachedDontClickMetadata = Array.Empty<string>();
        private HashSet<string>? _clickIdsSnapshot;
        private HashSet<string>? _dontClickIdsSnapshot;

        public void Render(GameController? gameController, object? state)
        {
            if (gameController == null) return;
            var labels = gameController.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null) return;

            // Cast via dynamic to avoid assembly type conflicts when the test project
            // provides its own mock ExileCore types (tests include lightweight fakes).
            RenderFromLabels((IEnumerable<LabelOnGround>)(dynamic)labels, gameController.Window.GetWindowRectangleTimeCache);
        }

        // Exposed for tests — pass in the current window rectangle so we can determine on-screen intersection.
        public void RenderFromLabels(IEnumerable<LabelOnGround> labels, SharpDX.RectangleF windowArea)
        {
            if (labels == null) return;

            EnsureStrongboxMetadataCache();

            bool showFrames = _settings.ShowStrongboxFrames.Value;
            IReadOnlyList<string> clickMetadata = _cachedClickMetadata;
            IReadOnlyList<string> dontClickMetadata = _cachedDontClickMetadata;
            bool anyTypeEnabled = clickMetadata.Count > 0;

            if (!showFrames && !anyTypeEnabled)
            {
                // Nothing to display or click for strongboxes
                return;
            }

            foreach (var label in labels)
            {
                if (!TryGetVisibleLabelRect(label, windowArea, out var rect, out var itemPathRaw))
                    continue;

                string renderName = label?.ItemOnGround?.RenderName ?? string.Empty;
                bool isUniqueStrongbox = IsUniqueStrongbox(label);
                bool isClickableBySettings = IsStrongboxClickableBySettings(itemPathRaw!, renderName, clickMetadata, dontClickMetadata, isUniqueStrongbox);
                if (!isClickableBySettings || !showFrames)
                    continue;

                var chestComp = label?.ItemOnGround?.GetComponent<ExileCore.PoEMemory.Components.Chest>();
                bool chestLocked = chestComp?.IsLocked == true;

                var color = chestLocked ? Color.Red : Color.LawnGreen;
                _deferredFrameQueue.Enqueue(rect, color, 2);
            }
        }

        private static bool TryGetVisibleLabelRect(LabelOnGround? label, SharpDX.RectangleF windowArea, out SharpDX.RectangleF rect, out string? itemPathRaw)
        {
            rect = new SharpDX.RectangleF();
            itemPathRaw = label?.ItemOnGround?.Path;
            if (string.IsNullOrEmpty(itemPathRaw)) return false;
            if (itemPathRaw.IndexOf("strongbox", StringComparison.OrdinalIgnoreCase) < 0) return false;

            var elem = label?.Label;
            if (elem == null || !elem.IsValid) return false;

            // ExileCore's Element.GetClientRect sometimes returns a nullable RectangleF
            // and in other contexts (or different runtime assemblies) it returns a
            // non-nullable RectangleF. To remain compatible with both, retrieve the
            // raw boxed result and handle both cases safely.
            object? maybeRectObj = elem.GetClientRect();
            if (maybeRectObj == null) return false;

            // If getClientRect returned either RectangleF (boxed) or a nullable
            // RectangleF with a value (which boxes to RectangleF), the `is`
            // pattern above will match and we can extract it. Any other value
            // (including a boxed null) means there's no valid rect.
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

        private static bool ContainsStrongboxUniqueIdentifier(IReadOnlyList<string> metadataIdentifiers)
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

        private static bool IsStrongboxClickableBySettings(string path, string itemName, IReadOnlyList<string> clickMetadata, IReadOnlyList<string> dontClickMetadata, bool isUniqueStrongbox)
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
