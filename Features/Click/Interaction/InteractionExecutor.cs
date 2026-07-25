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

        internal long GetSuccessfulClickSequence()
            => Interlocked.Read(ref _successfulClickSequence);

        internal void PerformClick(
            Vector2 position,
            Element? expectedElement = null,
            GameController? gameController = null,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
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
                return;


            if (_settings?.LeftHanded?.Value == true)
                Mouse.RightClick();
            else
                Mouse.LeftClick();

            Thread.Sleep(10);
            RestoreCursorIfLazyMode(before, gameController);
            MarkLazyModeClickCompleted();
            Interlocked.Increment(ref _successfulClickSequence);
            _performanceMonitor.RecordSuccessfulClickTiming(swTotal.ElapsedMilliseconds);
            swTotal.Stop();
        }

        internal void PerformClickAndHold(
            Vector2 position,
            int holdDurationMs,
            Element? expectedElement = null,
            GameController? gameController = null,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            _ = holdDurationMs;

            if (!TryConsumeLazyModeLimiter())
                return;

            Keys clickKey = _settings.ClickLabelKeyBinding;
            if (clickKey == Keys.None)
                return;
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
                return;


            try
            {
                if (_settings?.LeftHanded?.Value == true)
                {
                    Mouse.RightMouseDown();
                    while (Keyboard.IsKeyDown(clickKey))
                        Thread.Sleep(10);
                    Mouse.RightMouseUp();
                }
                else
                {
                    Mouse.LeftMouseDown();
                    while (Keyboard.IsKeyDown(clickKey))
                        Thread.Sleep(10);
                    Mouse.LeftMouseUp();
                }
            }
            finally
            {
                RestoreCursorIfLazyMode(before, gameController);
            }

            MarkLazyModeClickCompleted();
            Interlocked.Increment(ref _successfulClickSequence);
            _performanceMonitor.RecordSuccessfulClickTiming(swTotal.ElapsedMilliseconds);
            swTotal.Stop();
        }

        internal Element? HoverAndGetUIHover(Vector2 screenPoint, GameController? gameController, int delayMs = -1)
        {
            if (gameController == null)
                return null;

            if (!LabelClickPointSearch.TryValidateAutomationScreenPoint(screenPoint, gameController, out _))
                return null;

            int sleepMs = delayMs > 0 ? delayMs : _settings?.LazyModeUIHoverSleep?.Value ?? 20;

            try
            {
                if (!Mouse.DisableNativeInput)
                    Input.SetCursorPos(new NumVector2(screenPoint.X, screenPoint.Y));

                Thread.Sleep(sleepMs);
                return gameController.IngameState?.UIHoverElement;
            }
            catch
            {
                return null;
            }
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

            Thread.Sleep(_settings?.LazyMode?.Value == true ? _settings.LazyModeUIHoverSleep.Value : 10);

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
                    Thread.Sleep(restoreDelayMs);

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