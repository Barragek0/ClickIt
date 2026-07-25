namespace ClickIt.UI.Settings
{
    internal static class SettingsScreenComposer
    {
        internal static CustomNode CreateSliderWidthBoundaryNode(Action drawDelegate)
            => new()
            {
                DrawDelegate = drawDelegate
            };

        internal static CustomNode CreateSafePanelNode(
            string panelName,
            Action drawPanel,
            Action<string, Action> drawPanelSafe)
            => new()
            {
                DrawDelegate = () => drawPanelSafe(panelName, drawPanel)
            };
    }
}