using ExileCore;
using ExileCore.PoEMemory.Elements;
using Color = SharpDX.Color;
using ClickIt.Utils;

#nullable enable

namespace ClickIt.Rendering
{
    // Responsible for drawing debug frames around strongboxes (locked/unlocked)
    public class StrongboxRenderer(ClickItSettings settings, DeferredFrameQueue deferredFrameQueue)
    {
        private readonly DeferredFrameQueue _deferredFrameQueue = deferredFrameQueue;
        private readonly ClickItSettings _settings = settings;

        public void Render(GameController? gameController, object? state)
        {
            if (gameController == null) return;
            var labels = gameController.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null) return;

            // Cast via dynamic to avoid assembly type conflicts when the test project
            // provides its own mock ExileCore types (tests include lightweight fakes).
            RenderFromLabels((IEnumerable<LabelOnGround>)(dynamic)labels, gameController.Window.GetWindowRectangleTimeCache);
        }

        /// <summary>
        /// Count visible strongbox labels on screen. This is used by the render gate to decide
        /// whether we should perform a render pass even if other render flags are disabled.
        /// </summary>
        public int GetStrongboxCount(GameController? gameController)
        {
            if (gameController == null) return 0;
            var labels = gameController.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null) return 0;

            var windowArea = gameController.Window.GetWindowRectangleTimeCache;
            int count = 0;
            foreach (dynamic label in labels)
            {
                if (label == null) continue;
                // When calling a method using a dynamic argument the compiler
                // can't infer the types of implicitly-typed discards. Use
                // explicitly-typed out parameters here to avoid CS8183.
                if (TryGetVisibleLabelRect((LabelOnGround?)label, windowArea, out SharpDX.RectangleF _, out string? _))
                    count++;
            }

            return count;
        }


        // Exposed for tests — pass in the current window rectangle so we can determine on-screen intersection.
        public void RenderFromLabels(IEnumerable<LabelOnGround> labels, SharpDX.RectangleF windowArea)
        {
            if (labels == null) return;

            bool showFrames = _settings.ShowStrongboxFrames.Value;
            bool anyTypeEnabled = _settings.RegularStrongbox.Value || _settings.ArcanistStrongbox.Value ||
                                  _settings.ArmourerStrongbox.Value || _settings.ArtisanStrongbox.Value ||
                                  _settings.BlacksmithStrongbox.Value || _settings.CartographerStrongbox.Value ||
                                  _settings.DivinerStrongbox.Value || _settings.GemcutterStrongbox.Value ||
                                  _settings.JewellerStrongbox.Value || _settings.LargeStrongbox.Value ||
                                  _settings.OrnateStrongbox.Value;

            if (!showFrames && !anyTypeEnabled)
            {
                // Nothing to display or click for strongboxes
                return;
            }

            // Build a small list of enabled path keys so we can cheaply test membership per-label
            var enabledKeys = GetEnabledStrongboxKeys();

            foreach (var label in labels)
            {
                if (!TryGetVisibleLabelRect(label, windowArea, out var rect, out var itemPathRaw))
                    continue;

                bool isClickableBySettings = IsStrongboxClickableBySettings(itemPathRaw!, enabledKeys);
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

        public int GetStrongboxCount(IEnumerable<LabelOnGround>? labels, SharpDX.RectangleF windowArea)
        {
            if (labels == null) return 0;

            int count = 0;
            foreach (var label in labels)
            {
                if (label == null) continue;
                if (TryGetVisibleLabelRect(label, windowArea, out _, out _)) count++;
            }

            return count;
        }

        private List<string> GetEnabledStrongboxKeys()
        {
            var enabledKeys = new List<string>(11);
            if (_settings.RegularStrongbox.Value) enabledKeys.Add("StrongBoxes/Strongbox");
            if (_settings.ArcanistStrongbox.Value) enabledKeys.Add("StrongBoxes/Arcanist");
            if (_settings.ArmourerStrongbox.Value) enabledKeys.Add("StrongBoxes/Armory");
            if (_settings.ArtisanStrongbox.Value) enabledKeys.Add("StrongBoxes/Artisan");
            if (_settings.BlacksmithStrongbox.Value) enabledKeys.Add("StrongBoxes/Arsenal");
            if (_settings.CartographerStrongbox.Value) enabledKeys.Add("StrongBoxes/CartographerEndMaps");
            if (_settings.DivinerStrongbox.Value) enabledKeys.Add("StrongBoxes/StrongboxDivination");
            if (_settings.GemcutterStrongbox.Value) enabledKeys.Add("StrongBoxes/Gemcutter");
            if (_settings.JewellerStrongbox.Value) enabledKeys.Add("StrongBoxes/Jeweller");
            if (_settings.LargeStrongbox.Value) enabledKeys.Add("StrongBoxes/Large");
            if (_settings.OrnateStrongbox.Value) enabledKeys.Add("StrongBoxes/Ornate");
            return enabledKeys;
        }

        // Internal helper used by tests to inspect enqueued frames
        internal (SharpDX.RectangleF Rectangle, Color Color, int Thickness)[] GetEnqueuedFramesForTests()
        {
            return _deferredFrameQueue.GetSnapshotForTests();
        }

        private bool IsStrongboxClickableBySettings(string path, IReadOnlyList<string> enabledKeys)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (enabledKeys == null || enabledKeys.Count == 0) return false;

            // Iterate only the enabled keys and match the path case-insensitively.
            for (int i = 0; i < enabledKeys.Count; i++)
            {
                if (path.IndexOf(enabledKeys[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

    }
}
