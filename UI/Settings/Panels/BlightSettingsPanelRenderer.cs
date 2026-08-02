namespace ClickIt.UI.Settings.Panels;

internal sealed class BlightSettingsPanelRenderer(ClickItSettings settings)
{
    private readonly ClickItSettings _settings = settings;

    internal void DrawPanel()
    {
        ImGui.Spacing();

        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Click Blight Pump to Start the Encounter##ClickBlightPump",
            _settings.ClickBlightPump,
            "When enabled, ClickIt clicks the Blight pump to start the encounter.\n\n" +
            "The pump must be clicked before the blight lanes spawn and tower building can begin.\n\n" +
            "Disable this if you want to start encounters manually and only let ClickIt handle the rest.");

        ImGui.Spacing();

        if (!ImGui.TreeNodeEx("Tower Building##BlightTowerBuilding", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Build Towers During Blight##ClickBlightTowers",
            _settings.ClickBlightTowers,
            "When enabled, ClickIt automatically scans for Blight tower foundations and builds towers according to the selected strategy during Blight encounters.\n\n" +
            "Tower building only runs while a Blight encounter is active (pump clicked and lanes spawned).");

        if (_settings.ClickBlightTowers.Value)
        {
            ImGui.Spacing();
            DrawStrategySelector();
            ImGui.Spacing();
            DrawBlockOtherInteractionsToggle();
            DrawPathfindingToggle();
            ImGui.Spacing();
            DrawVisualizationToggles();
            ImGui.Spacing();
            DrawDelayControls();
        }

        ImGui.TreePop();
    }

    private void DrawPathfindingToggle()
    {
        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Pathfind to Tower Builds##BlightPathfindToBuild",
            _settings.BlightPathfindToBuild,
            "When enabled, the plugin pathfinds toward towers that need building even if the main pathfinding setting is disabled.\n\n" +
            "Only applies while a Blight encounter is active.\n\n" +
            "Tower building was designed with pathfinding enabled, so it's recommended to keep this setting on — otherwise you'll need to walk to towers manually without feedback from the plugin.");
    }

    private void DrawBlockOtherInteractionsToggle()
    {
        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Block Other Interactions During Blight##BlightBlockOtherInteractions",
            _settings.BlightBlockOtherInteractions,
            "When enabled, ClickIt blocks all other interactions (e.g. clicking harvest plots, chests, etc.) while a Blight encounter is active.\n\n" +
            "This prevents interacting with other objects while the plugin is trying to build towers.\n\n" +
            "It's recommended to keep this enabled.");
    }

    private void DrawVisualizationToggles()
    {
        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Visualize Blight Lanes##BlightVisualizePaths",
            _settings.BlightVisualizePaths,
            "When enabled, the blight lane pathways are rendered both in the game world over the lanes and on the world map.");

        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Visualize Tower Dots##BlightVisualizeTowers",
            _settings.BlightVisualizeTowers,
            "When enabled, every tower foundation and tower is shown as a dot both in the game world and on the world map.\n\n" +
            "Each dot is a circle in the colour of the tower the plan intends for that foundation (unbuilt foundations use the planned tower colour; built towers use their current tower colour).\n\n" +
            "The sub-options below control the plan-order numbers and the tower ranges.");

        if (_settings.BlightVisualizeTowers.Value)
        {
            ImGui.Indent();

            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Visualize Tower Ranges##BlightVisualizeTowerRanges",
                _settings.BlightVisualizeTowerRanges,
                "When enabled, each built tower's current range is shown both in the game world and on the world map.");

            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Visualize Upgrade Order##BlightVisualizeUpgrades",
                _settings.BlightVisualizeUpgrades,
                "When enabled, each tower dot shows the plan's execution order as numbers stacked in the centre of the dot, both in the game world and on the world map.\n\n" +
                "The numbers are the 1-based plan step numbers still pending for that foundation (for example, a tower built then upgraded twice shows 1, 3, 4).\n\n" +
                "As the plan runs, completed steps disappear, so the numbers shrink and finally vanish once the plan is done.");

            ImGui.Unindent();
        }
    }

    private void DrawStrategySelector()
    {
        ImGui.Text("Tower Strategy:");
        ImGui.SameLine();

        int currentStrategy = _settings.BlightTowerStrategy.Value;
        string[] strategyNames = BlightStrategyResolver.StrategyNames;

        if (ImGui.Combo("##BlightTowerStrategy", ref currentStrategy, strategyNames, strategyNames.Length))
        {
            _settings.BlightTowerStrategy.Value = currentStrategy;
        }

        ImGui.Spacing();

        BlightTowerStrategy strategy = (BlightTowerStrategy)currentStrategy;
        string description = BlightStrategyResolver.GetDescription(strategy);
        if (!string.IsNullOrEmpty(description))
        {
            SettingsUiRenderHelpers.DrawWrappedText(description, new Vector4(0.95f, 0.85f, 0.35f, 1f));
        }
    }

    private void DrawDelayControls()
    {
        SettingsUiRenderHelpers.DrawRangeNodeControl(
            "Build Delay (ms)",
            _settings.BlightTowerBuildDelayMs,
            200,
            400,
            "Delay between build actions in the tower plan.\n\n" +
            "Lower values build faster; higher values are safer if towers are placed far apart.");

        SettingsUiRenderHelpers.DrawRangeNodeControl(
            "Upgrade Delay (ms)",
            _settings.BlightTowerUpgradeDelayMs,
            200,
            400,
            "Delay between upgrade actions in the tower plan.\n\n" +
            "Lower values upgrade faster; higher values are safer if towers are placed far apart.");
    }
}
