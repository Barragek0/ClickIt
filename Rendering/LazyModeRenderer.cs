using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;

namespace ClickIt.Rendering
{
    public partial class LazyModeRenderer(ClickItSettings settings, DeferredTextQueue deferredTextQueue, Utils.InputHandler inputHandler, LabelFilterService? labelFilterService)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly DeferredTextQueue _deferredTextQueue = deferredTextQueue ?? new DeferredTextQueue();
        private readonly Utils.InputHandler _inputHandler = inputHandler;
        private readonly LabelFilterService? _labelFilterService = labelFilterService;

        public void Render(GameController gameController, PluginContext state)
        {
            if (!_settings.LazyMode.Value) return;
            var windowRect = gameController.Window.GetWindowRectangleTimeCache;
            float centerX = windowRect.Width / 2f;
            float topY = 60f;

            var allLabels = state.CachedLabels?.Value;
            bool hasRestrictedItems = _labelFilterService?.HasLazyModeRestrictedItemsOnScreen(allLabels) ?? false;

            bool leftButtonHeld = Input.GetKeyState(Keys.LButton);
            bool rightButtonHeld = Input.GetKeyState(Keys.RButton);
            bool leftClickBlocks = _settings.DisableLazyModeLeftClickHeld.Value && leftButtonHeld;
            bool rightClickBlocks = _settings.DisableLazyModeRightClickHeld.Value && rightButtonHeld;
            bool mouseButtonBlocks = leftClickBlocks || rightClickBlocks;

            var clickLabelKey = _settings.ClickLabelKey.Value;
            var lazyModeDisableKey = _settings.LazyModeDisableKey.Value;
            bool hotkeyHeld = Input.GetKeyState(clickLabelKey);
            bool lazyModeDisableHeld = Input.GetKeyState(lazyModeDisableKey);

            var (textColor, line1, line2, line3) = ComposeLazyModeStatus(
                hasRestrictedItems,
                hotkeyHeld,
                lazyModeDisableHeld,
                mouseButtonBlocks,
                leftClickBlocks,
                rightClickBlocks,
                gameController,
                clickLabelKey);

            RenderLazyModeText(centerX, topY, textColor, line1, line2, line3);
        }

        private (SharpDX.Color color, string line1, string line2, string line3) ComposeLazyModeStatus(
            bool hasRestrictedItems,
            bool hotkeyHeld,
            bool lazyModeDisableHeld,
            bool mouseButtonBlocks,
            bool leftClickBlocks,
            bool rightClickBlocks,
            GameController gameController,
            Keys clickLabelKey)
        {
            _ = (SharpDX.Color.White, string.Empty, string.Empty, string.Empty);

            if (hasRestrictedItems)
            {
                return hotkeyHeld
                    ? (SharpDX.Color.LawnGreen, "Blocking overridden by hotkey.", string.Empty, string.Empty)
                    : (SharpDX.Color.Red, "Locked chest or tree detected.", $"Hold {clickLabelKey} to click them.", string.Empty);
            }

            if (lazyModeDisableHeld)
            {
                return (SharpDX.Color.Red, "Lazy mode disabled by hotkey.", "Release to resume lazy clicking.", string.Empty);
            }

            if (mouseButtonBlocks)
            {
                string buttonName = leftClickBlocks && rightClickBlocks
                    ? "both mouse buttons"
                    : leftClickBlocks ? "Left mouse button" : "Right mouse button";

                return (SharpDX.Color.Red, $"{buttonName} held.", "Release to resume lazy clicking.", string.Empty);
            }

            bool isRitualActive = EntityHelpers.IsRitualActive(gameController);

            if (isRitualActive)
            {
                return hotkeyHeld
                    ? (SharpDX.Color.LawnGreen, "Blocking overridden by hotkey.", string.Empty, string.Empty)
                    : (SharpDX.Color.Red, "Ritual in progress.", "Complete it to resume lazy clicking.", string.Empty);
            }

            bool canActuallyClick = _inputHandler?.CanClick(gameController, false, isRitualActive) ?? false;
            if (!canActuallyClick)
            {
                return (SharpDX.Color.Red, _inputHandler?.GetCanClickFailureReason(gameController) ?? "Clicking disabled.", string.Empty, string.Empty);
            }

            return (SharpDX.Color.LawnGreen, string.Empty, string.Empty, string.Empty);
        }

        private void RenderLazyModeText(float centerX, float topY, SharpDX.Color color, string line1, string line2, string line3)
        {
            const string LAZY_MODE_TEXT = "Lazy Mode";
            _deferredTextQueue.Enqueue(LAZY_MODE_TEXT, new SharpDX.Vector2(centerX, topY), color, 36, ExileCore.Shared.Enums.FontAlign.Center);

            if (string.IsNullOrEmpty(line1)) return;

            float lineHeight = 36 * 1.2f;
            float secondLineY = topY + lineHeight;
            _deferredTextQueue.Enqueue(line1, new SharpDX.Vector2(centerX, secondLineY), color, 24, ExileCore.Shared.Enums.FontAlign.Center);

            if (string.IsNullOrEmpty(line2)) return;

            float thirdLineY = secondLineY + lineHeight;
            _deferredTextQueue.Enqueue(line2, new SharpDX.Vector2(centerX, thirdLineY), color, 24, ExileCore.Shared.Enums.FontAlign.Center);

            if (string.IsNullOrEmpty(line3)) return;

            float fourthLineY = thirdLineY + lineHeight;
            _deferredTextQueue.Enqueue(line3, new SharpDX.Vector2(centerX, fourthLineY), color, 24, ExileCore.Shared.Enums.FontAlign.Center);
        }
    }
}
