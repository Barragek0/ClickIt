namespace ClickIt.Shared.Input
{
    internal sealed class InputHotkeyStateService(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;
        private bool _lazyModeDisableToggled;
        private bool _lazyModeDisableKeyWasDown;
        private bool _clickHotkeyToggled;
        private bool _clickHotkeyWasDown;

        internal bool IsClickHotkeyActive(Func<Keys, bool> keyStateProvider)
        {
            Keys clickHotkey = _settings.ClickLabelKeyBinding;
            if (clickHotkey == Keys.None)
                return false;


            bool toggleMode = _settings.IsClickHotkeyToggleModeEnabled();
            bool keyDown = keyStateProvider(clickHotkey);
            return ResolveClickHotkeyActive(toggleMode, keyDown, ref _clickHotkeyToggled, ref _clickHotkeyWasDown);
        }

        internal bool IsLazyModeDisableActive(Func<Keys, bool> keyStateProvider)
        {
            if (!_settings.LazyMode.Value)
                _lazyModeDisableToggled = false;


            bool toggleMode = _settings.IsLazyModeDisableHotkeyToggleModeEnabled();
            bool keyDown = keyStateProvider(_settings.LazyModeDisableKeyBinding);
            return ResolveLazyModeDisableActive(toggleMode, keyDown, ref _lazyModeDisableToggled, ref _lazyModeDisableKeyWasDown);
        }

        internal static bool ResolveLazyModeDisableActive(bool toggleModeEnabled, bool disableKeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
        {
            if (!toggleModeEnabled)
            {
                wasPressedLastFrame = disableKeyPressed;
                return disableKeyPressed;
            }

            if (disableKeyPressed && !wasPressedLastFrame)
                toggledState = !toggledState;


            wasPressedLastFrame = disableKeyPressed;
            return toggledState;
        }

        internal static bool ResolveClickHotkeyActive(bool toggleModeEnabled, bool hotkeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
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