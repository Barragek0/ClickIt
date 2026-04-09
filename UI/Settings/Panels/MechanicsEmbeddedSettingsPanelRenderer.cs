namespace ClickIt.UI.Settings.Panels
{
    internal sealed class MechanicsEmbeddedSettingsPanelRenderer(
        ClickItSettings settings,
        ItemTypeFiltersPanelRenderer itemTypeFiltersPanelRenderer,
        EssenceCorruptionPanelRenderer essenceCorruptionPanelRenderer,
        StrongboxFilterPanelRenderer strongboxFilterPanelRenderer,
        UltimatumSettingsPanelRenderer ultimatumSettingsPanelRenderer,
        AltarSettingsPanelRenderer altarSettingsPanelRenderer)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly ItemTypeFiltersPanelRenderer _itemTypeFiltersPanelRenderer = itemTypeFiltersPanelRenderer;
        private readonly EssenceCorruptionPanelRenderer _essenceCorruptionPanelRenderer = essenceCorruptionPanelRenderer;
        private readonly StrongboxFilterPanelRenderer _strongboxFilterPanelRenderer = strongboxFilterPanelRenderer;
        private readonly UltimatumSettingsPanelRenderer _ultimatumSettingsPanelRenderer = ultimatumSettingsPanelRenderer;
        private readonly AltarSettingsPanelRenderer _altarSettingsPanelRenderer = altarSettingsPanelRenderer;

        private readonly struct ChestDropSettleSettingsDescriptor(
            string labelPrefix,
            string idPrefix,
            ToggleNode pauseNode,
            RangeNode<int> initialDelayNode,
            RangeNode<int> pollIntervalNode,
            RangeNode<int> quietWindowNode)
        {
            public string LabelPrefix { get; } = labelPrefix;
            public string IdPrefix { get; } = idPrefix;
            public ToggleNode PauseNode { get; } = pauseNode;
            public RangeNode<int> InitialDelayNode { get; } = initialDelayNode;
            public RangeNode<int> PollIntervalNode { get; } = pollIntervalNode;
            public RangeNode<int> QuietWindowNode { get; } = quietWindowNode;
        }

        internal void DrawMechanicEntrySubmenu(string entryId)
        {
            ImGui.Indent();

            if (string.Equals(entryId, MechanicIds.Items, StringComparison.OrdinalIgnoreCase))
                _itemTypeFiltersPanelRenderer.DrawPanel(embedded: true);

            else if (string.Equals(entryId, MechanicIds.Essences, StringComparison.OrdinalIgnoreCase))
            {
                SettingsUiRenderHelpers.DrawToggleNodeControl(
                    "Corrupt ALL Essences##MechanicsEssenceCorruptAll",
                    _settings.CorruptAllEssences,
                    "Overrides the essence table and attempts to corrupt every eligible essence encounter.");

                if (_settings.ShowEssenceCorruptionTablePanel)
                    _essenceCorruptionPanelRenderer.DrawPanel(embedded: true);

            }
            else if (string.Equals(entryId, MechanicIds.Strongboxes, StringComparison.OrdinalIgnoreCase))
                DrawStrongboxSettings();


            ImGui.Unindent();
        }

        internal void DrawMechanicGroupExtraSettings(string groupId)
        {
            if (string.Equals(groupId, "basic-chests", StringComparison.OrdinalIgnoreCase))
            {
                DrawChestDropSettleSettings(new ChestDropSettleSettingsDescriptor("Basic Chest", "BasicChests", _settings.PauseAfterOpeningBasicChests, _settings.PauseAfterOpeningBasicChestsInitialDelayMs, _settings.PauseAfterOpeningBasicChestsPollIntervalMs, _settings.PauseAfterOpeningBasicChestsQuietWindowMs));
                return;
            }

            if (string.Equals(groupId, "league-chests", StringComparison.OrdinalIgnoreCase))
            {
                DrawChestDropSettleSettings(new ChestDropSettleSettingsDescriptor("League Mechanic Chest", "LeagueChests", _settings.PauseAfterOpeningLeagueChests, _settings.PauseAfterOpeningBasicChestsInitialDelayMs, _settings.PauseAfterOpeningBasicChestsPollIntervalMs, _settings.PauseAfterOpeningBasicChestsQuietWindowMs));
                return;
            }

            if (string.Equals(groupId, "delve", StringComparison.OrdinalIgnoreCase))
            {
                DrawDelveSettings();
                return;
            }

            if (string.Equals(groupId, "ultimatum", StringComparison.OrdinalIgnoreCase))
            {
                DrawUltimatumSettings();
                return;
            }

            if (string.Equals(groupId, "altars", StringComparison.OrdinalIgnoreCase))
                DrawAltarsSettings();

        }

        internal void DrawMechanicSubgroupExtraSettings(string groupId, string subgroupName)
        {
            if (string.Equals(groupId, "heist", StringComparison.OrdinalIgnoreCase)
                && string.Equals(subgroupName, "Chests", StringComparison.OrdinalIgnoreCase))
                DrawChestDropSettleSettings(new ChestDropSettleSettingsDescriptor("Heist Chest", "HeistChests", _settings.PauseAfterOpeningHeistChests, _settings.PauseAfterOpeningBasicChestsInitialDelayMs, _settings.PauseAfterOpeningBasicChestsPollIntervalMs, _settings.PauseAfterOpeningBasicChestsQuietWindowMs));

        }

        private void DrawChestDropSettleSettings(ChestDropSettleSettingsDescriptor descriptor)
        {
            ImGui.Spacing();
            SettingsUiRenderHelpers.DrawToggleNodeControl($"Wait for Drops to Settle##{descriptor.IdPrefix}PauseEnabled", descriptor.PauseNode, $"When enabled, ClickIt waits for new loot labels after opening a {descriptor.LabelPrefix} before resuming clicks.");
            SettingsUiRenderHelpers.DrawToggleAndRangeNodeControls(
                $"Allow Nearby Mechanics while Waiting##{descriptor.IdPrefix}AllowNearbyMechanics",
                _settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle,
                "When enabled, nearby mechanics around the opened chest can still be clicked while drops are settling.",
                $"Nearby mechanic distance##{descriptor.IdPrefix}AllowNearbyMechanicsDistance",
                _settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance,
                0,
                100,
                "Maximum distance from the opened chest where mechanics are still allowed during settle wait.");
            SettingsUiRenderHelpers.DrawRangeNodeControl($"Initial delay (ms)##{descriptor.IdPrefix}InitialDelayMs", descriptor.InitialDelayNode, 100, 1500, "How long to wait after click confirmation before checking for new labels.");
            SettingsUiRenderHelpers.DrawRangeNodeControl($"Poll interval (ms)##{descriptor.IdPrefix}PollIntervalMs", descriptor.PollIntervalNode, 50, 500, "How frequently ClickIt checks ItemsOnGroundLabels for newly added drops.");
            SettingsUiRenderHelpers.DrawRangeNodeControl($"Quiet window (ms)##{descriptor.IdPrefix}QuietWindowMs", descriptor.QuietWindowNode, 100, 2000, "Loot is considered settled after this many milliseconds pass without new labels.");
        }

        private void DrawStrongboxSettings()
        {
            ImGui.Spacing();
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Show Strongbox Overlay##MechanicsStrongboxesOverlay",
                _settings.ShowStrongboxFrames,
                "When enabled, draws a visual frame around strongboxes indicating whether or not they are locked.");
            _strongboxFilterPanelRenderer.DrawPanel(embedded: true);
        }

        private void DrawDelveSettings()
        {
            ImGui.Spacing();
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Flares##MechanicsDelveFlares",
                _settings.ClickDelveFlares,
                "Use flares when darkness stacks and health or energy shield thresholds are reached.");
            SettingsUiRenderHelpers.DrawRangeNodeControl(
                "Darkness Debuff Stacks##MechanicsDelveStacks",
                _settings.DarknessDebuffStacks,
                1,
                10,
                "Minimum darkness debuff stacks before a flare can be used.");
            SettingsUiRenderHelpers.DrawRangeNodeControl(
                "Flare Health %##MechanicsDelveHealth",
                _settings.DelveFlareHealthThreshold,
                2,
                100,
                "Health threshold below which ClickIt can use a flare.");
            SettingsUiRenderHelpers.DrawRangeNodeControl(
                "Flare Energy Shield %##MechanicsDelveEnergyShield",
                _settings.DelveFlareEnergyShieldThreshold,
                2,
                100,
                "Energy shield threshold below which ClickIt can use a flare.");
            SettingsUiRenderHelpers.DrawHotkeyNodeControl(
                _settings.DelveFlareHotkey,
                "Flare Hotkey##MechanicsDelveFlareHotkey",
                "Set this to your in-game keybind for flares. The plugin will press this button to use a flare.");
        }

        private void DrawUltimatumSettings()
        {
            ImGui.Spacing();
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Show Option Overlay##MechanicsUltimatumOverlay",
                _settings.ShowUltimatumOptionOverlay,
                "Draws outlines on Ultimatum options: green for the selected option and priority colors for the other options.");

            bool modifiersOpen = ImGui.TreeNode("Modifier Priority##MechanicsUltimatumModifiers");
            if (modifiersOpen)
            {
                _ultimatumSettingsPanelRenderer.DrawModifierTablePanel(embedded: true);
                ImGui.TreePop();
            }

            bool takeRewardOpen = ImGui.TreeNode("Grueling Gauntlet##MechanicsUltimatumTakeReward");
            if (!takeRewardOpen)
                return;

            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Click Take Reward Button##MechanicsUltimatumTakeRewardButton",
                _settings.ClickUltimatumTakeRewardButton,
                "When enabled, ClickIt can press the Take Reward button for Grueling Gauntlet based on your table decisions.");
            _ultimatumSettingsPanelRenderer.DrawTakeRewardModifierTablePanel(embedded: true);
            ImGui.TreePop();
        }

        private void DrawAltarsSettings()
        {
            ImGui.Spacing();
            _altarSettingsPanelRenderer.DrawAltarsPanel(embedded: true);
        }
    }
}