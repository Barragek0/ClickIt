namespace ClickIt.UI.Settings.Panels;

internal sealed class HarvestSettingsPanelRenderer(ClickItSettings settings)
{
    private readonly ClickItSettings _settings = settings;

    internal void DrawPanel()
    {
        ImGui.Spacing();

        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Show Visual Indicator##ShowHarvestLifeforceEstimation",
            _settings.ShowHarvestLifeforceEstimation,
            "Shows a visual indicator as to which harvest plot will be clicked on\n" +
            "based on the setting below.");

        ImGui.Spacing();

        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Click Higher Estimate##ClickHigherHarvestEstimate",
            _settings.ClickHigherHarvestEstimate,
            "When enabled, ClickIt will click the plot with the highest estimated yield.\n" +
            "It will not click until 2 harvest plots are visible so it can compare their\n" +
            "estimated yields.\n\n" +
            "When disabled, ClickIt will click the harvest plot nearest to the player\n" +
            "regardless of its estimated lifeforce yield.");
    }
}
