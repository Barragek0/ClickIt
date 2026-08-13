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
            "When enabled, the plugin walks toward towers that need building during an active Blight encounter.\n\n" +
            "Tower walking reuses the same offscreen pathfinding as regular labels, so the 'Walk toward Offscreen Labels' control must also be enabled for any walking to happen.\n\n" +
            "Tower building was designed with pathfinding enabled, so it's recommended to keep both settings on, otherwise you'll need to walk to towers manually.");
    }

    private void DrawBlockOtherInteractionsToggle()
    {
        SettingsUiRenderHelpers.DrawToggleNodeControl(
            "Block Other Interactions During Blight##BlightBlockOtherInteractions",
            _settings.BlightBlockOtherInteractions,
            "When enabled, ClickIt blocks all other interactions (e.g. clicking items, chests, etc.) while a Blight encounter is active.\n\n" +
            "This prevents interacting with other objects while the plugin is trying to build towers.\n\n" +
            "It's recommended to keep this enabled.");
    }

    private void DrawVisualizationToggles()
    {
        DrawVisualizeToggleWithChildren(
            "Visualize Tower Dots##BlightVisualizeTowers",
            _settings.BlightVisualizeTowers,
            "When enabled, the foundations the current plan still targets are shown as dots both in the game world and on the world map.\n\n" +
            "Only foundations with pending build or upgrade steps are drawn - foundations the plan has finished, and foundations the plan never targets, are hidden.\n\n" +
            "Each dot is a circle in the colour of the tower the plan intends for that foundation (unbuilt foundations use the planned tower colour; built towers use their current tower colour).\n\n" +
            "The Map and Game sub-options below control which view the dots appear in.",
            "Map##BlightVisualizeTowersMap",
            _settings.BlightVisualizeTowersMap,
            "Render the tower dots on the world map.",
            "Game##BlightVisualizeTowersGame",
            _settings.BlightVisualizeTowersGame,
            "Render the tower dots in the game world.\n\n" +
            "Also shows the plan's pending execution order as numbers stacked above each dot.");

        DrawVisualizeToggleWithChildren(
            "Visualize Tower Ranges##BlightVisualizeTowerRanges",
            _settings.BlightVisualizeTowerRanges,
            "When enabled, each built tower's current range is shown both in the game world and on the world map.\n\n" +
            "The Map and Game sub-options below control which view the ranges appear in.",
            "Map##BlightVisualizeTowerRangesMap",
            _settings.BlightVisualizeTowerRangesMap,
            "Render each built tower's range on the world map.",
            "Game##BlightVisualizeTowerRangesGame",
            _settings.BlightVisualizeTowerRangesGame,
            "Render each built tower's range in the game world.");
    }

    private static void DrawVisualizeToggleWithChildren(
        string label,
        ToggleNode parent,
        string parentTooltip,
        string mapLabel,
        ToggleNode mapNode,
        string mapTooltip,
        string gameLabel,
        ToggleNode gameNode,
        string gameTooltip)
    {
        SettingsUiRenderHelpers.DrawToggleNodeControl(label, parent, parentTooltip);
        if (!parent.Value)
            return;

        ImGui.Indent();
        SettingsUiRenderHelpers.DrawToggleNodeControl(mapLabel, mapNode, mapTooltip);
        SettingsUiRenderHelpers.DrawToggleNodeControl(gameLabel, gameNode, gameTooltip);
        ImGui.Unindent();
    }

    private void DrawStrategySelector()
    {
        ImGui.Text("Tower Strategy:");
        ImGui.SameLine();

        // The combo works on the display index; the setting stores the persisted strategy value.
        string[] strategyNames = BlightStrategyResolver.StrategyNames;
        int currentIndex = BlightStrategyResolver.GetComboIndex(_settings.BlightTowerStrategy.Value);
        if (ImGui.Combo("##BlightTowerStrategy", ref currentIndex, strategyNames, strategyNames.Length))
        {
            _settings.BlightTowerStrategy.Value = (int)BlightStrategyResolver.Strategies[currentIndex];
        }

        ImGui.Spacing();

        BlightTowerStrategy strategy = BlightStrategyResolver.Strategies[currentIndex];
        string description = BlightStrategyResolver.GetDescription(strategy);
        if (!string.IsNullOrEmpty(description))
        {
            // Muted grey base (same tone as item-description text elsewhere); oil colour names, tower names, and tower phrases (e.g. "Chilling Beams") are tinted to their actual hues by the resolver.
            SettingsUiRenderHelpers.DrawColoredText(
                description,
                new Vector4(0.65f, 0.65f, 0.65f, 1f),
                BlightDescriptionColors.TryResolvePhrase);
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
