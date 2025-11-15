using ClickIt.Utils;
using ExileCore;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using SharpDX;
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
        private readonly Action<string, int>? _logMessage;

        public InputHandler(ClickItSettings settings, Action<bool>? safeBlockInput = null, Action<string, int>? logMessage = null)
        {
            _settings = settings;
            _random = new Random();
            _safeBlockInput = safeBlockInput;
            _logMessage = logMessage;
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
#pragma warning disable CS0618
            return Input.GetKeyState(_settings.ClickLabelKey.Value) &&
#pragma warning restore CS0618
                   IsPOEActive(gameController) &&
                   (!_settings.BlockOnOpenLeftRightPanel || !IsPanelOpen(gameController)) &&
                   !IsInTownOrHideout(gameController) &&
                   !gameController.IngameState.IngameUi.ChatTitlePanel.IsVisible;
        }
        private static bool IsPOEActive(GameController gameController)
        {
            return gameController.Window.IsForeground();
        }
        private static bool IsPanelOpen(GameController gameController)
        {
            return gameController.IngameState.IngameUi.OpenLeftPanel.Address != 0 ||
                   gameController.IngameState.IngameUi.OpenRightPanel.Address != 0;
        }
        private static bool IsInTownOrHideout(GameController gameController)
        {
            return gameController.Area.CurrentArea.IsHideout || gameController.Area.CurrentArea.IsTown;
        }

        public void PerformClick(Vector2 position)
        {
            _logMessage?.Invoke("InputHandler: PerformClick - entering", 5);

            if (_settings.BlockUserInput.Value && _safeBlockInput != null)
            {
                _logMessage?.Invoke("InputHandler: Requesting system input block", 5);
                _safeBlockInput(true);
                _logMessage?.Invoke("InputHandler: System input block requested", 5);
            }

            _logMessage?.Invoke($"InputHandler: Setting cursor pos to {position}", 5);
            var swTotal = Stopwatch.StartNew();
            var before = Mouse.GetCursorPosition();
            _logMessage?.Invoke($"InputHandler: Cursor before move: {before}", 5);

            var sw = Stopwatch.StartNew();
            Input.SetCursorPos(position);
            sw.Stop();
            _logMessage?.Invoke($"InputHandler: Cursor position set (SetCursorPos took {sw.ElapsedMilliseconds} ms)", 5);

            var after = Mouse.GetCursorPosition();
            _logMessage?.Invoke($"InputHandler: Cursor after move: {after}", 5);

            // Small delay to ensure cursor movement has taken effect before click
            Thread.Sleep(15);

            sw.Restart();
            if (_settings.LeftHanded.Value)
            {
                _logMessage?.Invoke("InputHandler: Performing right click (left-handed)", 5);
                Mouse.RightClick();
            }
            else
            {
                _logMessage?.Invoke("InputHandler: Performing left click", 5);
                Mouse.LeftClick();
            }
            sw.Stop();
            _logMessage?.Invoke($"InputHandler: Click performed (took {sw.ElapsedMilliseconds} ms)", 5);

            swTotal.Stop();
            // If the whole operation took too long, attempt to clear any stuck input block and log
            if (swTotal.ElapsedMilliseconds > 500)
            {
                _logMessage?.Invoke($"InputHandler: WARNING - PerformClick took {swTotal.ElapsedMilliseconds} ms, attempting to release input block", 10);
                if (_settings.BlockUserInput.Value && _safeBlockInput != null)
                {
                    _safeBlockInput(false);
                    _logMessage?.Invoke("InputHandler: Watchdog released system input block", 10);
                }
            }

            if (_settings.BlockUserInput.Value && _safeBlockInput != null)
            {
                _logMessage?.Invoke("InputHandler: Releasing system input block", 5);
                _safeBlockInput(false);
                _logMessage?.Invoke("InputHandler: System input block released", 5);
            }
            _logMessage?.Invoke("InputHandler: PerformClick - exiting", 5);
        }
    }
}
