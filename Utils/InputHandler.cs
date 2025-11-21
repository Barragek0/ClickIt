using ClickIt.Utils;
using ExileCore;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using System;
using System.Threading;
#nullable enable
namespace ClickIt.Utils
{
    public class InputHandler
    {
        private readonly ClickItSettings _settings;
        private readonly Random _random;
        private readonly Action<bool>? _safeBlockInput;
        private readonly Utils.ErrorHandler? _errorHandler;
        // Timestamp in milliseconds of the last performed click. Used for Lazy Mode limiting.
        private long _lastClickTimestampMs = 0;

        public InputHandler(ClickItSettings settings, Action<bool>? safeBlockInput = null, Utils.ErrorHandler? errorHandler = null)
        {
            _settings = settings;
            _random = new Random();
            _safeBlockInput = safeBlockInput;
            _errorHandler = errorHandler;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private static string GetActiveWindowTitle()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return string.Empty;
            int len = GetWindowTextLength(hwnd);
            if (len <= 0) return string.Empty;
            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
        public Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            Vector2 offset = new(_random.Next(0, 5),
                label.ItemOnGround.Type == EntityType.Chest ?
                -_random.Next(_settings.ChestHeightOffset, _settings.ChestHeightOffset + 2) :
                _random.Next(0, 5));
            // Use null-conditional operator for safe memory access
            if (label.Label?.GetClientRect() is not RectangleF rect)
            {
                throw new InvalidOperationException("Label element is invalid");
            }
            return rect.Center + windowTopLeft + offset;
        }
        public bool TriggerToggleItems()
        {
            if (_settings.ToggleItems.Value && _random.Next(0, 20) == 0)
            {
#pragma warning disable CS0618
                Keyboard.KeyPress(_settings.ToggleItemsHotkey, 20);
                Keyboard.KeyPress(_settings.ToggleItemsHotkey, 20);
                return true;
#pragma warning restore CS0618
            }
            return false;
        }
        public bool CanClick(GameController gameController)
        {
            if (gameController == null) return false;
            bool keyState = Input.GetKeyState(_settings.ClickLabelKey.Value);
            if (_settings?.LazyMode != null && _settings.LazyMode.Value)
            {
                keyState = !keyState;
            }
            return keyState &&
                IsPOEActive(gameController) &&
                (!_settings.BlockOnOpenLeftRightPanel!.Value || !IsPanelOpen(gameController)) &&
                !IsInTownOrHideout(gameController) &&
                !gameController.IngameState.IngameUi.ChatTitlePanel.IsVisible;
        }
        private static bool IsPOEActive(GameController gameController)
        {
            return gameController.Window.IsForeground();
        }
        private static bool IsPanelOpen(GameController gameController)
        {
            if (gameController == null) return false;
            var ui = gameController.IngameState?.IngameUi;
            if (ui == null) return false;
            return ui.OpenLeftPanel.Address != 0 || ui.OpenRightPanel.Address != 0;
        }
        private static bool IsInTownOrHideout(GameController gameController)
        {
            if (gameController == null) return false;
            var area = gameController.Area?.CurrentArea;
            if (area == null) return false;
            return area.IsHideout || area.IsTown;
        }

        public void PerformClick(Vector2 position)
        {
            if (!TryConsumeLazyModeLimiter())
                return;

            _errorHandler?.LogMessage(true, true, "InputHandler: PerformClick - entering", 5);

            if ((_settings.BlockUserInput?.Value ?? false) && _safeBlockInput != null)
            {
                _errorHandler?.LogMessage(true, true, "InputHandler: Requesting system input block", 5);
                _safeBlockInput(true);
                _errorHandler?.LogMessage(true, true, "InputHandler: System input block requested", 5);
            }

            _errorHandler?.LogMessage(true, true, $"InputHandler: Setting cursor pos to {position}", 5);
            var swTotal = Stopwatch.StartNew();
            var before = Mouse.GetCursorPosition();
            _errorHandler?.LogMessage(true, true, $"InputHandler: Cursor before move: {before}", 5);

            var sw = Stopwatch.StartNew();
            Input.SetCursorPos(position);
            sw.Stop();
            _errorHandler?.LogMessage(true, true, $"InputHandler: Cursor position set (SetCursorPos took {sw.ElapsedMilliseconds} ms)", 5);

            var after = Mouse.GetCursorPosition();
            _errorHandler?.LogMessage(true, true, $"InputHandler: Cursor after move: {after}", 5);

            // Small delay to ensure cursor movement has taken effect before click
            Thread.Sleep(15);

            sw.Restart();
            if (_settings.LeftHanded.Value)
            {
                _errorHandler?.LogMessage(true, true, "InputHandler: Performing right click (left-handed)", 5);
                Mouse.RightClick();
            }
            else
            {
                _errorHandler?.LogMessage(true, true, "InputHandler: Performing left click", 5);
                Mouse.LeftClick();
            }
            sw.Stop();
            _errorHandler?.LogMessage(true, true, $"InputHandler: Click performed (took {sw.ElapsedMilliseconds} ms)", 5);

            RestoreCursorIfLazyMode(before);

            swTotal.Stop();
            // If the whole operation took too long, attempt to clear any stuck input block and log
            if (swTotal.ElapsedMilliseconds > 500)
            {
                _errorHandler?.LogMessage(true, true, $"InputHandler: WARNING - PerformClick took {swTotal.ElapsedMilliseconds} ms, attempting to release input block", 10);
                if ((_settings.BlockUserInput?.Value ?? false) && _safeBlockInput != null)
                {
                    _safeBlockInput(false);
                    _errorHandler?.LogMessage(true, true, "InputHandler: Watchdog released system input block", 10);
                }
            }

            if ((_settings.BlockUserInput?.Value ?? false) && _safeBlockInput != null)
            {
                _errorHandler?.LogMessage(true, true, "InputHandler: Releasing system input block", 5);
                _safeBlockInput(false);
                _errorHandler?.LogMessage(true, true, "InputHandler: System input block released", 5);
            }
            _errorHandler?.LogMessage(true, true, "InputHandler: PerformClick - exiting", 5);
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
            if (_settings?.LazyMode != null && _settings.LazyMode.Value)
            {
                try
                {
                    var beforeVec = new Vector2(before.X, before.Y);
                    Input.SetCursorPos(beforeVec);
                    // Small delay to let the OS update cursor position
                    Thread.Sleep(8);
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
