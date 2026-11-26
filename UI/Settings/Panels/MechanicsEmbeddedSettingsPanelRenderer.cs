namespace ClickIt.UI.Settings.Panels
{
    internal sealed class MechanicsEmbeddedSettingsPanelRenderer(
        ClickItSettings settings,
        ItemTypeFiltersPanelRenderer itemTypeFiltersPanelRenderer,
        EssenceCorruptionPanelRenderer essenceCorruptionPanelRenderer,
        StrongboxFilterPanelRenderer strongboxFilterPanelRenderer,
        UltimatumSettingsPanelRenderer ultimatumSettingsPanelRenderer,
        AltarSettingsPanelRenderer altarSettingsPanelRenderer,
        BlightSettingsPanelRenderer blightSettingsPanelRenderer)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly ItemTypeFiltersPanelRenderer _itemTypeFiltersPanelRenderer = itemTypeFiltersPanelRenderer;
        private readonly EssenceCorruptionPanelRenderer _essenceCorruptionPanelRenderer = essenceCorruptionPanelRenderer;
        private readonly StrongboxFilterPanelRenderer _strongboxFilterPanelRenderer = strongboxFilterPanelRenderer;
        private readonly UltimatumSettingsPanelRenderer _ultimatumSettingsPanelRenderer = ultimatumSettingsPanelRenderer;
        private readonly AltarSettingsPanelRenderer _altarSettingsPanelRenderer = altarSettingsPanelRenderer;
        private readonly BlightSettingsPanelRenderer _blightSettingsPanelRenderer = blightSettingsPanelRenderer;

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
                _itemTypeFiltersPanelRenderer.DrawPanel();

            else if (string.Equals(entryId, MechanicIds.Essences, StringComparison.OrdinalIgnoreCase))
            {
                SettingsUiRenderHelpers.DrawToggleNodeControl(
                    "Corrupt ALL Essences##MechanicsEssenceCorruptAll",
                    _settings.CorruptAllEssences,
                    "Overrides the essence table and attempts to corrupt every eligible essence encounter.");
                SettingsUiRenderHelpers.DrawToggleNodeControl(
                    "Only corrupt when there are no Deafening##EssenceSkipIfDeafening",
                    _settings.EssenceCorruptionSkipIfDeafening,
                    "Never corrupt an essence whose label reads Deafening, whether it is the first corruption or a second one.");
                SettingsUiRenderHelpers.DrawToggleNodeControl(
                    "Only corrupt when there are no special essences##EssenceSkipIfSpecialEssence",
                    _settings.EssenceCorruptionSkipIfSpecialEssence,
                    "Never corrupt an essence that is one of the corruption-only essences (Hysteria, Insanity, Horror, Delirium, Desolation, or Trickery).\n\nThis applies to the first corruption and to the second, so a special essence is left alone rather than risked.");

                if (_settings.ShowEssenceCorruptionTablePanel)
                    _essenceCorruptionPanelRenderer.DrawPanel();

            }
            else if (string.Equals(entryId, MechanicIds.Betrayal, StringComparison.OrdinalIgnoreCase))
                SettingsUiRenderHelpers.DrawToggleNodeControl("Corpse Sculpture##BetrayalCorpseSculpture",
                    _settings.ClickBetrayalCorpseSculpture, "Click Corpse Sculptures when Betrayal is enabled in the click table.");
            else if (string.Equals(entryId, MechanicIds.Fates, StringComparison.OrdinalIgnoreCase))
            {
                bool available = _settings.TransientState.RuntimeCache.AutoAuguryPluginAvailable;
                ImGui.BeginDisabled(!available);
                SettingsUiRenderHelpers.DrawToggleNodeControl(
                    "Choose AutoAugury Recommendations##FatesAutoAugury",
                    _settings.ClickRecommendedFates,
                    "Follow AutoAugury's recommended Fates while the click key is active.\nAt a Cauldron, select its recommended Fate. At the boss, place its recommended Fates in order and confirm.\nRequires the AutoAugury plugin to be enabled, available via PluginUpdater.");
                ImGui.EndDisabled();

                if (!available)
                    ImGui.TextDisabled("Requires AutoAugury to be enabled and running.");
            }
            else if (string.Equals(entryId, MechanicIds.Strongboxes, StringComparison.OrdinalIgnoreCase))
                DrawStrongboxSettings();

            else if (string.Equals(entryId, MechanicIds.Harvest, StringComparison.OrdinalIgnoreCase))
            {
                SettingsUiRenderHelpers.DrawToggleNodeControl(
                    "Boss Seeds##HarvestBossSeeds",
                    _settings.HarvestBossSeeds,
                    "Click Harvest boss seeds when Harvest is enabled in the click table.");
                SettingsUiRenderHelpers.DrawToggleNodeControl(
                    "Reroll to Target Specific Modifiers##HarvestRerollTargetMods",
                    _settings.HarvestRerollTargetMods,
                    "Upgrades normal and magic Harvest trees to rare,\nthen rerolls them using your chosen currency.\nStops when any enabled modifier meets its minimum.");
                if (_settings.HarvestRerollTargetMods.Value)
                {
                    ImGui.Indent();
                    int method = Math.Clamp(_settings.HarvestRerollMethod.Value, 0, 1);
                    string[] methods = ["Chaos Orbs", "Scour > Alch"];
                    ImGui.TextUnformatted("Reroll Method");
                    ImGui.SetNextItemWidth(150f);
                    if (ImGui.Combo("##HarvestRerollMethod", ref method, methods, methods.Length))
                        _settings.HarvestRerollMethod.Value = method;
                    SettingsUiRenderHelpers.DrawInlineTooltip(
                        "Keep Scrolls of Wisdom, Orbs of Scouring, and Orbs of Alchemy in your inventory.\nIf using Chaos Orbs to reroll, keep those too; Scouring and Alchemy still prepare magic and normal trees.\nThe plugin rerolls until the tree has one of the enabled modifiers chosen below.");
                    float minimumColumnX = ImGui.GetCursorPosX() + ImGui.GetFrameHeight()
                        + Math.Max(ImGui.CalcTextSize("Stacked Decks").X, ImGui.CalcTextSize("Currency Items").X) + 16f;
                    bool decks = _settings.HarvestTargetStackedDecks.Value;
                    bool threads = _settings.HarvestTargetThreads.Value;
                    bool currency = _settings.HarvestTargetCurrencyItems.Value;
                    bool unique = _settings.HarvestTargetUniqueItems.Value;
                    bool duplicated = _settings.HarvestTargetDuplicatedItems.Value;
                    bool lifeforce = _settings.HarvestTargetLifeforceQuantity.Value;
                    if (!decks && !threads && !currency && !unique && !duplicated && !lifeforce)
                        _settings.HarvestTargetStackedDecks.Value = decks = true;
                    if (ImGui.Checkbox("Stacked Decks##HarvestTargetStackedDecks", ref decks))
                        _settings.HarvestTargetStackedDecks.Value = decks = decks || !(threads || currency || unique || duplicated || lifeforce);
                    ImGui.SameLine(minimumColumnX);
                    DrawHarvestMinimum("##HarvestMinStackedDecks", _settings.HarvestMinStackedDecks, 1, 3);
                    if (ImGui.Checkbox("Threads##HarvestTargetThreads", ref threads))
                        _settings.HarvestTargetThreads.Value = threads = threads || !(decks || currency || unique || duplicated || lifeforce);
                    ImGui.SameLine(minimumColumnX);
                    DrawHarvestMinimum("##HarvestMinThreads", _settings.HarvestMinThreads, 1, 3);
                    if (ImGui.Checkbox("Currency Items##HarvestTargetCurrencyItems", ref currency))
                        _settings.HarvestTargetCurrencyItems.Value = currency = currency || !(decks || threads || unique || duplicated || lifeforce);
                    ImGui.SameLine(minimumColumnX);
                    DrawHarvestMinimum("##HarvestMinCurrencyItems", _settings.HarvestMinCurrencyItems, 1, 4);
                    if (ImGui.Checkbox("Unique Items##HarvestTargetUniqueItems", ref unique))
                        _settings.HarvestTargetUniqueItems.Value = unique = unique || !(decks || threads || currency || duplicated || lifeforce);
                    if (ImGui.Checkbox("Duplicated Items##HarvestTargetDuplicatedItems", ref duplicated))
                        _settings.HarvestTargetDuplicatedItems.Value = duplicated = duplicated || !(decks || threads || currency || unique || lifeforce);
                    if (ImGui.Checkbox("Lifeforce Quantity##HarvestTargetLifeforceQuantity", ref lifeforce))
                        _settings.HarvestTargetLifeforceQuantity.Value = lifeforce = lifeforce || !(decks || threads || currency || unique || duplicated);
                    int minLifeforce = Math.Clamp(_settings.HarvestMinLifeforceQuantity.Value, 50, 150);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(150f);
                    if (ImGui.SliderInt("##HarvestMinLifeforceQuantity", ref minLifeforce, 50, 150, "%d%%")
                        || minLifeforce != _settings.HarvestMinLifeforceQuantity.Value)
                        _settings.HarvestMinLifeforceQuantity.Value = minLifeforce;
                    ImGui.Unindent();
                }
                else
                {
                    SettingsUiRenderHelpers.DrawToggleNodeControl(
                        "Identify > Scour > Alch if Magic##HarvestIdentifyScourAlchMagic",
                        _settings.HarvestIdentifyScourAlchMagic,
                        "When enabled, identify magic Harvest encounters, scour them to normal, then use an Alchemy Orb before opening them. Requires Scrolls of Wisdom, Orbs of Scouring, and Orbs of Alchemy in your inventory.");
                    SettingsUiRenderHelpers.DrawToggleNodeControl(
                        "Alch if Normal##HarvestAlchNormal",
                        _settings.HarvestAlchNormal,
                        "Use an Orb of Alchemy on Normal Harvest encounters before opening them.");
                }
            }

            else if (string.Equals(entryId, MechanicIds.Blight, StringComparison.OrdinalIgnoreCase))
                _blightSettingsPanelRenderer.DrawPanel();


            ImGui.Unindent();
        }

        private static void DrawHarvestMinimum(string id, RangeNode<int> node, int minimum, int maximum)
        {
            int value = Math.Clamp(node.Value, minimum, maximum);
            ImGui.SetNextItemWidth(70f);
            bool changed = ImGui.InputInt(id, ref value, 1, 1);
            value = Math.Clamp(value, minimum, maximum);
            if (changed || value != node.Value)
                node.Value = value;
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
                DrawChestDropSettleSettings(new ChestDropSettleSettingsDescriptor("League Mechanic Chest", "LeagueChests", _settings.PauseAfterOpeningBasicChests, _settings.PauseAfterOpeningBasicChestsInitialDelayMs, _settings.PauseAfterOpeningBasicChestsPollIntervalMs, _settings.PauseAfterOpeningBasicChestsQuietWindowMs));
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
                DrawChestDropSettleSettings(new ChestDropSettleSettingsDescriptor("Heist Chest", "HeistChests", _settings.PauseAfterOpeningBasicChests, _settings.PauseAfterOpeningBasicChestsInitialDelayMs, _settings.PauseAfterOpeningBasicChestsPollIntervalMs, _settings.PauseAfterOpeningBasicChestsQuietWindowMs));

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
            _strongboxFilterPanelRenderer.DrawPanel();
        }

        private void DrawDelveSettings()
        {
            ImGui.Spacing();
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Reveal Full Map##MechanicsDelveRevealMap", _settings.RevealDelveMap,
                "Shows hidden encounters and draws red lines for known connections.");
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Reveal Shortest Route to Targets##MechanicsDelveTargetRoutes", _settings.ShowDelveTargetRoutes,
                "Draws green lines from the closest explored encounter to each selected target along the shortest known route.");
            ImGui.Indent();
            DrawDelveTargets();
            ImGui.Unindent();
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Fractured Walls##MechanicsDelveFracturedWalls", _settings.RevealDelveFracturedWalls,
                "Shows hidden routes behind fractured walls in red when the approach to the wall is already revealed.");

            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Use Flares##MechanicsDelveFlares",
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
            DrawDelveSupplyWarning("Flares", _settings.ShowLowDelveFlaresWarning, _settings.LowDelveFlaresThreshold);
            DrawDelveSupplyWarning("Dynamite", _settings.ShowLowDelveDynamiteWarning, _settings.LowDelveDynamiteThreshold);
        }

        private void DrawDelveTargets()
        {
            bool targetsOpen = ImGui.TreeNode("Targets##MechanicsDelveTargets");
            SettingsUiRenderHelpers.DrawInlineTooltip("Choose which encounters to highlight with green routes.");
            if (!targetsOpen)
                return;

            bool bossOpen = ImGui.TreeNode("Boss##MechanicsDelveBossTargets");
            SettingsUiRenderHelpers.DrawInlineTooltip("Choose which Delve boss encounters to target.");
            if (bossOpen)
            {
                SettingsUiRenderHelpers.DrawToggleNodeControl("Aul", _settings.TargetDelveAul,
                    "Target Aul at the Crystal King's Throne in the Primeval Ruins.");
                SettingsUiRenderHelpers.DrawToggleNodeControl("Lich", _settings.TargetDelveLich,
                    "Target Kurgal at the Lich's Tomb in the Abyssal City.");
                SettingsUiRenderHelpers.DrawToggleNodeControl("Architect", _settings.TargetDelveArchitect,
                    "Target Ahuatotli at the Grand Architect's Temple in the Vaal Outpost.");
                ImGui.TreePop();
            }

            bool fossilsOpen = ImGui.TreeNode("Fossils##MechanicsDelveFossilTargets");
            SettingsUiRenderHelpers.DrawInlineTooltip("Choose which fossil encounters to target, including Smuggler's Stash.");
            if (fossilsOpen)
            {
                SettingsUiRenderHelpers.DrawToggleNodeControl("Crystal Spire", _settings.TargetDelveCrystalSpire,
                    "Drops a Hollow Fossil. Found in the Abyssal Depths.");
                SettingsUiRenderHelpers.DrawToggleNodeControl("Molten Cavity", _settings.TargetDelveMoltenCavity,
                    "Drops a Faceted Fossil. Found in Magma Fissure.");
                SettingsUiRenderHelpers.DrawToggleNodeControl("Humid Fissure", _settings.TargetDelveHumidFissure,
                    "Drops a Fractured Fossil. Found in Sulphur Vents.");
                SettingsUiRenderHelpers.DrawToggleNodeControl("Time-Lost Cavern", _settings.TargetDelveTimeLostCavern,
                    "Drops a Glyphic Fossil. Found in Frozen Hollow.");
                SettingsUiRenderHelpers.DrawToggleNodeControl("Stonewood Hollow", _settings.TargetDelveStonewoodHollow,
                    "Drops a Bloodstained Fossil. Found in Petrified Forest.");
                SettingsUiRenderHelpers.DrawToggleNodeControl("Haunted Tomb", _settings.TargetDelveHauntedTomb,
                    "Drops a Tangled Fossil. Found in Fungal Caverns.");
                SettingsUiRenderHelpers.DrawToggleNodeControl("Smuggler's Stash", _settings.TargetDelveSmugglersStash,
                    "Target the fossil variant of Smuggler's Stash, which contains assorted fossils.");
                ImGui.TreePop();
            }

            SettingsUiRenderHelpers.DrawToggleNodeControl("Minion or Aura Items", _settings.TargetDelveMinionAuraItems,
                "Target encounters that contain items with Delve minion or aura modifiers.");
            ImGui.TreePop();
        }

        private static void DrawDelveSupplyWarning(string supply, ToggleNode enabled, RangeNode<int> threshold)
        {
            bool value = enabled.Value;
            if (ImGui.Checkbox($"Show warning when less than##DelveWarning{supply}", ref value))
                enabled.Value = value;
            ImGui.SameLine();
            int count = Math.Clamp(threshold.Value, 1, 10);
            ImGui.SetNextItemWidth(78f);
            if (ImGui.InputInt($"##DelveWarningThreshold{supply}", ref count, 1, 1))
                threshold.Value = Math.Clamp(count, 1, 10);
            ImGui.SameLine();
            ImGui.TextUnformatted(supply);
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
