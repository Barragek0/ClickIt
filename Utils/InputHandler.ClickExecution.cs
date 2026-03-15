using ExileCore;
using ExileCore.PoEMemory;
using SharpDX;
using System.Diagnostics;
using System.Threading;
using ClickIt.Definitions;
using ExileCore.Shared.Nodes;

namespace ClickIt.Utils
{
    public partial class InputHandler
    {
        public void PerformClick(Vector2 position, Element? expectedElement = null, GameController? gameController = null)
        {
            if (!TryConsumeLazyModeLimiter())
                return;

            if (!_settings.LazyMode.Value && !Input.GetKeyState(_settings.ClickLabelKey.Value))
                return;

            if (!TryValidateAutomationScreenPoint(position, gameController, out string invalidPointReason))
            {
                _errorHandler?.LogMessage(true, true, $"InputHandler: Skipping click at {position} ({invalidPointReason}).", 10);
                return;
            }

            var swTotal = Stopwatch.StartNew();
            var before = Mouse.GetCursorPosition();

            var sw = Stopwatch.StartNew();
            if (!Mouse.DisableNativeInput)
            {
                Input.SetCursorPos(position);
            }
            sw.Stop();

            if (_settings?.LazyMode?.Value == true)
            {
                Thread.Sleep(_settings.LazyModeUIHoverSleep.Value);
            }
            else
            {
                Thread.Sleep(10);
            }

            var uiHover = gameController?.IngameState?.UIHoverElement;

            bool lazyModeEnabled = _settings?.LazyMode?.Value == true;
            bool verifyUiHoverWhenNotLazy = _settings?.VerifyUIHoverWhenNotLazy?.Value != false;
            ulong expectedAddress = unchecked((ulong)(expectedElement?.Address ?? 0));
            ulong hoverAddress = unchecked((ulong)(uiHover?.Address ?? 0));

            if (ShouldSkipClickDueToHoverMismatch(lazyModeEnabled, verifyUiHoverWhenNotLazy, expectedAddress, hoverAddress))
            {
                _errorHandler?.LogMessage(true, true, "InputHandler: UIHover verification failed for current mode. Skipping click.", 5);
                RestoreCursorIfLazyMode(before, gameController);
                return;
            }

            if (expectedAddress == 0)
            {
                _errorHandler?.LogMessage(true, true, "InputHandler: UIHover verification skipped - expectedElement is null", 5);
            }

            sw.Restart();
            if (_settings?.LeftHanded?.Value == true)
            {
                Mouse.RightClick();
            }
            else
            {
                Mouse.LeftClick();
            }
            sw.Stop();

            Thread.Sleep(10);

            RestoreCursorIfLazyMode(before, gameController);
            MarkLazyModeClickCompleted();
            Interlocked.Increment(ref _successfulClickSequence);
            _performanceMonitor.RecordSuccessfulClickTiming(swTotal.ElapsedMilliseconds);

            swTotal.Stop();
        }

        public void PerformClickAndHold(Vector2 position, int holdDurationMs, Element? expectedElement = null, GameController? gameController = null)
        {
            _ = holdDurationMs;

            if (!TryConsumeLazyModeLimiter())
                return;

            HotkeyNode? clickKeyNode = _settings.ClickLabelKey;
            if (clickKeyNode == null)
                return;

            var clickKey = clickKeyNode.Value;
            if (!_settings.LazyMode.Value && !Input.GetKeyState(clickKey))
                return;

            if (!TryValidateAutomationScreenPoint(position, gameController, out string invalidPointReason))
            {
                _errorHandler?.LogMessage(true, true, $"InputHandler: Skipping hold click at {position} ({invalidPointReason}).", 10);
                return;
            }

            var swTotal = Stopwatch.StartNew();
            var before = Mouse.GetCursorPosition();

            if (!Mouse.DisableNativeInput)
            {
                Input.SetCursorPos(position);
            }

            if (_settings?.LazyMode?.Value == true)
            {
                Thread.Sleep(_settings.LazyModeUIHoverSleep.Value);
            }
            else
            {
                Thread.Sleep(10);
            }

            var uiHover = gameController?.IngameState?.UIHoverElement;
            bool lazyModeEnabled = _settings?.LazyMode?.Value == true;
            bool verifyUiHoverWhenNotLazy = _settings?.VerifyUIHoverWhenNotLazy?.Value != false;
            ulong expectedAddress = unchecked((ulong)(expectedElement?.Address ?? 0));
            ulong hoverAddress = unchecked((ulong)(uiHover?.Address ?? 0));

            if (ShouldSkipClickDueToHoverMismatch(lazyModeEnabled, verifyUiHoverWhenNotLazy, expectedAddress, hoverAddress))
            {
                _errorHandler?.LogMessage(true, true, "InputHandler: UIHover verification failed for hold-click. Skipping.", 5);
                RestoreCursorIfLazyMode(before, gameController);
                return;
            }

            try
            {
                if (_settings?.LeftHanded?.Value == true)
                {
                    Mouse.RightMouseDown();
                    while (Input.GetKeyState(clickKey))
                    {
                        Thread.Sleep(10);
                    }
                    Mouse.RightMouseUp();
                }
                else
                {
                    Mouse.LeftMouseDown();
                    while (Input.GetKeyState(clickKey))
                    {
                        Thread.Sleep(10);
                    }
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

        public static bool ShouldSkipClickDueToHoverMismatch(
            bool lazyModeEnabled,
            bool verifyUiHoverWhenNotLazy,
            ulong expectedAddress,
            ulong hoverAddress)
        {
            bool strictHoverVerification = lazyModeEnabled || verifyUiHoverWhenNotLazy;
            if (!strictHoverVerification)
                return false;

            if (expectedAddress == 0)
                return false;

            return hoverAddress == 0 || hoverAddress != expectedAddress;
        }

        public Element? HoverAndGetUIHover(Vector2 screenPoint, GameController? gameController, int delayMs = -1)
        {
            if (gameController == null)
                return null;

            if (!TryValidateAutomationScreenPoint(screenPoint, gameController, out _))
                return null;

            int sleepMs = delayMs;
            if (sleepMs <= 0)
            {
                sleepMs = _settings?.LazyModeUIHoverSleep?.Value ?? 20;
            }

            try
            {
                if (!Mouse.DisableNativeInput)
                {
                    Input.SetCursorPos(screenPoint);
                }

                Thread.Sleep(sleepMs);
                return gameController?.IngameState?.UIHoverElement;
            }
            catch
            {
                return null;
            }
        }

        private bool TryConsumeLazyModeLimiter()
        {
            if (_settings?.LazyMode != null && _settings.LazyMode.Value)
            {
                int limiterMs = _settings?.LazyModeClickLimiting?.Value ?? 250;
                long now = Environment.TickCount64;
                long elapsed = now - _lastClickTimestampMs;
                if (_lastClickTimestampMs != 0 && elapsed < limiterMs)
                {
                    _errorHandler?.LogMessage(true, true, $"InputHandler: Skipping click due to LazyMode limiter ({elapsed}ms < {limiterMs}ms)", 5);
                    return false;
                }
            }

            return true;
        }

        private void MarkLazyModeClickCompleted()
        {
            if (_settings?.LazyMode?.Value == true)
            {
                _lastClickTimestampMs = Environment.TickCount64;
            }
        }

        private void RestoreCursorIfLazyMode(System.Drawing.Point before, GameController? gameController)
        {
            if (_settings?.LazyMode?.Value == true && _settings.RestoreCursorInLazyMode?.Value == true)
            {
                try
                {
                    int restoreDelayMs = _settings?.LazyModeRestoreCursorDelayMs?.Value ?? 10;
                    Thread.Sleep(restoreDelayMs);

                    Vector2 beforeVec = new(before.X, before.Y);
                    if (!TryValidateAutomationScreenPoint(beforeVec, gameController, out _))
                        return;

                    if (!Mouse.DisableNativeInput)
                    {
                        Input.SetCursorPos(beforeVec);
                    }

                    _errorHandler?.LogMessage(true, true, $"InputHandler: Restored cursor to {before}", 5);
                }
                catch (Exception ex)
                {
                    _errorHandler?.LogMessage(true, true, $"InputHandler: Failed to restore cursor position: {ex.Message}", 10);
                }
            }
        }
    }
}
