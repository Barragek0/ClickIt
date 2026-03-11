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
    public partial class InputHandler(ClickItSettings settings, PerformanceMonitor performanceMonitor, ErrorHandler? errorHandler = null)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Random _random = new();
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

            bool keyState = IsClickKeyStateActive(hasLazyModeRestrictedItemsOnScreen);
            bool clickHotkeyHeld = IsClickHotkeyHeld();

            return keyState &&
                IsPOEActive(gameController) &&
                (_settings?.BlockOnOpenLeftRightPanel?.Value != true || !IsPanelOpen(gameController)) &&
                !IsInTownOrHideout(gameController) &&
                // Allow clicking during a ritual if the click hotkey is being held
                (!isRitualActive || clickHotkeyHeld) &&
                !IsBlockedByUiOrEscapeState(gameController);
        }

        private bool IsClickKeyStateActive(bool hasLazyModeRestrictedItemsOnScreen)
        {
            // Lazy mode is active when:
            // - Lazy mode is enabled
            // - No restricted items are on screen
            // - Lazy mode disable hotkey is NOT being held
            bool lazyModeActive = _settings?.LazyMode != null
                && _settings.LazyMode.Value
                && !hasLazyModeRestrictedItemsOnScreen
                && !Input.GetKeyState(_settings.LazyModeDisableKey.Value);

            // In lazy mode, always allow clicking (ignore hotkey state)
            // When not in lazy mode, check hotkey state normally
            return lazyModeActive || IsClickHotkeyHeld();
        }

        private bool IsClickHotkeyHeld()
        {
            return _settings?.ClickLabelKey != null && Input.GetKeyState(_settings.ClickLabelKey.Value);
        }

        private bool IsBlockedByUiOrEscapeState(GameController gameController)
        {
            if (gameController.Game.IsEscapeState)
                return true;

            return GetUiBlockingReason(gameController) != null;
        }

        private string? GetUiBlockingReason(GameController? gameController)
        {
            var uiState = gameController?.IngameState?.IngameUi;

            var rules = new (string PropertyName, string Message, bool RequiresUltimatumDisabled)[]
            {
                ("ChatTitlePanel", "Chat is open.", false),
                ("AtlasPanel", "Atlas panel is open.", false),
                ("AtlasTreePanel", "Atlas tree panel is open.", false),
                ("TreePanel", "Passive tree panel is open.", false),
                ("UltimatumPanel", "Ultimatum panel is open (ClickUltimatum is disabled).", true),
                ("BetrayalWindow", "Betrayal window is open.", false),
                ("SyndicatePanel", "Syndicate panel is open.", false),
                ("SyndicateTree", "Syndicate tree panel is open.", false),
                ("IncursionWindow", "Incursion window is open.", false),
                ("RitualWindow", "Ritual window is open.", false),
                ("SanctumFloorWindow", "Sanctum floor window is open.", false),
                ("SanctumRewardWindow", "Sanctum reward window is open.", false),
                ("MicrotransactionShopWindow", "Microtransaction shop window is open.", false),
                ("ResurrectPanel", "Resurrect panel is open.", false),
                ("NpcDialog", "NPC dialog is open.", false),
                ("KalandraTabletWindow", "Kalandra tablet window is open.", false)
            };

            foreach ((string propertyName, string message, bool requiresUltimatumDisabled) in rules)
            {
                if (requiresUltimatumDisabled && _settings.ClickUltimatum.Value)
                    continue;
                if (IsUiPanelVisible(uiState, propertyName))
                    return message;
            }

            return null;
        }

        private static bool IsUiPanelVisible(object? uiState, string panelPropertyName)
        {
            if (uiState == null || string.IsNullOrWhiteSpace(panelPropertyName))
                return false;

            var panelProperty = uiState.GetType().GetProperty(panelPropertyName);
            var panelObject = panelProperty?.GetValue(uiState);
            if (panelObject == null)
                return false;

            var isVisibleProperty = panelObject.GetType().GetProperty("IsVisible");
            if (isVisibleProperty?.GetValue(panelObject) is bool isVisible)
                return isVisible;

            return false;
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

            string? uiReason = GetUiBlockingReason(gameController);
            if (!string.IsNullOrEmpty(uiReason))
                return uiReason;

            if (gameController?.Game?.IsEscapeState == true)
                return "Escape menu is open.";

            return "Clicking disabled.";
        }

        public bool IsClickHotkeyPressed(TimeCache<List<LabelOnGround>>? cachedLabels, Services.LabelFilterService? labelFilterService)
        {
            bool hotkeyHeld = Input.GetKeyState(_settings.ClickLabelKey.Value);
            if (!_settings.LazyMode.Value)
            {
                return hotkeyHeld;
            }

            var labels = cachedLabels?.Value;
            bool hasRestricted = labelFilterService?.HasLazyModeRestrictedItemsOnScreen(labels) ?? false;
            bool disableKeyHeld = Input.GetKeyState(_settings.LazyModeDisableKey.Value);
            var (_, _, mouseButtonBlocks) = GetMouseButtonBlockingState(_settings, Input.GetKeyState);

            if (hotkeyHeld)
            {
                return true;
            }

            return !hasRestricted && !disableKeyHeld && !mouseButtonBlocks;
        }

        public static (bool leftClickBlocks, bool rightClickBlocks, bool mouseButtonBlocks)
            GetMouseButtonBlockingState(ClickItSettings settings, Func<Keys, bool> keyStateProvider)
        {
            if (settings == null || keyStateProvider == null)
                return (false, false, false);

            bool leftClickBlocks = settings.DisableLazyModeLeftClickHeld.Value && keyStateProvider(Keys.LButton);
            bool rightClickBlocks = settings.DisableLazyModeRightClickHeld.Value && keyStateProvider(Keys.RButton);
            return (leftClickBlocks, rightClickBlocks, leftClickBlocks || rightClickBlocks);
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

    }
}
