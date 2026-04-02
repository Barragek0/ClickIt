using ClickIt.Services;
using ClickIt.Rendering.Debug.Layout;
using ClickIt.Utils;
using ExileCore;

namespace ClickIt.Rendering
{
    public class LazyModeRenderer(ClickItSettings settings, DeferredTextQueue deferredTextQueue, InputHandler inputHandler, LabelFilterService? labelFilterService)
    {
        private const string LazyModeTitle = "Lazy Mode";
        private const string GenericRestrictionDetectedText = "Lazy mode blocking condition detected.";
        private const string LazyModeDisabledByHotkeyText = "Lazy mode disabled by hotkey.";
        private const string ReleaseToResumeLazyClickingText = "Release to resume lazy clicking.";
        private const string RitualInProgressText = "Ritual in progress.";
        private const string CompleteRitualToResumeText = "Complete it to resume lazy clicking.";
        private const string BlockingOverriddenByHotkeyText = "Blocking overridden by hotkey.";
        private const int OverlayLineLengthLimit = 48;
        private const float LazyModeTitleY = 60f;
        private const float LazyModeLineHeightMultiplier = 1.2f;
        private const int BodyFontSize = 24;

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
            string restrictionReason = GetLazyModeRestrictionDisplayReason(_labelFilterService?.LastLazyModeRestrictionReason);

            var (leftClickBlocks, rightClickBlocks, mouseButtonBlocks) =
                InputHandler.GetMouseButtonBlockingState(_settings, Input.GetKeyState);

            var clickLabelKey = _settings.ClickLabelKey.Value;
            bool hotkeyHeld = Input.GetKeyState(clickLabelKey);
            bool lazyModeDisableHeld = _inputHandler.IsLazyModeDisableActiveForCurrentInputState();
            bool lazyModeDisableToggleMode = _settings.IsLazyModeDisableHotkeyToggleModeEnabled();
            bool isRitualActive = EntityHelpers.IsRitualActive(gameController);
            bool canActuallyClick = _inputHandler?.CanClick(gameController, false, isRitualActive) ?? false;

            var (textColor, line1, line2, line3) = ComposeLazyModeStatus(
                hasRestrictedItems,
                restrictionReason,
                hotkeyHeld,
                lazyModeDisableHeld,
                lazyModeDisableToggleMode,
                mouseButtonBlocks,
                leftClickBlocks,
                rightClickBlocks,
                gameController,
                clickLabelKey,
                isRitualActive,
                canActuallyClick);

            RenderLazyModeText(centerX, topY, textColor, line1, line2, line3);
        }

        private (SharpDX.Color color, string line1, string line2, string line3) ComposeLazyModeStatus(
            bool hasRestrictedItems,
            string restrictionReason,
            bool hotkeyHeld,
            bool lazyModeDisableHeld,
            bool lazyModeDisableToggleMode,
            bool mouseButtonBlocks,
            bool leftClickBlocks,
            bool rightClickBlocks,
            GameController gameController,
            Keys clickLabelKey,
            bool isRitualActive,
            bool canActuallyClick)
        {
            if (hasRestrictedItems)
            {
                return hotkeyHeld
                    ? BuildBlockedOverrideStatus()
                    : (SharpDX.Color.Red, restrictionReason, GetHoldClickLabelHint(clickLabelKey), string.Empty);
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

            if (isRitualActive)
            {
                return hotkeyHeld
                    ? BuildBlockedOverrideStatus()
                    : (SharpDX.Color.Red, RitualInProgressText, CompleteRitualToResumeText, string.Empty);
            }

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

        internal static string GetLazyModeRestrictionDisplayReason(string? rawReason)
        {
            return string.IsNullOrWhiteSpace(rawReason)
                ? GenericRestrictionDetectedText
                : rawReason.Trim();
        }

        internal static List<string> WrapOverlayText(string? text, int maxLength)
        {
            return DebugTextLayoutEngine.WrapOverlayText(text, maxLength);
        }

        private string GetHoldClickLabelHint(Keys clickLabelKey)
        {
            if (_cachedClickLabelKey != clickLabelKey || string.IsNullOrEmpty(_cachedHoldClickLabelHint))
            {
                _cachedClickLabelKey = clickLabelKey;
                _cachedHoldClickLabelHint = $"Hold {clickLabelKey} to override.";
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

        internal static string GetBlockingMouseButtonName(bool leftClickBlocks, bool rightClickBlocks)
        {
            if (leftClickBlocks && rightClickBlocks)
                return "both mouse buttons";

            return leftClickBlocks ? "Left mouse button" : "Right mouse button";
        }

        private void RenderLazyModeText(float centerX, float topY, SharpDX.Color color, string line1, string line2, string line3)
        {
            _deferredTextQueue.Enqueue(LazyModeTitle, new SharpDX.Vector2(centerX, topY), color, 36, ExileCore.Shared.Enums.FontAlign.Center);

            List<string> wrappedLines = [];
            wrappedLines.AddRange(WrapOverlayText(line1, OverlayLineLengthLimit));
            wrappedLines.AddRange(WrapOverlayText(line2, OverlayLineLengthLimit));
            wrappedLines.AddRange(WrapOverlayText(line3, OverlayLineLengthLimit));

            if (wrappedLines.Count == 0)
                return;

            float lineHeight = BodyFontSize * LazyModeLineHeightMultiplier;
            for (int i = 0; i < wrappedLines.Count; i++)
            {
                float y = topY + ((i + 1) * lineHeight);
                _deferredTextQueue.Enqueue(wrappedLines[i], new SharpDX.Vector2(centerX, y), color, BodyFontSize, ExileCore.Shared.Enums.FontAlign.Center);
            }
        }
    }
}
