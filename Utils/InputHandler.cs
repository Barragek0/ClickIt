using ExileCore;
using System.Diagnostics;
using ExileCore.Shared.Cache;
using System.Runtime.InteropServices;
using System.Text;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory;
namespace ClickIt.Utils
{
    public class InputHandler(ClickItSettings settings, PerformanceMonitor performanceMonitor, ErrorHandler? errorHandler = null)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Random _random = new Random();
        private readonly ErrorHandler? _errorHandler = errorHandler;
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor;
        // Timestamp in milliseconds of the last performed click. Used for Lazy Mode limiting.
        private long _lastClickTimestampMs = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // helper removed — not used
        public Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            // Use null-conditional operator for safe memory access
            if (label.Label?.GetClientRect() is not RectangleF rect)
            {
                throw new InvalidOperationException("Label element is invalid");
            }

            float jitterRange = 2f;
            float jitterX = (float)(_random.NextDouble() * (jitterRange * 2) - jitterRange);
            float jitterY = (float)(_random.NextDouble() * (jitterRange * 2) - jitterRange);

            if (label.ItemOnGround.Type == EntityType.Chest)
            {
                jitterY -= _settings.ChestHeightOffset;
            }

            return rect.Center + windowTopLeft + new Vector2(jitterX, jitterY);
        }
        public bool TriggerToggleItems()
        {
            if (_settings.ToggleItems.Value && _random.Next(0, 20) == 0)
            {
                Keyboard.KeyPress(_settings.ToggleItemsHotkey, 20);
                Keyboard.KeyPress(_settings.ToggleItemsHotkey, 20);
                return true;
            }
            return false;
        }
        public bool CanClick(GameController gameController, bool hasLazyModeRestrictedItemsOnScreen = false, bool isRitualActive = false)
        {
            if (gameController == null) return false;

            // Lazy mode is active when:
            // - Lazy mode is enabled
            // - No restricted items are on screen  
            // - Lazy mode disable hotkey is NOT being held
            bool lazyModeActive = _settings?.LazyMode != null &&
                                 _settings.LazyMode.Value &&
                                 !hasLazyModeRestrictedItemsOnScreen &&
                                 !Input.GetKeyState(_settings.LazyModeDisableKey.Value);

            // In lazy mode, always allow clicking (ignore hotkey state)
            // When not in lazy mode, check hotkey state normally
            bool keyState = lazyModeActive || (_settings?.ClickLabelKey != null && Input.GetKeyState(_settings.ClickLabelKey.Value));

            // Holding the click hotkey should override ritual-in-progress blocking.
            bool clickHotkeyHeld = _settings?.ClickLabelKey != null && Input.GetKeyState(_settings.ClickLabelKey.Value);

            return keyState &&
                IsPOEActive(gameController) &&
                (_settings?.BlockOnOpenLeftRightPanel?.Value != true || !IsPanelOpen(gameController)) &&
                !IsInTownOrHideout(gameController) &&
                !gameController.IngameState.IngameUi.ChatTitlePanel.IsVisible &&
                // Allow clicking during a ritual if the click hotkey is being held
                (!isRitualActive || clickHotkeyHeld) &&
                !gameController.Game.IsEscapeState &&
                !gameController.IngameState.IngameUi.AtlasPanel.IsVisible &&
                !gameController.IngameState.IngameUi.AtlasTreePanel.IsVisible &&
                !gameController.IngameState.IngameUi.TreePanel.IsVisible &&
                !gameController.IngameState.IngameUi.UltimatumPanel.IsVisible &&
                !gameController.IngameState.IngameUi.BetrayalWindow.IsVisible &&
                !gameController.IngameState.IngameUi.SyndicatePanel.IsVisible &&
                !gameController.IngameState.IngameUi.SyndicateTree.IsVisible &&
                !gameController.IngameState.IngameUi.IncursionWindow.IsVisible &&
                !gameController.IngameState.IngameUi.RitualWindow.IsVisible &&
                !gameController.IngameState.IngameUi.SanctumFloorWindow.IsVisible &&
                !gameController.IngameState.IngameUi.SanctumRewardWindow.IsVisible &&
                !gameController.IngameState.IngameUi.MicrotransactionShopWindow.IsVisible &&
                !gameController.IngameState.IngameUi.ResurrectPanel.IsVisible &&
                !gameController.IngameState.IngameUi.NpcDialog.IsVisible &&
                !gameController.IngameState.IngameUi.KalandraTabletWindow.IsVisible;
        }
        public string GetCanClickFailureReason(GameController gameController)
        {
            if (gameController?.Window?.IsForeground() == false)
                return "PoE not in focus.";

            var area = gameController?.Area?.CurrentArea;
            if (_settings.BlockOnOpenLeftRightPanel.Value)
            {
                var ui = gameController?.IngameState?.IngameUi;
                if (ui?.OpenLeftPanel?.Address != 0 || ui?.OpenRightPanel?.Address != 0)
                    return "Panel is open.";
            }

            if (area?.IsTown == true || area?.IsHideout == true)
                return "In town/hideout.";

            var uiState = gameController?.IngameState?.IngameUi;
            var checks = new (bool Condition, string Message)[]
            {
                (uiState?.ChatTitlePanel?.IsVisible == true, "Chat is open."),
                (uiState?.AtlasPanel?.IsVisible == true, "Atlas panel is open."),
                (uiState?.AtlasTreePanel?.IsVisible == true, "Atlas tree panel is open."),
                (uiState?.TreePanel?.IsVisible == true, "Passive tree panel is open."),
                (uiState?.UltimatumPanel?.IsVisible == true, "Ultimatum panel is open."),
                (uiState?.BetrayalWindow?.IsVisible == true, "Betrayal window is open."),
                (uiState?.SyndicatePanel?.IsVisible == true, "Syndicate panel is open."),
                (uiState?.SyndicateTree?.IsVisible == true, "Syndicate tree panel is open."),
                (uiState?.IncursionWindow?.IsVisible == true, "Incursion window is open."),
                (uiState?.RitualWindow?.IsVisible == true, "Ritual window is open."),
                (uiState?.SanctumFloorWindow?.IsVisible == true, "Sanctum floor window is open."),
                (uiState?.SanctumRewardWindow?.IsVisible == true, "Sanctum reward window is open."),
                (uiState?.MicrotransactionShopWindow?.IsVisible == true, "Microtransaction shop window is open."),
                (uiState?.ResurrectPanel?.IsVisible == true, "Resurrect panel is open."),
                (uiState?.NpcDialog?.IsVisible == true, "NPC dialog is open."),
                (uiState?.KalandraTabletWindow?.IsVisible == true, "Kalandra tablet window is open.")
            };

            foreach (var check in checks)
            {
                if (check.Condition)
                    return check.Message;
            }

            if (gameController?.Game?.IsEscapeState == true)
                return "Escape menu is open.";

            return "Clicking disabled.";
        }

        public bool IsClickHotkeyPressed(TimeCache<System.Collections.Generic.List<LabelOnGround>>? cachedLabels, Services.LabelFilterService? labelFilterService)
        {
            bool hotkeyHeld = Input.GetKeyState(_settings.ClickLabelKey.Value);
            if (!_settings.LazyMode.Value)
            {
                return hotkeyHeld;
            }

            var labels = cachedLabels?.Value;
            bool hasRestricted = labelFilterService?.HasLazyModeRestrictedItemsOnScreen(labels) ?? false;
            bool disableKeyHeld = Input.GetKeyState(_settings.LazyModeDisableKey.Value);
            bool leftClickBlocks = _settings.DisableLazyModeLeftClickHeld.Value && Input.GetKeyState(Keys.LButton);
            bool rightClickBlocks = _settings.DisableLazyModeRightClickHeld.Value && Input.GetKeyState(Keys.RButton);
            bool mouseButtonBlocks = leftClickBlocks || rightClickBlocks;

            if (hotkeyHeld)
            {
                return true;
            }

            return !hasRestricted && !disableKeyHeld && !mouseButtonBlocks;
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

        public void PerformClick(Vector2 position, Element? expectedElement = null, GameController? gameController = null)
        {
            if (!TryConsumeLazyModeLimiter())
                return;

            // NOTE: Performing clicks is a real native operation. Tests must not invoke PerformClick via
            // reflection (we rely on test seams to exercise logic without executing native input).
            var swTotal = Stopwatch.StartNew();
            var before = Mouse.GetCursorPosition();

            var sw = Stopwatch.StartNew();
            // Skip native cursor movement during tests / CI when Mouse.DisableNativeInput is enabled
            if (!Mouse.DisableNativeInput)
            {
                Input.SetCursorPos(position);
            }
            sw.Stop();

            var after = Mouse.GetCursorPosition();
            //UIHover needs time to update so we sleep longer in lazy mode, we still sleep in normal mode to give cursor time to move
            if (_settings?.LazyMode?.Value == true)
            {
                Thread.Sleep(_settings.LazyModeUIHoverSleep.Value);
            }
            else
            {
                Thread.Sleep(10);
            }

            var uiHover = gameController?.IngameState?.UIHoverElement;

            // Verify UIHover matches expected element in lazy mode to detect obscuring elements
            if (_settings?.LazyMode != null && _settings.LazyMode.Value && expectedElement != null && uiHover != null)
            {
                if (uiHover.Address != expectedElement?.Address)
                {
                    _errorHandler?.LogMessage(true, true, $"InputHandler: UIHover verification failed - expected element is obscured. Skipping click.", 5);
                    RestoreCursorIfLazyMode(before);
                    return;
                }
            }
            else if (uiHover == null)
            {
                _errorHandler?.LogMessage(true, true, $"InputHandler: UIHover verification failed - UIHover is null", 5);
            }
            else if (expectedElement == null)
            {
                _errorHandler?.LogMessage(true, true, $"InputHandler: UIHover verification skipped - expectedElement is null", 5);
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

        /// <summary>
        /// Move the cursor to the supplied screen point (if native input is allowed),
        /// sleep for the UI hover update, and return the game's UIHoverElement.
        /// Returns null on error or when UIHover is not present.
        /// This method respects Mouse.DisableNativeInput so tests won't move the real cursor.
        /// </summary>
        public Element? HoverAndGetUIHover(Vector2 screenPoint, GameController? gameController, int delayMs = -1)
        {
            if (gameController == null) return null;

            int sleepMs = delayMs;
            if (sleepMs <= 0)
            {
                // Use configured hover sleep when available
                sleepMs = _settings?.LazyModeUIHoverSleep?.Value ?? 20;
            }

            try
            {
                if (!Mouse.DisableNativeInput)
                {
                    Input.SetCursorPos(screenPoint);
                }

                Thread.Sleep(sleepMs);

                var uiHover = gameController?.IngameState?.UIHoverElement;
                return uiHover;
            }
            catch
            {
                return null;
            }
        }

        // probe-based verification removed; simplified HoverAndGetUIHover is used instead

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
                    var beforeVec = new Vector2(before.X, before.Y);
                    if (!Mouse.DisableNativeInput)
                    {
                        Input.SetCursorPos(beforeVec);
                    }
                    // Small delay to let the OS update cursor position
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
