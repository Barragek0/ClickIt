using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;

namespace ClickIt.Rendering
{
    public class LazyModeRenderer(ClickItSettings settings, DeferredTextQueue deferredTextQueue, InputHandler inputHandler, LabelFilterService? labelFilterService)
    {
        private const string LazyModeTitle = "Lazy Mode";
        private const string LockedChestOrTreeDetectedText = "Locked chest or tree detected.";
        private const string LazyModeDisabledByHotkeyText = "Lazy mode disabled by hotkey.";
        private const string ReleaseToResumeLazyClickingText = "Release to resume lazy clicking.";
        private const string RitualInProgressText = "Ritual in progress.";
        private const string CompleteRitualToResumeText = "Complete it to resume lazy clicking.";
        private const string BlockingOverriddenByHotkeyText = "Blocking overridden by hotkey.";
        private const float LazyModeTitleY = 60f;
        private const float LazyModeLineHeightMultiplier = 1.2f;

        private readonly ClickItSettings _settings = settings;
        private readonly DeferredTextQueue _deferredTextQueue = deferredTextQueue ?? new DeferredTextQueue();
        private readonly InputHandler _inputHandler = inputHandler;
        private readonly LabelFilterService? _labelFilterService = labelFilterService;
        private Keys _cachedClickLabelKey = (Keys)(-1);
        private string _cachedHoldClickLabelHint = string.Empty;
        private Keys _cachedLazyModeDisableKey = (Keys)(-1);
        private string _cachedToggleDisableHint = string.Empty;

        public void Render(GameController gameController, PluginContext state)
        {
            if (!_settings.LazyMode.Value) return;
            var windowRect = gameController.Window.GetWindowRectangleTimeCache;
            float centerX = windowRect.Width / 2f;
            float topY = LazyModeTitleY;

            var allLabels = state.CachedLabels?.Value;
            bool hasRestrictedItems = _labelFilterService?.HasLazyModeRestrictedItemsOnScreen(allLabels) ?? false;

            var (leftClickBlocks, rightClickBlocks, mouseButtonBlocks) =
                InputHandler.GetMouseButtonBlockingState(_settings, Input.GetKeyState);

            var clickLabelKey = _settings.ClickLabelKey.Value;
            bool hotkeyHeld = Input.GetKeyState(clickLabelKey);
            bool lazyModeDisableHeld = _inputHandler.IsLazyModeDisableActiveForCurrentInputState();
            bool lazyModeDisableToggleMode = _settings.IsLazyModeDisableHotkeyToggleModeEnabled();

            var (textColor, line1, line2, line3) = ComposeLazyModeStatus(
                hasRestrictedItems,
                hotkeyHeld,
                lazyModeDisableHeld,
                lazyModeDisableToggleMode,
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
            bool lazyModeDisableToggleMode,
            bool mouseButtonBlocks,
            bool leftClickBlocks,
            bool rightClickBlocks,
            GameController gameController,
            Keys clickLabelKey)
        {
            if (hasRestrictedItems)
            {
                return hotkeyHeld
                    ? BuildBlockedOverrideStatus()
                    : (SharpDX.Color.Red, LockedChestOrTreeDetectedText, GetHoldClickLabelHint(clickLabelKey), string.Empty);
            }

            if (lazyModeDisableHeld)
            {
                string resumeHint = lazyModeDisableToggleMode
                    ? GetToggleDisableHint(_settings.LazyModeDisableKey.Value)
                    : ReleaseToResumeLazyClickingText;

                return (SharpDX.Color.Red, LazyModeDisabledByHotkeyText, resumeHint, string.Empty);
            }

            if (mouseButtonBlocks)
            {
                return (SharpDX.Color.Red, $"{GetBlockingMouseButtonName(leftClickBlocks, rightClickBlocks)} held.", "Release to resume lazy clicking.", string.Empty);
            }

            bool isRitualActive = EntityHelpers.IsRitualActive(gameController);

            if (isRitualActive)
            {
                return hotkeyHeld
                    ? BuildBlockedOverrideStatus()
                    : (SharpDX.Color.Red, RitualInProgressText, CompleteRitualToResumeText, string.Empty);
            }

            bool canActuallyClick = _inputHandler?.CanClick(gameController, false, isRitualActive) ?? false;
            if (!canActuallyClick)
            {
                return (SharpDX.Color.Red, _inputHandler?.GetCanClickFailureReason(gameController) ?? "Clicking disabled.", string.Empty, string.Empty);
            }

            return (SharpDX.Color.LawnGreen, string.Empty, string.Empty, string.Empty);
        }

        private static (SharpDX.Color color, string line1, string line2, string line3) BuildBlockedOverrideStatus()
        {
            return (SharpDX.Color.LawnGreen, BlockingOverriddenByHotkeyText, string.Empty, string.Empty);
        }

        private string GetHoldClickLabelHint(Keys clickLabelKey)
        {
            if (_cachedClickLabelKey != clickLabelKey || string.IsNullOrEmpty(_cachedHoldClickLabelHint))
            {
                _cachedClickLabelKey = clickLabelKey;
                _cachedHoldClickLabelHint = $"Hold {clickLabelKey} to click them.";
            }

            return _cachedHoldClickLabelHint;
        }

        private string GetToggleDisableHint(Keys disableKey)
        {
            if (_cachedLazyModeDisableKey != disableKey || string.IsNullOrEmpty(_cachedToggleDisableHint))
            {
                _cachedLazyModeDisableKey = disableKey;
                _cachedToggleDisableHint = $"Press {disableKey} again to resume lazy clicking.";
            }

            return _cachedToggleDisableHint;
        }

        private static string GetBlockingMouseButtonName(bool leftClickBlocks, bool rightClickBlocks)
        {
            if (leftClickBlocks && rightClickBlocks)
                return "both mouse buttons";

            return leftClickBlocks ? "Left mouse button" : "Right mouse button";
        }

        private void RenderLazyModeText(float centerX, float topY, SharpDX.Color color, string line1, string line2, string line3)
        {
            _deferredTextQueue.Enqueue(LazyModeTitle, new SharpDX.Vector2(centerX, topY), color, 36, ExileCore.Shared.Enums.FontAlign.Center);

            if (string.IsNullOrEmpty(line1)) return;

            float lineHeight = 36 * LazyModeLineHeightMultiplier;
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
