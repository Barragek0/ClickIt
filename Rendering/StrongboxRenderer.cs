using ExileCore;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using Color = SharpDX.Color;
using System;
using System.Linq;
using ClickIt;
using System.Collections.Generic;
using ClickIt.Utils;

#nullable enable

namespace ClickIt.Rendering
{
    // Responsible for drawing debug frames around strongboxes (locked/unlocked)
    public class StrongboxRenderer
    {
        private readonly DeferredFrameQueue _deferredFrameQueue;
        private readonly ClickItSettings _settings;

        public StrongboxRenderer(ClickItSettings settings, DeferredFrameQueue? deferredFrameQueue = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _deferredFrameQueue = deferredFrameQueue ?? new DeferredFrameQueue();
        }

        public void Render(GameController? gameController, object? state)
        {
            if (gameController == null) return;
            var labels = gameController.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null) return;

            RenderFromLabels(labels, gameController.Window.GetWindowRectangleTimeCache);
        }

        // Exposed for tests — pass in the current window rectangle so we can determine on-screen intersection.
        public void RenderFromLabels(IEnumerable<LabelOnGround> labels, SharpDX.RectangleF windowArea)
        {
            if (labels == null) return;

            foreach (var label in labels)
            {
                var itemPathRaw = label?.ItemOnGround?.Path ?? string.Empty;
                var itemPath = itemPathRaw.ToLowerInvariant();
                if (string.IsNullOrEmpty(itemPath) || !itemPath.Contains("strongbox"))
                    continue;

                var labelElem = label?.Label;
                if (labelElem == null || !labelElem.IsValid) continue;

                var rect = labelElem.GetClientRect();
                if (rect.Width <= 0 || rect.Height <= 0) continue;

                // Convert to absolute window coordinates and make sure some part is on-screen.
                var rectAbs = new SharpDX.RectangleF(rect.X + windowArea.X, rect.Y + windowArea.Y, rect.Width, rect.Height);
                if (!rectAbs.Intersects(windowArea))
                    continue; // completely off-screen => skip

                var chestComp = label?.ItemOnGround?.GetComponent<ExileCore.PoEMemory.Components.Chest>();
                bool chestLocked = chestComp?.IsLocked == true;

                var isClickableBySettings = IsStrongboxClickableBySettings(itemPathRaw, chestLocked);
                if (!isClickableBySettings && !_settings.ShowStrongboxFrames.Value)
                    continue;

                var color = chestLocked ? Color.Red : Color.LawnGreen;
                _deferredFrameQueue.Enqueue(rect, color, 2);
            }
        }

        // Internal helper used by tests to inspect enqueued frames
        internal (SharpDX.RectangleF Rectangle, Color Color, int Thickness)[] GetEnqueuedFramesForTests()
        {
            return _deferredFrameQueue.GetSnapshotForTests();
        }

        private bool IsStrongboxClickableBySettings(string path, bool chestLocked)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // Map the path keys used by LabelFilterService to the settings toggles.
            var checks = new (bool enabled, string key)[]
            {
                (_settings.RegularStrongbox.Value, "StrongBoxes/Strongbox"),
                (_settings.ArcanistStrongbox.Value, "StrongBoxes/Arcanist"),
                (_settings.ArmourerStrongbox.Value, "StrongBoxes/Armory"),
                (_settings.ArtisanStrongbox.Value, "StrongBoxes/Artisan"),
                (_settings.BlacksmithStrongbox.Value, "StrongBoxes/Arsenal"),
                (_settings.CartographerStrongbox.Value, "StrongBoxes/CartographerEndMaps"),
                (_settings.DivinerStrongbox.Value, "StrongBoxes/StrongboxDivination"),
                (_settings.GemcutterStrongbox.Value, "StrongBoxes/Gemcutter"),
                (_settings.JewellerStrongbox.Value, "StrongBoxes/Jeweller"),
                (_settings.LargeStrongbox.Value, "StrongBoxes/Large"),
                (_settings.OrnateStrongbox.Value, "StrongBoxes/Ornate")
            };

            foreach (var (on, key) in checks)
            {
                if (!on) continue;
                if (path.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

    }
}
