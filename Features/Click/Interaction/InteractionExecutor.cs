namespace ClickIt.Features.Click.Interaction
{
    internal sealed class InteractionExecutor(
        ClickItSettings settings,
        PerformanceMonitor performanceMonitor,
        Func<bool> isClickHotkeyActive,
        ErrorHandler? errorHandler = null)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor;
        private readonly Func<bool> _isClickHotkeyActive = isClickHotkeyActive;
        private readonly ErrorHandler? _errorHandler = errorHandler;
        private long _lastClickTimestampMs;
        private long _successfulClickSequence;
        // Safety-sleep reading taken when a click starts, so the recorded click cost excludes the deliberate hover/post-click settles that happened inside it. Click thread only.
        private double _clickSleepStartMs;

        // Returns true only when the click was actually sent to the OS. Internal rejections (lazy limiter, hotkey inactive, UIHover mismatch, invalid point) return false so callers do not run the success aftermath (path/sticky clearing, pending-chest arming, lever cooldowns).
        internal bool PerformClick(
            Vector2 position,
            Element? expectedElement = null,
            GameController? gameController = null,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false,
            IntervalKind interval = IntervalKind.Click)
        {
            _clickSleepStartMs = ClickPipelineTiming.ReadSleepTimeMs();
            if (!TryPrepareClickExecution(
                position,
                expectedElement,
                gameController,
                forceUiHoverVerification,
                allowWhenHotkeyInactive,
                avoidCursorMove,
                "click",
                "InteractionExecutor: UIHover verification failed for current mode. Skipping click.",
                logExpectedElementMissing: true,
                out Stopwatch swTotal,
                out SystemDrawingPoint before))
                return false;


            if (_settings?.LeftHanded?.Value == true)
                Mouse.RightClick();
            else
                Mouse.LeftClick();

            _performanceMonitor.MarkInterval(interval);
            _performanceMonitor.RecordClickDispatch();
            ClickPipelineTiming.Sleep(10);
            RestoreCursorIfLazyMode(before, gameController);
            MarkLazyModeClickCompleted();
            Interlocked.Increment(ref _successfulClickSequence);
            // Exclude the safety sleeps (hover settle + post-click settle) so the recorded click cost is the true processing time, not the deliberate wait time.
            double trueMs = swTotal.ElapsedMilliseconds - (ClickPipelineTiming.ReadSleepTimeMs() - _clickSleepStartMs);
            _performanceMonitor.RecordSuccessfulClickTiming((long)SystemMath.Max(0, trueMs));
            swTotal.Stop();
            return true;
        }

        internal bool PerformClickAndHold(
            Vector2 position,
            Element? expectedElement = null,
            GameController? gameController = null,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            _clickSleepStartMs = ClickPipelineTiming.ReadSleepTimeMs();
            if (!TryConsumeLazyModeLimiter())
                return false;

            Keys clickKey = _settings.ClickLabelKeyBinding;
            if (clickKey == Keys.None)
                return false;
            if (!TryPrepareClickExecution(
                position,
                expectedElement,
                gameController,
                forceUiHoverVerification,
                allowWhenHotkeyInactive,
                avoidCursorMove,
                "hold click",
                "InteractionExecutor: UIHover verification failed for hold-click. Skipping.",
                logExpectedElementMissing: false,
                out Stopwatch swTotal,
                out SystemDrawingPoint before))
                return false;


            try
            {
                if (_settings?.LeftHanded?.Value == true)
                {
                    Mouse.RightMouseDown();
                    while (Keyboard.IsKeyDown(clickKey))
                        ClickPipelineTiming.Sleep(10);
                    Mouse.RightMouseUp();
                }
                else
                {
                    Mouse.LeftMouseDown();
                    while (Keyboard.IsKeyDown(clickKey))
                        ClickPipelineTiming.Sleep(10);
                    Mouse.LeftMouseUp();
                }
            }
            finally
            {
                RestoreCursorIfLazyMode(before, gameController);
            }

            _performanceMonitor.MarkInterval(IntervalKind.Click);
            _performanceMonitor.RecordClickDispatch();
            MarkLazyModeClickCompleted();
            Interlocked.Increment(ref _successfulClickSequence);
            double trueMs = swTotal.ElapsedMilliseconds - (ClickPipelineTiming.ReadSleepTimeMs() - _clickSleepStartMs);
            _performanceMonitor.RecordSuccessfulClickTiming((long)SystemMath.Max(0, trueMs));
            swTotal.Stop();
            return true;
        }

        internal static bool ShouldSkipClickWhenNotLazyAndHotkeyInactive(bool lazyModeEnabled, bool clickHotkeyActive, bool allowWhenHotkeyInactive = false)
        {
            if (allowWhenHotkeyInactive)
                return false;

            return !lazyModeEnabled && !clickHotkeyActive;
        }

        internal static Vector2 ResolveClickExecutionPosition(Vector2 requestedPosition, bool avoidCursorMove)
        {
            if (!avoidCursorMove)
                return requestedPosition;

            SystemDrawingPoint cursor = Mouse.GetCursorPosition();
            return new Vector2(cursor.X, cursor.Y);
        }

        internal static bool ShouldSkipClickDueToHoverMismatch(
            bool lazyModeEnabled,
            bool verifyUiHoverWhenNotLazy,
            ulong expectedAddress,
            ulong hoverAddress,
            bool forceUiHoverVerification = false)
        {
            bool strictHoverVerification = forceUiHoverVerification || lazyModeEnabled || verifyUiHoverWhenNotLazy;
            if (!strictHoverVerification || expectedAddress == 0)
                return false;

            return hoverAddress == 0 || hoverAddress != expectedAddress;
        }

        private bool TryPrepareClickExecution(
            Vector2 position,
            Element? expectedElement,
            GameController? gameController,
            bool forceUiHoverVerification,
            bool allowWhenHotkeyInactive,
            bool avoidCursorMove,
            string clickKind,
            string hoverMismatchMessage,
            bool logExpectedElementMissing,
            out Stopwatch swTotal,
            out SystemDrawingPoint before)
        {
            swTotal = Stopwatch.StartNew();
            before = Mouse.GetCursorPosition();

            if (!TryConsumeLazyModeLimiter())
            {
                swTotal.Stop();
                return false;
            }

            if (ShouldSkipClickWhenNotLazyAndHotkeyInactive(
                _settings.LazyMode.Value,
                _isClickHotkeyActive(),
                allowWhenHotkeyInactive))
            {
                swTotal.Stop();
                return false;
            }

            Vector2 executionPosition = ResolveClickExecutionPosition(position, avoidCursorMove);
            if (!LabelClickPointSearch.TryValidateAutomationScreenPoint(executionPosition, gameController, out string invalidPointReason))
            {
                _errorHandler?.LogMessage(true, true, $"InteractionExecutor: Skipping {clickKind} at {executionPosition} ({invalidPointReason}).", 10);
                swTotal.Stop();
                return false;
            }

            if (!avoidCursorMove && !Mouse.DisableNativeInput)
                Input.SetCursorPos(new NumVector2(executionPosition.X, executionPosition.Y));

            ClickPipelineTiming.Sleep(_settings?.LazyMode?.Value == true ? _settings.LazyModeUIHoverSleep.Value : 10);

            Element? uiHover = gameController?.IngameState?.UIHoverElement;
            bool lazyModeEnabled = _settings?.LazyMode?.Value == true;
            bool verifyUiHoverWhenNotLazy = _settings?.VerifyUIHoverWhenNotLazy?.Value != false;
            ulong expectedAddress = unchecked((ulong)(expectedElement?.Address ?? 0));
            ulong hoverAddress = unchecked((ulong)(uiHover?.Address ?? 0));

            if (ShouldSkipClickDueToHoverMismatch(lazyModeEnabled, verifyUiHoverWhenNotLazy, expectedAddress, hoverAddress, forceUiHoverVerification))
            {
                _errorHandler?.LogMessage(true, true, hoverMismatchMessage, 5);
                RestoreCursorIfLazyMode(before, gameController);
                swTotal.Stop();
                return false;
            }

            if (logExpectedElementMissing && expectedAddress == 0)
                _errorHandler?.LogMessage(true, true, "InteractionExecutor: UIHover verification skipped - expectedElement is null", 5);

            return true;
        }

        private bool TryConsumeLazyModeLimiter()
        {
            if (_settings?.LazyMode?.Value == true)
            {
                int limiterMs = _settings?.LazyModeClickLimiting?.Value ?? 250;
                long now = Environment.TickCount64;
                long elapsed = now - _lastClickTimestampMs;
                if (_lastClickTimestampMs != 0 && elapsed < limiterMs)
                {
                    _errorHandler?.LogMessage(true, true, $"InteractionExecutor: Skipping click due to LazyMode limiter ({elapsed}ms < {limiterMs}ms)", 5);
                    return false;
                }
            }

            return true;
        }

        private void MarkLazyModeClickCompleted()
        {
            if (_settings?.LazyMode?.Value == true)
                _lastClickTimestampMs = Environment.TickCount64;
        }

        private void RestoreCursorIfLazyMode(SystemDrawingPoint before, GameController? gameController)
        {
            if (_settings?.LazyMode?.Value == true && _settings.RestoreCursorInLazyMode?.Value == true)
                try
                {
                    int restoreDelayMs = _settings?.LazyModeRestoreCursorDelayMs?.Value ?? 10;
                    ClickPipelineTiming.Sleep(restoreDelayMs);

                    Vector2 beforeVec = new(before.X, before.Y);
                    if (!LabelClickPointSearch.TryValidateAutomationScreenPoint(beforeVec, gameController, out _))
                        return;

                    if (!Mouse.DisableNativeInput)
                        Input.SetCursorPos(new NumVector2(beforeVec.X, beforeVec.Y));

                    _errorHandler?.LogMessage(true, true, $"InteractionExecutor: Restored cursor to {before}", 5);
                }
                catch (Exception ex)
                {
                    _errorHandler?.LogMessage(true, true, $"InteractionExecutor: Failed to restore cursor position: {ex.Message}", 10);
                }

        }
    }
}
