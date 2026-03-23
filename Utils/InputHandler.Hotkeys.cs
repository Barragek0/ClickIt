using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using System.Windows.Forms;

namespace ClickIt.Utils
{
    public partial class InputHandler
    {
        public bool CanClick(GameController gameController, bool hasLazyModeRestrictedItemsOnScreen = false, bool isRitualActive = false)
        {
            if (gameController == null)
                return false;

            bool keyState = IsClickKeyStateActive(hasLazyModeRestrictedItemsOnScreen);
            bool clickHotkeyHeld = IsClickHotkeyHeld();

            return keyState
                && IsPOEActive(gameController)
                && (_settings?.BlockOnOpenLeftRightPanel?.Value != true || !IsPanelOpen(gameController))
                && !IsInTownOrHideout(gameController)
                && (!isRitualActive || clickHotkeyHeld)
                && !IsInToggleItemsPostClickBlockWindow()
                && !IsBlockedByUiOrEscapeState(gameController);
        }

        private bool IsClickKeyStateActive(bool hasLazyModeRestrictedItemsOnScreen)
        {
            bool lazyModeActive = _settings?.LazyMode != null
                && _settings.LazyMode.Value
                && !hasLazyModeRestrictedItemsOnScreen
                && !IsLazyModeDisableActiveForCurrentInputState();

            return lazyModeActive || IsClickHotkeyHeld();
        }

        private bool IsClickHotkeyHeld()
            => IsClickHotkeyActiveForCurrentInputState();

        public bool IsClickHotkeyActiveForCurrentInputState()
        {
            if (_settings?.ClickLabelKey == null)
                return false;

            bool toggleMode = _settings.IsClickHotkeyToggleModeEnabled();
            bool keyDown = Input.GetKeyState(_settings.ClickLabelKey.Value);
            return ResolveClickHotkeyActive(toggleMode, keyDown, ref _clickHotkeyToggled, ref _clickHotkeyWasDown);
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

            if (uiState?.ChatTitlePanel?.IsVisible ?? false)
                return "Chat is open.";
            if (uiState?.AtlasPanel?.IsVisible ?? false)
                return "Atlas panel is open.";
            if (uiState?.AtlasTreePanel?.IsVisible ?? false)
                return "Atlas tree panel is open.";
            if (uiState?.TreePanel?.IsVisible ?? false)
                return "Passive tree panel is open.";
            if ((uiState?.UltimatumPanel?.IsVisible ?? false) && !_settings.IsOtherUltimatumClickEnabled())
                return "Ultimatum panel is open (Click Ultimatum Choices is disabled).";
            if (uiState?.BetrayalWindow?.IsVisible ?? false)
                return "Betrayal window is open.";
            if (uiState?.SyndicatePanel?.IsVisible ?? false)
                return "Syndicate panel is open.";
            //if (uiState?.SyndicateTree?.IsVisible ?? false)
            //    return "Syndicate tree panel is open.";
            if (uiState?.IncursionWindow?.IsVisible ?? false)
                return "Incursion window is open.";
            if (uiState?.RitualWindow?.IsVisible ?? false)
                return "Ritual window is open.";
            if (uiState?.SanctumFloorWindow?.IsVisible ?? false)
                return "Sanctum floor window is open.";
            if (uiState?.SanctumRewardWindow?.IsVisible ?? false)
                return "Sanctum reward window is open.";
            if (uiState?.MicrotransactionShopWindow?.IsVisible ?? false)
                return "Microtransaction shop window is open.";
            if (uiState?.ResurrectPanel?.IsVisible ?? false)
                return "Resurrect panel is open.";
            if (uiState?.NpcDialog?.IsVisible ?? false)
                return "NPC dialog is open.";

            return null;
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

            if (IsInToggleItemsPostClickBlockWindow())
                return "Waiting after Toggle Item View.";

            string? uiReason = GetUiBlockingReason(gameController);
            if (!string.IsNullOrEmpty(uiReason))
                return uiReason;

            if (gameController?.Game?.IsEscapeState == true)
                return "Escape menu is open.";

            return "Clicking disabled.";
        }

        public bool CanClickWithoutInputState(GameController gameController)
        {
            if (gameController == null)
                return false;

            return IsPOEActive(gameController)
                && (_settings?.BlockOnOpenLeftRightPanel?.Value != true || !IsPanelOpen(gameController))
                && !IsInTownOrHideout(gameController)
                && !IsInToggleItemsPostClickBlockWindow()
                && !IsBlockedByUiOrEscapeState(gameController);
        }

        public bool IsClickHotkeyPressed(TimeCache<List<LabelOnGround>>? cachedLabels, Services.LabelFilterService? labelFilterService)
        {
            bool hotkeyHeld = IsClickHotkeyHeld();
            if (!_settings.LazyMode.Value)
            {
                return hotkeyHeld;
            }

            var labels = cachedLabels?.Value;
            bool hasRestricted = labelFilterService?.HasLazyModeRestrictedItemsOnScreen(labels) ?? false;
            bool disableKeyHeld = IsLazyModeDisableActiveForCurrentInputState();
            var (_, _, mouseButtonBlocks) = GetMouseButtonBlockingState(_settings, Input.GetKeyState);

            if (hotkeyHeld)
            {
                return true;
            }

            return !hasRestricted && !disableKeyHeld && !mouseButtonBlocks;
        }

        public bool IsLazyModeDisableActiveForCurrentInputState()
        {
            if (!_settings.LazyMode.Value)
            {
                _lazyModeDisableToggled = false;
            }

            bool toggleMode = _settings.IsLazyModeDisableHotkeyToggleModeEnabled();
            bool keyDown = Input.GetKeyState(_settings.LazyModeDisableKey.Value);
            return ResolveLazyModeDisableActive(toggleMode, keyDown, ref _lazyModeDisableToggled, ref _lazyModeDisableKeyWasDown);
        }

        public static bool ResolveLazyModeDisableActive(bool toggleModeEnabled, bool disableKeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
        {
            if (!toggleModeEnabled)
            {
                wasPressedLastFrame = disableKeyPressed;
                return disableKeyPressed;
            }

            if (disableKeyPressed && !wasPressedLastFrame)
            {
                toggledState = !toggledState;
            }

            wasPressedLastFrame = disableKeyPressed;
            return toggledState;
        }

        public static bool ResolveClickHotkeyActive(bool toggleModeEnabled, bool hotkeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
        {
            if (!toggleModeEnabled)
            {
                toggledState = false;
                wasPressedLastFrame = hotkeyPressed;
                return hotkeyPressed;
            }

            if (hotkeyPressed && !wasPressedLastFrame)
            {
                toggledState = !toggledState;
            }

            wasPressedLastFrame = hotkeyPressed;
            return toggledState;
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
            => gameController.Window.IsForeground();

        private static bool IsPanelOpen(GameController gameController)
        {
            if (gameController == null)
                return false;

            var ui = gameController.IngameState?.IngameUi;
            if (ui == null)
                return false;

            return ui.OpenLeftPanel.Address != 0 || ui.OpenRightPanel.Address != 0;
        }

        private static bool IsInTownOrHideout(GameController gameController)
        {
            if (gameController == null)
                return false;

            var area = gameController.Area?.CurrentArea;
            if (area == null)
                return false;

            return area.IsHideout || area.IsTown;
        }
    }
}