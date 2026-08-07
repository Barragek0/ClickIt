namespace ClickIt.Features.Labels
{
    /// <summary>
    /// Owns the Lazy Mode status text overlay. Pure per-frame: reads input/settings state and
    /// enqueues the status lines (no coroutine refresh — the decision inputs are already cached).
    /// </summary>
    public sealed class LazyModeOverlay : IOverlay
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

        private readonly InputHandler _inputHandler;
        private readonly LazyModeBlockerService? _lazyModeBlockerService;
        private Keys _cachedClickLabelKey = (Keys)(-1);
        private string _cachedHoldClickLabelHint = string.Empty;
        private Keys _cachedLazyModeDisableKey = (Keys)(-1);
        private string _cachedToggleDisableHint = string.Empty;

        public LazyModeOverlay(InputHandler inputHandler, LazyModeBlockerService? lazyModeBlockerService)
        {
            _inputHandler = inputHandler;
            _lazyModeBlockerService = lazyModeBlockerService;
        }

        public string Name => "LazyMode";

        public RenderSection Section => RenderSection.LazyMode;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.None;

        public TimingChannel? RefreshTimingChannel => null;

        public ProcessingSection ProcessingSection => ProcessingSection.Label;

        public bool IsEnabled(ClickItSettings settings)
            => settings.LazyMode.Value;

        public void Refresh(OverlayRefreshContext ctx)
        {
        }

        public void Draw(OverlayRenderContext ctx)
        {
            RectangleF windowRect = ctx.WindowArea;
            float centerX = windowRect.X + (windowRect.Width / 2f);
            float topY = LazyModeTitleY;

            bool hasRestrictedItems = _lazyModeBlockerService?.HasRestrictedItemsOnScreen(ctx.Labels) ?? false;
            string restrictionReason = GetLazyModeRestrictionDisplayReason(_lazyModeBlockerService?.LastRestrictionReason);

            (bool leftClickBlocks, bool rightClickBlocks, bool mouseButtonBlocks) =
                InputHandler.GetMouseButtonBlockingState(ctx.Settings, Input.GetKeyState);

            Keys clickLabelKey = ctx.Settings.ClickLabelKeyBinding;
            bool hotkeyHeld = Input.GetKeyState(clickLabelKey);
            bool lazyModeDisableHeld = _inputHandler.IsLazyModeDisableActiveForCurrentInputState();
            bool lazyModeDisableToggleMode = ctx.Settings.IsLazyModeDisableHotkeyToggleModeEnabled();
            bool isRitualActive = EntityHelpers.IsRitualActive(ctx.GameController);
            bool canActuallyClick = _inputHandler?.CanClick(ctx.GameController, false, isRitualActive) ?? false;

            (Color textColor, string? line1, string? line2, string? line3) = ComposeLazyModeStatus(
                hasRestrictedItems,
                restrictionReason,
                hotkeyHeld,
                lazyModeDisableHeld,
                lazyModeDisableToggleMode,
                mouseButtonBlocks,
                leftClickBlocks,
                rightClickBlocks,
                ctx.GameController,
                clickLabelKey,
                ctx.Settings.LazyModeDisableKeyBinding,
                isRitualActive,
                canActuallyClick);

            RenderLazyModeText(ctx.TextQueue, centerX, topY, textColor, line1, line2, line3);
        }

        private (Color color, string line1, string line2, string line3) ComposeLazyModeStatus(
            bool hasRestrictedItems,
            string restrictionReason,
            bool hotkeyHeld,
            bool lazyModeDisableHeld,
            bool lazyModeDisableToggleMode,
            bool mouseButtonBlocks,
            bool leftClickBlocks,
            bool rightClickBlocks,
            GameController? gameController,
            Keys clickLabelKey,
            Keys lazyModeDisableKeyBinding,
            bool isRitualActive,
            bool canActuallyClick)
        {
            if (hasRestrictedItems)
            {
                return hotkeyHeld
                    ? BuildBlockedOverrideStatus()
                    : (Color.Red, restrictionReason, GetHoldClickLabelHint(clickLabelKey), string.Empty);
            }

            if (lazyModeDisableHeld)
            {
                string resumeHint = lazyModeDisableToggleMode
                    ? GetToggleDisableHint(lazyModeDisableKeyBinding)
                    : ReleaseToResumeLazyClickingText;

                return (Color.Red, LazyModeDisabledByHotkeyText, resumeHint, string.Empty);
            }

            if (mouseButtonBlocks)
            {
                return (Color.Red, $"{GetBlockingMouseButtonName(leftClickBlocks, rightClickBlocks)} held.", "Release to resume lazy clicking.", string.Empty);
            }

            if (isRitualActive)
            {
                return hotkeyHeld
                    ? BuildBlockedOverrideStatus()
                    : (Color.Red, RitualInProgressText, CompleteRitualToResumeText, string.Empty);
            }

            if (!canActuallyClick)
            {
                return (Color.Red, _inputHandler?.GetCanClickFailureReason(gameController) ?? "Clicking disabled.", string.Empty, string.Empty);
            }

            return (Color.LawnGreen, string.Empty, string.Empty, string.Empty);
        }

        private static (Color color, string line1, string line2, string line3) BuildBlockedOverrideStatus()
        {
            return (Color.LawnGreen, BlockingOverriddenByHotkeyText, string.Empty, string.Empty);
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

        internal void RenderLazyModeText(DeferredTextQueue textQueue, float centerX, float topY, Color color, string? line1, string? line2, string? line3)
        {
            textQueue.Enqueue(LazyModeTitle, new Vector2(centerX, topY), color, 36, FontAlign.Center);

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
                textQueue.Enqueue(wrappedLines[i], new Vector2(centerX, y), color, BodyFontSize, FontAlign.Center);
            }
        }
    }
}
