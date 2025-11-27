using System;

namespace ClickIt.Utils
{
    // Test-only seam helpers for InputHandler to avoid constructing complex ExileCore.GameController objects
    public partial class InputHandler
    {
        internal static string GetCanClickFailureReasonForTests(
            bool? windowIsForeground = null,
            bool blockOnOpenLeftRightPanel = false,
            long openLeftPanelAddress = 0,
            long openRightPanelAddress = 0,
            bool isTown = false,
            bool isHideout = false,
            bool chatTitlePanelIsVisible = false,
            bool escapeState = false)
        {
            if (windowIsForeground == false)
                return "PoE not in focus.";

            if (blockOnOpenLeftRightPanel)
            {
                if (openLeftPanelAddress != 0 || openRightPanelAddress != 0)
                    return "Panel is open.";
            }

            if (isTown || isHideout)
                return "In town/hideout.";

            if (chatTitlePanelIsVisible)
                return "Chat is open.";

            if (escapeState)
                return "Escape menu is open.";

            return "Clicking disabled.";
        }

        // Test seam to exercise IsClickHotkeyPressed logic without native Input and label objects.
        internal static bool IsClickHotkeyPressedForTests(
            bool lazyMode,
            bool hotkeyHeld,
            bool hasRestrictedItemsOnScreen,
            bool disableKeyHeld,
            bool disableLeftClickHeldSetting,
            bool leftClickHeld,
            bool disableRightClickHeldSetting,
            bool rightClickHeld)
        {
            if (!lazyMode)
                return hotkeyHeld;

            if (hotkeyHeld)
                return true;

            bool leftClickBlocks = disableLeftClickHeldSetting && leftClickHeld;
            bool rightClickBlocks = disableRightClickHeldSetting && rightClickHeld;
            bool mouseButtonBlocks = leftClickBlocks || rightClickBlocks;

            return !hasRestrictedItemsOnScreen && !disableKeyHeld && !mouseButtonBlocks;
        }
    }
}
