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
            panelObj = gameController?.IngameState?.IngameUi?.UltimatumPanel;
            if (panelObj == null)
            {
                if (logFailures)
                    debugLog("[TryHandleUltimatumPanelUi] UltimatumPanel not available.");
                return false;
            }

            if (!panelObj.IsVisible)
            {
                if (logFailures)
                    debugLog("[TryHandleUltimatumPanelUi] UltimatumPanel exists but is not visible.");
                return false;
            }

            return true;
        }
    }
}