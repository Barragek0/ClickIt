using ExileCore;
using ExileCore.PoEMemory;
using SharpDX;
using System.Diagnostics;
using System.Threading;

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
                RestoreCursorIfLazyMode(before);
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

            RestoreCursorIfLazyMode(before);
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

        // Moves cursor (if enabled), waits for UI update, then returns UI hover element.
        public Element? HoverAndGetUIHover(Vector2 screenPoint, GameController? gameController, int delayMs = -1)
        {
            if (gameController == null)
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

                _lastClickTimestampMs = now;
            }

            return true;
        }

        private void RestoreCursorIfLazyMode(System.Drawing.Point before)
        {
            if (_settings?.LazyMode?.Value == true && _settings.RestoreCursorInLazyMode?.Value == true)
            {
                try
                {
                    Vector2 beforeVec = new(before.X, before.Y);
                    if (!Mouse.DisableNativeInput)
                    {
                        Input.SetCursorPos(beforeVec);
                    }

                    Thread.Sleep(5);
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