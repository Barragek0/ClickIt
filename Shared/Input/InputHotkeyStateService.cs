namespace ClickIt.Shared.Input
{
    internal sealed class InputHotkeyStateService(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Lock _lock = new();
        private bool _lazyModeDisableToggled;
        private bool _lazyModeDisableKeyWasDown;
        private bool _clickHotkeyToggled;
        private bool _clickHotkeyWasDown;

        internal bool IsClickHotkeyActive(Func<Keys, bool> keyStateProvider)
        {
            Keys clickHotkey = _settings.ClickLabelKeyBinding;
            if (clickHotkey == Keys.None)
                return false;

            lock (_lock)
            {
                bool toggleMode = _settings.IsClickHotkeyToggleModeEnabled();
                bool keyDown = keyStateProvider(clickHotkey);
                return ResolveToggleHotkeyActive(toggleMode, keyDown, ref _clickHotkeyToggled, ref _clickHotkeyWasDown);
            }
        }

        internal bool IsLazyModeDisableActive(Func<Keys, bool> keyStateProvider)
        {
            lock (_lock)
            {
                if (!_settings.LazyMode.Value)
                    _lazyModeDisableToggled = false;

                bool toggleMode = _settings.IsLazyModeDisableHotkeyToggleModeEnabled();
                bool keyDown = keyStateProvider(_settings.LazyModeDisableKeyBinding);
                return ResolveToggleHotkeyActive(toggleMode, keyDown, ref _lazyModeDisableToggled, ref _lazyModeDisableKeyWasDown);
            }
        }

        // Shared hotkey toggle/edge resolver: in hold mode the key's raw state is returned (toggle state reset); in toggle mode a fresh key press edge flips the toggle state.
        internal static bool ResolveToggleHotkeyActive(bool toggleModeEnabled, bool hotkeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
        {
            if (!toggleModeEnabled)
            {
                toggledState = false;
                wasPressedLastFrame = hotkeyPressed;
                return hotkeyPressed;
            }

            if (hotkeyPressed && !wasPressedLastFrame)
                toggledState = !toggledState;


            wasPressedLastFrame = hotkeyPressed;
            return toggledState;
        }

        internal static (bool leftClickBlocks, bool rightClickBlocks, bool mouseButtonBlocks) GetMouseButtonBlockingState(ClickItSettings settings, Func<Keys, bool> keyStateProvider)
        {
            if (settings == null || keyStateProvider == null)
                return (false, false, false);


            bool leftClickBlocks = settings.DisableLazyModeLeftClickHeld.Value && keyStateProvider(Keys.LButton);
            bool rightClickBlocks = settings.DisableLazyModeRightClickHeld.Value && keyStateProvider(Keys.RButton);
            return (leftClickBlocks, rightClickBlocks, leftClickBlocks || rightClickBlocks);
        }
    }
}