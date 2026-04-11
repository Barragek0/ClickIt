namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumPanelUiQuery
    {
        internal static bool TryGetVisiblePanel(
            GameController? gameController,
            bool logFailures,
            Action<string> debugLog,
            out UltimatumPanel? panelObj)
        {
            panelObj = null;
            if (!TryGetVisiblePanelObject(gameController, logFailures, debugLog, out object? panelObject)
                || panelObject is not UltimatumPanel resolvedPanel)
            {
                if (logFailures && panelObject != null)
                    debugLog("[TryHandleUltimatumPanelUi] UltimatumPanel object is not the expected runtime type.");

                return false;
            }

            panelObj = resolvedPanel;
            return true;
        }

        internal static bool TryGetVisiblePanelObject(
            object? gameController,
            bool logFailures,
            Action<string> debugLog,
            out object? panelObj)
        {
            panelObj = null;

            if (!DynamicAccess.TryGetDynamicValue(gameController, DynamicAccessProfiles.GameControllerIngameState, out object? ingameState)
                || ingameState == null
                || !DynamicAccess.TryGetDynamicValue(ingameState, DynamicAccessProfiles.IngameStateIngameUi, out object? ingameUi)
                || ingameUi == null
                || !DynamicAccess.TryGetDynamicValue(ingameUi, DynamicAccessProfiles.IngameUiUltimatumPanel, out panelObj)
                || panelObj == null)
            {
                if (logFailures)
                    debugLog("[TryHandleUltimatumPanelUi] UltimatumPanel not available.");
                return false;
            }

            if (!DynamicAccess.TryReadBool(panelObj, DynamicAccessProfiles.IsVisible, out bool isVisible) || !isVisible)
            {
                if (logFailures)
                    debugLog("[TryHandleUltimatumPanelUi] UltimatumPanel exists but is not visible.");
                panelObj = null;
                return false;
            }

            return true;
        }
    }
}