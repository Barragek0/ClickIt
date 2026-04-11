namespace ClickIt.Shared.Input
{
    internal readonly record struct UiBlockingState(
        bool ChatOpen,
        bool AtlasPanelOpen,
        bool AtlasTreePanelOpen,
        bool PassiveTreePanelOpen,
        bool UltimatumPanelOpen,
        bool SyndicatePanelOpen,
        bool IncursionWindowOpen,
        bool RitualWindowOpen,
        bool SanctumFloorWindowOpen,
        bool SanctumRewardWindowOpen,
        bool MicrotransactionShopWindowOpen,
        bool ResurrectPanelOpen,
        bool NpcDialogOpen);

    public class InputHandler(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly ToggleItemsController _toggleItemsController = new(settings, Keyboard.KeyPress);
        private readonly InputHotkeyStateService _hotkeyStateService = new(settings);

        public bool TriggerToggleItems()
            => _toggleItemsController.TriggerToggleItems();

        public int GetToggleItemsPostClickBlockMs()
            => _toggleItemsController.GetToggleItemsPostClickBlockMs();

        public bool CanClick(GameController gameController, bool hasLazyModeRestrictedItemsOnScreen = false, bool isRitualActive = false)
        {
            if (gameController == null)
                return false;

            bool keyState = IsClickKeyStateActive(hasLazyModeRestrictedItemsOnScreen);
            if (!keyState)
                return false;

            bool clickHotkeyHeld = IsClickHotkeyHeld();
            bool blockOnOpenPanels = _settings?.BlockOnOpenLeftRightPanel?.Value == true;
            bool isPoeActive = IsPOEActive(gameController);
            bool isPanelOpen = IsPanelOpen(gameController);
            bool isInTownOrHideout = IsInTownOrHideout(gameController);
            bool isInToggleItemsPostClickBlockWindow = IsInToggleItemsPostClickBlockWindow();
            bool isEscapeState = IsEscapeState(gameController);
            string? uiBlockingReason = GetUiBlockingReason(gameController);

            return keyState
                && ShouldAllowClickWithoutInputState(
                    isPoeActive,
                    isPanelOpen,
                    isInTownOrHideout,
                    isInToggleItemsPostClickBlockWindow,
                    isEscapeState,
                    uiBlockingReason,
                    blockOnOpenPanels)
                && (!isRitualActive || clickHotkeyHeld)
                ;
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
            => _hotkeyStateService.IsClickHotkeyActive(Keyboard.IsKeyDown);

        private bool IsBlockedByUiOrEscapeState(GameController gameController)
        {
            if (gameController.Game.IsEscapeState)
                return true;

            return GetUiBlockingReason(gameController) != null;
        }

        private string? GetUiBlockingReason(GameController? gameController)
            => ResolveUiBlockingReason(
                CaptureUiBlockingState(gameController?.IngameState?.IngameUi),
                _settings.IsOtherUltimatumClickEnabled());

        internal static UiBlockingState CaptureUiBlockingState(object? uiState)
        {
            return new UiBlockingState(
                ChatOpen: IsUiElementVisible(uiState, "ChatTitlePanel"),
                AtlasPanelOpen: IsUiElementVisible(uiState, "Atlas", "AtlasPanel"),
                AtlasTreePanelOpen: IsUiElementVisible(uiState, "AtlasTreePanel"),
                PassiveTreePanelOpen: IsUiElementVisible(uiState, "TreePanel"),
                UltimatumPanelOpen: IsUiElementVisible(uiState, "UltimatumPanel"),
                SyndicatePanelOpen: IsUiElementVisible(uiState, "SyndicatePanel", "BetrayalWindow"),
                IncursionWindowOpen: IsUiElementVisible(uiState, "IncursionWindow"),
                RitualWindowOpen: IsUiElementVisible(uiState, "RitualWindow"),
                SanctumFloorWindowOpen: IsUiElementVisible(uiState, "SanctumFloorWindow"),
                SanctumRewardWindowOpen: IsUiElementVisible(uiState, "SanctumRewardWindow"),
                MicrotransactionShopWindowOpen: IsUiElementVisible(uiState, "MicrotransactionShopWindow"),
                ResurrectPanelOpen: IsUiElementVisible(uiState, "ResurrectPanel"),
                NpcDialogOpen: IsUiElementVisible(uiState, "NpcDialog"));
        }

        internal static string? ResolveUiBlockingReason(UiBlockingState state, bool otherUltimatumClickEnabled)
        {
            if (state.ChatOpen)
                return "Chat is open.";
            if (state.AtlasPanelOpen)
                return "Atlas panel is open.";
            if (state.AtlasTreePanelOpen)
                return "Atlas tree panel is open.";
            if (state.PassiveTreePanelOpen)
                return "Passive tree panel is open.";
            if (state.UltimatumPanelOpen && !otherUltimatumClickEnabled)
                return "Ultimatum panel is open (Click Ultimatum Choices is disabled).";
            if (state.SyndicatePanelOpen)
                return "Syndicate panel is open.";
            if (state.IncursionWindowOpen)
                return "Incursion window is open.";
            if (state.RitualWindowOpen)
                return "Ritual window is open.";
            if (state.SanctumFloorWindowOpen)
                return "Sanctum floor window is open.";
            if (state.SanctumRewardWindowOpen)
                return "Sanctum reward window is open.";
            if (state.MicrotransactionShopWindowOpen)
                return "Microtransaction shop window is open.";
            if (state.ResurrectPanelOpen)
                return "Resurrect panel is open.";
            if (state.NpcDialogOpen)
                return "NPC dialog is open.";

            return null;
        }

        private static bool IsUiElementVisible(object? uiState, params string[] propertyNames)
        {
            if (uiState == null)
                return false;

            Type uiStateType = uiState.GetType();
            for (int i = 0; i < propertyNames.Length; i++)
            {
                object? element = uiStateType.GetProperty(propertyNames[i])?.GetValue(uiState);
                if (element?.GetType().GetProperty("IsVisible")?.GetValue(element) is true)
                    return true;
            }

            return false;
        }

        public string GetCanClickFailureReason(GameController gameController)
        {
            return ResolveCanClickFailureReason(
                isPoeActive: IsPOEActive(gameController),
                isPanelOpen: gameController != null && IsPanelOpen(gameController),
                isInTownOrHideout: gameController != null && IsInTownOrHideout(gameController),
                isInToggleItemsPostClickBlockWindow: IsInToggleItemsPostClickBlockWindow(),
                isEscapeState: IsEscapeState(gameController),
                uiBlockingReason: GetUiBlockingReason(gameController),
                blockOnOpenPanels: _settings.BlockOnOpenLeftRightPanel.Value);
        }

        public bool CanClickWithoutInputState(GameController gameController)
        {
            if (gameController == null)
                return false;

            return ShouldAllowClickWithoutInputState(
                isPoeActive: IsPOEActive(gameController),
                isPanelOpen: IsPanelOpen(gameController),
                isInTownOrHideout: IsInTownOrHideout(gameController),
                isInToggleItemsPostClickBlockWindow: IsInToggleItemsPostClickBlockWindow(),
                isEscapeState: IsEscapeState(gameController),
                uiBlockingReason: GetUiBlockingReason(gameController),
                blockOnOpenPanels: _settings?.BlockOnOpenLeftRightPanel?.Value == true);
        }

        internal static bool ShouldAllowClickWithoutInputState(
            bool isPoeActive,
            bool isPanelOpen,
            bool isInTownOrHideout,
            bool isInToggleItemsPostClickBlockWindow,
            bool isEscapeState,
            string? uiBlockingReason,
            bool blockOnOpenPanels)
        {
            return isPoeActive
                && (!blockOnOpenPanels || !isPanelOpen)
                && !isInTownOrHideout
                && !isInToggleItemsPostClickBlockWindow
                && !isEscapeState
                && string.IsNullOrEmpty(uiBlockingReason);
        }

        internal static string ResolveCanClickFailureReason(
            bool isPoeActive,
            bool isPanelOpen,
            bool isInTownOrHideout,
            bool isInToggleItemsPostClickBlockWindow,
            bool isEscapeState,
            string? uiBlockingReason,
            bool blockOnOpenPanels)
        {
            if (!isPoeActive)
                return "PoE not in focus.";
            if (blockOnOpenPanels && isPanelOpen)
                return "Panel is open.";
            if (isInTownOrHideout)
                return "In town/hideout.";
            if (isInToggleItemsPostClickBlockWindow)
                return "Waiting after Toggle Item View.";
            if (!string.IsNullOrEmpty(uiBlockingReason))
                return uiBlockingReason;
            if (isEscapeState)
                return "Escape menu is open.";

            return "Clicking disabled.";
        }

        public bool IsClickHotkeyPressed(
            TimeCache<List<LabelOnGround>>? cachedLabels,
            Func<IReadOnlyList<LabelOnGround>?, bool>? hasLazyModeRestrictedItemsOnScreen)
        {
            bool hotkeyHeld = IsClickHotkeyHeld();
            if (!_settings.LazyMode.Value)
            {
                return hotkeyHeld;
            }

            List<LabelOnGround>? labels = cachedLabels?.Value;
            bool hasRestricted = hasLazyModeRestrictedItemsOnScreen?.Invoke(labels) ?? false;
            bool disableKeyHeld = IsLazyModeDisableActiveForCurrentInputState();

            if (hotkeyHeld)
            {
                return true;
            }

            return !hasRestricted && !disableKeyHeld;
        }

        public bool IsLazyModeDisableActiveForCurrentInputState()
            => _hotkeyStateService.IsLazyModeDisableActive(Keyboard.IsKeyDown);

        public static bool ResolveLazyModeDisableActive(bool toggleModeEnabled, bool disableKeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
            => InputHotkeyStateService.ResolveLazyModeDisableActive(toggleModeEnabled, disableKeyPressed, ref toggledState, ref wasPressedLastFrame);

        public static bool ResolveClickHotkeyActive(bool toggleModeEnabled, bool hotkeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
            => InputHotkeyStateService.ResolveClickHotkeyActive(toggleModeEnabled, hotkeyPressed, ref toggledState, ref wasPressedLastFrame);

        public static (bool leftClickBlocks, bool rightClickBlocks, bool mouseButtonBlocks)
            GetMouseButtonBlockingState(ClickItSettings settings, Func<Keys, bool> keyStateProvider)
            => InputHotkeyStateService.GetMouseButtonBlockingState(settings, keyStateProvider);

        private static bool IsPOEActive(GameController? gameController)
        {
            return DynamicAccess.TryReadBool(gameController?.Window, DynamicAccessProfiles.WindowIsForeground, out bool isForeground)
                && isForeground;
        }

        private static bool IsEscapeState(GameController? gameController)
        {
            return DynamicAccess.TryReadBool(gameController?.Game, DynamicAccessProfiles.IsEscapeState, out bool isEscapeState)
                && isEscapeState;
        }

        private static bool IsPanelOpen(GameController gameController)
        {
            if (gameController == null)
                return false;

            IngameUIElements? ui = gameController.IngameState?.IngameUi;
            if (ui == null)
                return false;

            return HasOpenSidePanels(ui.OpenLeftPanel.Address, ui.OpenRightPanel.Address);
        }

        private static bool IsInTownOrHideout(GameController gameController)
        {
            if (gameController == null)
                return false;

            AreaInstance? area = gameController.Area?.CurrentArea;
            if (area == null)
                return false;

            return IsTownOrHideoutArea(area.IsHideout, area.IsTown);
        }

        internal static bool HasOpenSidePanels(long leftPanelAddress, long rightPanelAddress)
            => leftPanelAddress != 0 || rightPanelAddress != 0;

        internal static bool IsTownOrHideoutArea(bool isHideout, bool isTown)
            => isHideout || isTown;

        private bool IsInToggleItemsPostClickBlockWindow()
            => _toggleItemsController.IsInPostClickBlockWindow();
    }
}
