namespace ClickIt.UI.Settings.Panels;

internal sealed class HarvestSettingsPanelRenderer(ClickItSettings settings)
{
    private readonly ClickItSettings _settings = settings;

    internal void DrawPanel()
    {
        ImGui.Spacing();

        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Lifeforce Estimation##HarvestLifeforceEstimation",
            _settings.HarvestLifeforceEstimation,
            "When enabled, ClickIt makes a rough estimate of the lifeforce yield\n" +
            "for each visible harvest plot and only clicks the one with the highest\n" + "estimated yield. If only one harvest plot is visible, ClickIt will not click.\n" +
            "It will wait for 2 harvest plots to be visible so it can compare their estimated yields.\n\n" +
            "When disabled, behaves as a simple nearest-harvest toggle.");
    }
}
