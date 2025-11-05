using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Forms;

namespace ClickIt
{
    public class ClickItSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("Enable these if you run into a bug", 900)]
        public EmptyNode EmptyTesting { get; set; } = new EmptyNode();

        [Menu("Debug Mode", "You should only use this if you encounter an issue with the plugin. " +
            "It will flood your screen with debug information that you can provide on the plugin thread to help narrow down the issue.", 1, 900)]
        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);

        [Menu("Additional Debug Information - Render", "Provides more debug text related to rendering the overlay. ", 2, 900)]
        public ToggleNode RenderDebug { get; set; } = new ToggleNode(false);

        [Menu("Report Bug", "If you run into a bug that hasn't already been reported, please report it here.", 3, 900)]
        public ButtonNode ReportBugButton { get; set; } = new ButtonNode();


        [Menu("Accessibility", 1000)]
        public EmptyNode EmptyAccessibility { get; set; } = new EmptyNode();
        [Menu("Left-handed", "Changes the primary mouse button the plugin uses from left to right.", 1, 1000)]
        public ToggleNode LeftHanded { get; set; } = new ToggleNode(false);

        [Menu("General", 3000)]
        public EmptyNode Click { get; set; } = new EmptyNode();

        [Menu("Hotkey", "Held hotkey to start clicking", 1, 3000)]
        [System.Obsolete]
        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);

        [Menu("Search Radius", "Radius the plugin will search in for interactable objects.", 1, 3000)]
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(95, 0, 300);

        [Menu("Items", "Click items", 2, 3000)]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);

        [Menu("Ignore Unique Items", "Ignore unique items", 3, 3000)]
        public ToggleNode IgnoreUniques { get; set; } = new ToggleNode(false);

        //[Menu("Not yet implemented", 2, 3000)]
        //public ToggleNode MouseProximityMode { get; set; } = new ToggleNode(false);
        [Menu("Basic Chests", "Click normal (non-league related) chests", 6, 3000)]
        public ToggleNode ClickBasicChests { get; set; } = new ToggleNode(false);

        [Menu("League Mechanic 'Chests'", "Click league mechanic related 'chests' (blight pustules, legion war hoards / chests, sentinel caches, etc)", 7, 3000)]
        public ToggleNode ClickLeagueChests { get; set; } = new ToggleNode(true);

        [Menu("Shrines", "Click shrines", 8, 3000)]
        public ToggleNode ClickShrines { get; set; } = new ToggleNode(true);

        [Menu("Nearest Harvest", "Click nearest harvest", 9, 3000)]
        public ToggleNode NearestHarvest { get; set; } = new ToggleNode(true);

        [Menu("Area Transitions", "Click area transitions", 10, 3000)]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(false);

        [Menu("Sulphite Veins", "Click sulphite veins", 11, 3000)]
        public ToggleNode ClickSulphiteVeins { get; set; } = new ToggleNode(true);

        [Menu("Azurite in Delve", "Click pure living azurite in the delve mechanic", 12, 3000)]
        public ToggleNode ClickAzuriteVeins { get; set; } = new ToggleNode(true);

        [Menu("Crafting Recipes", "Click crafting recipes", 13, 3000)]
        public ToggleNode ClickCraftingRecipes { get; set; } = new ToggleNode(true);

        [Menu("Breach Nodes", "Click breach nodes", 14, 3000)]
        public ToggleNode ClickBreachNodes { get; set; } = new ToggleNode(false);

        [Menu("Block when Left or Right Panel open", "Prevent clicks when the inventory or character screen are open", 15, 3000)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);

        [Menu("Chest Height Offset", "If you're experiencing a lot of missclicking for chests specifically (clicking too high or low),\n" +
            "change this value. If you're clicking too high, lower the value, if you're clicking too low, raise the value", 16, 3000)]
        public RangeNode<int> ChestHeightOffset { get; set; } = new RangeNode<int>(0, -100, 100);

        [Menu("Block User Input", "Prevents mouse movement and clicks while the hotkey is held. Will help stop missclicking, but may cause issues.", 17, 3000)]
        public ToggleNode BlockUserInput { get; set; } = new ToggleNode(false);

        [Menu("Hide / Show Items occasionally", "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels", 18, 3000)]
        public ToggleNode ToggleItems { get; set; } = new ToggleNode(true);

        [Menu("Toggle Items Hotkey", "Hotkey to toggle the display of ground items / labels", 19, 3000)]
        [System.Obsolete]
        public HotkeyNode ToggleItemsHotkey { get; set; } = new HotkeyNode(Keys.Z);


        [Menu("Essences", 3500)]
        public EmptyNode Essences { get; set; } = new EmptyNode();

        [Menu("Essences", "Click essences", 1, 3500)]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);

        [Menu("Open Inventory Hotkey", "Hotkey to open your inventory", 2, 3500)]
        [System.Obsolete]
        public HotkeyNode OpenInventoryKey { get; set; } = new HotkeyNode(Keys.I);

        [Menu("Corrupt ALL Essences (Warning: This overrides all settings below)", "Corrupt all essences, overriding the settings below.", 3, 3500)]
        public ToggleNode CorruptAllEssences { get; set; } = new ToggleNode(false);

        [Menu("Corrupt Misery, Envy, Dread, Scorn", "Corrupt misery, envy, dread, scorn.", 4, 3500)]
        public ToggleNode CorruptMEDSEssences { get; set; } = new ToggleNode(true);

        [Menu("Corrupt Essences which don't contain a shrieking essence", "Corrupt any essences that don't have a shrieking essence on them.\n\n" +
            "This is for if you use the 'Crystal Resonance' atlas passive, which duplicates monsters when they contain a shrieking essence.", 6, 3500)]
        public ToggleNode CorruptAnyNonShrieking { get; set; } = new ToggleNode(true);

        [JsonIgnore]
        public CustomNode Tribes { get; }

        public ClickItSettings()
        {
            Tribes = new CustomNode
            {
                DrawDelegate = () =>
                {
                    if (ImGui.TreeNode("Altar Upside Mods"))
                    {
                        ImGui.Spacing();
                        ImGui.Spacing();

                        ImGui.TextWrapped("Weight Scale:");
                        DrawWeightScale(bestAtHigh: true);

                        ImGui.Spacing();
                        ImGui.Spacing();

                        if (ImGui.BeginTable("UnitConfig", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
                        {
                            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);
                            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthFixed, 1000);
                            ImGui.TableHeadersRow();
                            foreach ((string id, string name, string type, int defaultValue) in AltarModsConstants.UpsideMods)
                            {
                                ImGui.PushID($"unit{id}");
                                ImGui.TableNextRow(ImGuiTableRowFlags.None);
                                _ = ImGui.TableNextColumn();
                                ImGui.SetNextItemWidth(100);
                                int currentValue = GetModTier(id);
                                if (ImGui.SliderInt($"", ref currentValue, 1, 100))
                                {
                                    ModTiers[id] = currentValue;
                                }
                                ImGui.SetNextItemWidth(1000);
                                _ = ImGui.TableNextColumn();
                                ImGui.Text(name);

                                ImGui.PopID();
                            }

                            ImGui.EndTable();
                        }

                        ImGui.TreePop();
                    }

                    if (ImGui.TreeNode("Altar Downside Mods"))
                    {
                        ImGui.Spacing();
                        ImGui.Spacing();

                        ImGui.TextWrapped("Weight Scale:");
                        DrawWeightScale(bestAtHigh: false); // Red → Green (100 = Worst)

                        ImGui.Spacing();
                        ImGui.Spacing();

                        if (ImGui.BeginTable("UnitConfig", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
                        {
                            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);
                            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthFixed, 1000);
                            ImGui.TableHeadersRow();
                            foreach ((string id, string name, string type, int defaultValue) in AltarModsConstants.DownsideMods)
                            {
                                ImGui.PushID($"unit{id}");
                                ImGui.TableNextRow(ImGuiTableRowFlags.None);
                                _ = ImGui.TableNextColumn();
                                ImGui.SetNextItemWidth(100);
                                int currentValue = GetModTier(id);
                                if (ImGui.SliderInt($"", ref currentValue, 1, 100))
                                {
                                    ModTiers[id] = currentValue;
                                }
                                ImGui.SetNextItemWidth(1000);
                                _ = ImGui.TableNextColumn();
                                ImGui.Text(name);

                                ImGui.PopID();
                            }

                            ImGui.EndTable();
                        }

                        ImGui.TreePop();
                    }
                }
            };

        }

        public int GetModTier(string mod)
        {
            return ModTiers.TryGetValue(mod ?? "", out int value) ? value : 1;
        }

        public Dictionary<string, int> ModTiers = [];


        private static void DrawWeightScale(bool bestAtHigh = true, float width = 400f, float height = 20f)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 p = ImGui.GetCursorScreenPos();

            // Colors (good -> bad). We'll flip which is left/right based on bestAtHigh.
            Vector4 colGood = new(0.2f, 1.0f, 0.2f, 1.0f); // green
            Vector4 colBad = new(1.0f, 0.2f, 0.2f, 1.0f); // red

            uint colLeft = ImGui.GetColorU32(bestAtHigh ? colBad : colGood);
            uint colRight = ImGui.GetColorU32(bestAtHigh ? colGood : colBad);

            Vector2 rectMin = p;
            Vector2 rectMax = new(p.X + width, p.Y + height);

            // Horizontal gradient (efficient)
            drawList.AddRectFilledMultiColor(rectMin, rectMax, colLeft, colRight, colRight, colLeft);

            // Outline
            uint borderCol = ImGui.GetColorU32(ImGuiCol.Border);
            drawList.AddRect(rectMin, rectMax, borderCol);

            // Tick marks and labels
            int steps = 4; // 0,25,50,75,100 -> 4 intervals
            float stepPx = width / steps;

            float tickTop = rectMax.Y;
            float tickBottom = rectMax.Y + 6f;
            float labelY = rectMax.Y + 8f;

            for (int i = 0; i <= steps; i++)
            {
                float x = rectMin.X + (i * stepPx);

                // Tick line
                drawList.AddLine(new Vector2(x, tickTop), new Vector2(x, tickBottom), ImGui.GetColorU32(ImGuiCol.Text), 1.0f);

                // Label text (centered)
                string label = (i * 25).ToString();
                Vector2 textSize = ImGui.CalcTextSize(label);
                Vector2 textPos = new(x - (textSize.X * 0.5f), labelY);
                drawList.AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), label);
            }

            // Optional legend text under the bar (centered)
            string leftLegend = bestAtHigh ? "Worst" : "Best";
            string rightLegend = bestAtHigh ? "Best" : "Worst";
            Vector2 leftLegendSize = ImGui.CalcTextSize(leftLegend);
            Vector2 rightLegendSize = ImGui.CalcTextSize(rightLegend);

            // Place legends near the ends, with a small margin
            float margin = 2f;
            Vector2 leftPos = new(rectMin.X + margin, labelY + leftLegendSize.Y + 4f);
            Vector2 rightPos = new(rectMax.X - rightLegendSize.X - margin, labelY + rightLegendSize.Y + 4f);
            drawList.AddText(leftPos, ImGui.GetColorU32(ImGuiCol.Text), leftLegend);
            drawList.AddText(rightPos, ImGui.GetColorU32(ImGuiCol.Text), rightLegend);

            // Advance layout so following widgets don't overlap the bar/labels
            ImGui.Dummy(new Vector2(width, height + 28f + leftLegendSize.Y));
        }







        [Menu("Searing Exarch", 4000)]
        public EmptyNode ExarchAltar { get; set; } = new EmptyNode();

        [Menu("Click recommended option",
            "Clicks searing exarch altars for you based on a decision tree created from your settings." +
            "\n\nIf both options are as good as each other (according to your weights), this won't click for you.", 1, 4000)]
        public ToggleNode ClickExarchAltars { get; set; } = new ToggleNode(false);

        [Menu("Highlight recommended option",
            "Highlights the recommended option for you to choose for searing exarch altars, based on a decision tree created from your settings below.", 2, 4000)]
        public ToggleNode HighlightExarchAltars { get; set; } = new ToggleNode(true);


        [Menu("Searing Exarch Mod Weights - Downsides (Map Boss)", 5000)]
        public EmptyNode ExarchDownsideMapBossMods { get; set; } = new EmptyNode();

        [Menu("Map Boss:    Create Consecrated Ground on Hit, lasting 6 seconds", 1, 5000)]
        public ToggleNode Exarch_MapBossCreateConsecratedGroundonHit { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 2, 5000)]
        public RangeNode<int> Exarch_MapBossCreateConsecratedGroundonHit_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:    +10% to maximum Fire Resistance\n" +
            "                       +80% to Fire Resistance\n" +
            "                       +10% to maximum Chaos Resistance\n" +
            "                       +80% to Chaos Resistance  ", 3, 5000)]
        public ToggleNode Exarch_MapBosstoMaximumFireResistance { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 4, 5000)]
        public RangeNode<int> Exarch_MapBosstoMaximumFireResistance_Weight
        { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:    +50000 to Armour ", 5, 5000)]
        public ToggleNode Exarch_MapBossToArmour { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 6, 5000)]
        public RangeNode<int> Exarch_MapBossToArmour_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:    Gain (50 to 80)% of Physical Damage as Extra Fire Damage\n" +
            "                       Cover Enemies in Ash on Hit ", 7, 5000)]
        public ToggleNode Exarch_MapBossGainofPhysicalDamageasExtraFire { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 8, 5000)]
        public RangeNode<int> Exarch_MapBossGainofPhysicalDamageasExtraFire_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Map Boss:    Poison on Hit\n" +
            "                       All Damage from Hits can Poison ", 11, 5000)]
        public ToggleNode Exarch_MapBossPoisonOnHit { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 12, 5000)]
        public RangeNode<int> Exarch_MapBossPoisonOnHit_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:    Enemies lose 6 Flask Charges every 3 seconds and cannot gain\n" +
            "                       Flask Charges for 6 seconds after being Hit", 13, 5000)]
        public ToggleNode Exarch_MapBossEnemiesLoseFlaskChargesEverySeconds { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 14, 5000)]
        public RangeNode<int> Exarch_MapBossEnemiesLoseFlaskChargesEverySeconds_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:    Gain (50 to 80)% of Physical Damage as Extra Damage of a random Element\n" +
            "                       Damage Penetrates (15 to 25)% of Enemy Elemental Resistances", 15, 5000)]
        public ToggleNode Exarch_MapBossGainOfPhysicalDamageAsExtraDamageOfARandomElement { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 16, 5000)]
        public RangeNode<int> Exarch_MapBossGainOfPhysicalDamageAsExtraDamageOfARandomElement_Weight { get; set; } = new RangeNode<int>(65, 1, 100);

        [Menu("Map Boss:    Your hits inflict Malediction\n" +
            "                       (10% reduced damage dealt, 10% increased damage taken)", 16, 5000)]
        public ToggleNode Exarch_MapBossYourHitsInflictMalediction { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 17, 5000)]
        public RangeNode<int> Exarch_MapBossYourHitsInflictMalediction_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:    Hits always Ignite\n" +
            "                       Gain (70 to 130)% of Physical Damage as Extra Fire Damage\n" +
            "                       All Damage can Ignite ", 18, 5000)]
        public ToggleNode Exarch_MapBossHitsAlwaysIgnite { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 19, 5000)]
        public RangeNode<int> Exarch_MapBossHitsAlwaysIgnite_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Map Boss:    Gain (70 to 130)% of Physical Damage as Extra Chaos Damage\n" +
            "                       Poison on Hit\n" +
            "                       All Damage from Hits can Poison ", 20, 5000)]
        public ToggleNode Exarch_MapBossGainOfPhysicalDamageAsExtraChaos { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 21, 5000)]
        public RangeNode<int> Exarch_MapBossGainOfPhysicalDamageAsExtraChaos_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Map Boss:    (100 to 200)% increased Armour\n" +
            "                       (100 to 200)% increased Evasion Rating  ", 22, 5000)]
        public ToggleNode Exarch_MapBossIncreasedArmour { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 23, 5000)]
        public RangeNode<int> Exarch_MapBossIncreasedArmour_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:    Nearby Enemies are Hindered, with 40% reduced Movement Speed", 24, 5000)]
        public ToggleNode Exarch_MapBossNearbyEnemiesAreHindered { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 25, 5000)]
        public RangeNode<int> Exarch_MapBossNearbyEnemiesAreHindered_Weight { get; set; } = new RangeNode<int>(40, 1, 100);



        [Menu("Searing Exarch Mod Weights - Downsides (Eldritch Minions)", 6000)]
        public EmptyNode ExarchDownsideMinionsMods { get; set; } = new EmptyNode();

        [Menu("Eldritch Minions:    Drops Burning Ground on Death, lasting 3 seconds ", 1, 6000)]
        public ToggleNode Exarch_EldritchMinionsDropsBurningGroundOnDeath { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 2, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsDropsBurningGroundOnDeath_Weight { get; set; } = new RangeNode<int>(1, 1, 100);

        [Menu("Eldritch Minions:    Create Consecrated Ground on Death, lasting 6 seconds  ", 3, 6000)]
        public ToggleNode Exarch_EldritchMinionsCreateConsecratedGroundOnDeath { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 4, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsCreateConsecratedGroundOnDeath_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:    Gain (70 to 130)% of Physical Damage as Extra Damage of a random Element\n" +
            "                                   Inflict Fire, Cold, and Lightning Exposure on Hit ", 5, 6000)]
        public ToggleNode Exarch_EldritchMinionsGainOfPhysicalDamageAsExtraDamageOfARandomElement { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 6, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsGainOfPhysicalDamageAsExtraDamageOfARandomElement_Weight { get; set; } = new RangeNode<int>(70, 1, 100);

        [Menu("Eldritch Minions:    Enemies lose 6 Flask Charges every 3 seconds and cannot gain\n" +
            "                                   Flask Charges for 6 seconds after being Hit  ", 7, 6000)]
        public ToggleNode Exarch_EldritchMinionsEnemiesLoseFlaskChargesEvery { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 8, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsEnemiesLoseFlaskChargesEvery_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:    +10% to maximum Fire Resistance\n" +
            "                                   +80% to Fire Resistance\n" +
            "                                   +10% to maximum Chaos Resistance\n" +
            "                                   +80% to Chaos Resistance ", 9, 6000)]
        public ToggleNode Exarch_EldritchMinionsToMaximumFireResistance { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 10, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsToMaximumFireResistance_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:    +50000 to Armour", 11, 6000)]
        public ToggleNode Exarch_EldritchMinionsToArmour { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 12, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsToArmour_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:    (70 to 130)% increased Area of Effect ", 13, 6000)]
        public ToggleNode Exarch_EldritchMinionsIncreasedAreaOfEffect { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 14, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsIncreasedAreaOfEffect_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:    (250 to 500)% increased Evasion Rating ", 15, 6000)]
        public ToggleNode Exarch_EldritchMinionsIncreasedEvasionRating { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 16, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsIncreasedEvasionRating_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:    Hits always Ignite\n" +
            "                                   All Damage can Ignite ", 17, 6000)]
        public ToggleNode Exarch_EldritchMinionsHitsAlwaysIgnite { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 18, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsHitsAlwaysIgnite_Weight { get; set; } = new RangeNode<int>(1, 1, 100);

        [Menu("Eldritch Minions:    Poison on Hit\n" +
            "                                   All Damage from Hits can Poison ", 19, 6000)]
        public ToggleNode Exarch_EldritchMinionsPoisonOnHit { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 20, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsPoisonOnHit_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:    Curse Enemies with Vulnerability on Hit\n" +
            "                                   (Take 30% increased Physical Damage)", 21, 6000)]
        public ToggleNode Exarch_EldritchMinionsCurseEnemiesWithVulnerabilityOnHit { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 22, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsCurseEnemiesWithVulnerabilityOnHit_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Eldritch Minions:    Gain (70 to 130)% of Physical Damage as Extra Chaos Damage ", 23, 6000)]
        public ToggleNode Exarch_EldritchMinionsGainOfPhysicalDamageAsExtraChaos { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 24, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsGainOfPhysicalDamageAsExtraChaos_Weight { get; set; } = new RangeNode<int>(65, 1, 100);

        [Menu("Eldritch Minions:    Gain (70 to 130)% of Physical Damage as Extra Fire Damage ", 25, 6000)]
        public ToggleNode Exarch_EldritchMinionsGainOfPhysicalDamageAsExtraFire { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 26, 6000)]
        public RangeNode<int> Exarch_EldritchMinionsGainOfPhysicalDamageAsExtraFire_Weight { get; set; } = new RangeNode<int>(40, 1, 100);



        [Menu("Searing Exarch Mod Weights - Downsides (Player)", 7000)]
        public EmptyNode ExarchDownsidePlayerMods { get; set; } = new EmptyNode();

        [Menu("Player:  (-60 to -40)% to Fire Resistance\n" +
            "               (-60 to -40)% to Chaos Resistance ", 1, 7000)]
        public ToggleNode Exarch_PlayerToFireResistance { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 2, 7000)]
        public RangeNode<int> Exarch_PlayerToFireResistance_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Player:  -3000 to Armour\n" +
            "               -3000 to Evasion Rating  ", 3, 7000)]
        public ToggleNode Exarch_PlayerToArmour { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 4, 7000)]
        public RangeNode<int> Exarch_PlayerToArmour_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Player:  (20 to 40)% increased Flask Charges used\n" +
            "               (40 to60)% reduced Flask Effect Duration ", 5, 7000)]
        public ToggleNode Exarch_PlayerIncreasedFlaskChargesUsed { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 6, 7000)]
        public RangeNode<int> Exarch_PlayerIncreasedFlaskChargesUsed_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Player:  Take 600 Chaos Damage per second during any Flask Effect ", 7, 7000)]
        public ToggleNode Exarch_PlayerTakeChaosDamagePerSecondDuringAnyFlaskEffect { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 8, 7000)]
        public RangeNode<int> Exarch_PlayerTakeChaosDamagePerSecondDuringAnyFlaskEffect_Weight { get; set; } = new RangeNode<int>(100, 1, 100);

        [Menu("Player:  Spell Hits have (20 to 30)% chance to Hinder you ", 9, 7000)]
        public ToggleNode Exarch_PlayerSpellHitsHaveChanceToHinderYou { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 10, 7000)]
        public RangeNode<int> Exarch_PlayerSpellHitsHaveChanceToHinderYou_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Player:  All Damage taken from Hits can Scorch you\n" +
            "               (25 to 35)% chance to be Scorched when Hit ", 11, 7000)]
        public ToggleNode Exarch_PlayerAllDamageTakenFromHitsCanScorchYou { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 12, 7000)]
        public RangeNode<int> Exarch_PlayerAllDamageTakenFromHitsCanScorchYou_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Player:  Curses you inflict are reflected back to you ", 13, 7000)]
        public ToggleNode Exarch_PlayerCursesYouInflictAreReflectedBackToYou { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 14, 7000)]
        public RangeNode<int> Exarch_PlayerCursesYouInflictAreReflectedBackToYou_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Player:  (15 to 20)% chance for Enemies to drop Burning Ground when Hitting you, no more than once every 2 seconds ", 15, 7000)]
        public ToggleNode Exarch_PlayerChanceForEnemiesToDropBurningGroundWhenHittingYou { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 16, 7000)]
        public RangeNode<int> Exarch_PlayerChanceForEnemiesToDropBurningGroundWhenHittingYou_Weight { get; set; } = new RangeNode<int>(1, 1, 100);

        [Menu("Player:  30% chance to be targeted by a Meteor when you use a Flask ", 17, 7000)]
        public ToggleNode Exarch_PlayerChanceToBeTargetedByAMeteor { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 18, 7000)]
        public RangeNode<int> Exarch_PlayerChanceToBeTargetedByAMeteor_Weight { get; set; } = new RangeNode<int>(100, 1, 100);

        [Menu("Player:  Nearby Enemies Gain 100% of their Physical Damage as Extra Fire Damage ", 19, 7000)]
        public ToggleNode Exarch_PlayerNearbyEnemiesGainOfTheirPhysicalDamageAsExtraFire { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 20, 7000)]
        public RangeNode<int> Exarch_PlayerNearbyEnemiesGainOfTheirPhysicalDamageAsExtraFire_Weight { get; set; } = new RangeNode<int>(60, 1, 100);

        [Menu("Player:  Nearby Enemies Gain 100% of their Physical Damage as Extra Chaos Damage ", 21, 7000)]
        public ToggleNode Exarch_PlayerNearbyEnemiesGainOfTheirPhysicalDamageAsExtraChaos { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 22, 7000)]
        public RangeNode<int> Exarch_PlayerNearbyEnemiesGainOfTheirPhysicalDamageAsExtraChaos_Weight { get; set; } = new RangeNode<int>(85, 1, 100);



        [Menu("Searing Exarch Mod Weights - Upsides (Map Boss)", 8000)]
        public EmptyNode ExarchUpsideMapBossMods { get; set; } = new EmptyNode();

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Awakened Sextants ", 3, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalAwakenedSextants { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 4, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalAwakenedSextants_Weight { get; set; } = new RangeNode<int>(40, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Orbs of Binding ", 5, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalOrbsOfBinding { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 6, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalOrbsOfBinding_Weight { get; set; } = new RangeNode<int>(1, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Orbs of Horizons ", 7, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalOrbsOfHorizons { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 8, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalOrbsOfHorizons_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Orbs of Unmaking ", 9, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalOrbsOfUnmaking { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 10, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalOrbsOfUnmaking_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Cartographer's Chisels ", 11, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalCartographer { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 12, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalCartographer_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Lesser Eldritch Embers ", 13, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalLesserEldritchEmbers { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 14, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalLesserEldritchEmbers_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Greater Eldritch Embers ", 15, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGreaterEldritchEmbers { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 16, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGreaterEldritchEmbers_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Grand Eldritch Embers ", 17, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGrandEldritchEmbers { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 18, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGrandEldritchEmbers_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Eldritch Chaos Orbs", 19, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalEldritchChaosOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 20, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalEldritchChaosOrbs_Weight { get; set; } = new RangeNode<int>(85, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Eldritch Exalted Orbs", 21, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalEldritchExaltedOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 22, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalEldritchExaltedOrbs_Weight { get; set; } = new RangeNode<int>(75, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Eldritch Orbs of Annulment", 23, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalEldritchOrbsOfAnnulment { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 24, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalEldritchOrbsOfAnnulment_Weight { get; set; } = new RangeNode<int>(80, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Orbs of Annulment ", 25, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalOrbsOfAnnulment { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 26, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalOrbsOfAnnulment_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Vaal Orbs ", 27, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalVaalOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 28, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalVaalOrbs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Enkindling Orbs ", 29, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalEnkindlingOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 30, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalEnkindlingOrbs_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Instilling Orbs ", 31, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalInstillingOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 32, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalInstillingOrbs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Orbs of Regret ", 33, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalOrbsOfRegret { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 34, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalOrbsOfRegret_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Glassblower's Baubles ", 35, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGlassblowersBaubles { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 36, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGlassblowersBaubles_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gemcutter's Prisms", 35, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGemcuttersPrisms { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 36, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGemcuttersPrisms_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Chaos Orbs", 37, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalChaosOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 38, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalChaosOrbs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Bestiary Scarabs", 39, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalRustedBestiaryScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 40, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalRustedBestiaryScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Bestiary Scarabs", 41, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalPolishedBestiaryScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 42, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalPolishedBestiaryScarabs_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Bestiary Scarabs", 43, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGildedBestiaryScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 44, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGildedBestiaryScarabs_Weight { get; set; } = new RangeNode<int>(40, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Torment Scarabs", 45, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalRustedTormentScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 46, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalRustedTormentScarabs_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Torment Scarabs", 47, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalPolishedTormentScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 48, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalPolishedTormentScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Torment Scarabs", 49, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGildedTormentScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 50, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGildedTormentScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Metamorph Scarabs", 51, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalRustedMetamorphScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 52, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalRustedMetamorphScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Metamorph Scarabs", 53, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalPolishedMetamorphScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 54, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalPolishedMetamorphScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Metamorph Scarabs", 55, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGildedMetamorphScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 56, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGildedMetamorphScarabs_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Blight Scarabs", 57, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalRustedBlightScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 58, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalRustedBlightScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Blight Scarabs", 59, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalPolishedBlightScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 60, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalPolishedBlightScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Blight Scarabs", 61, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGildedBlightScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 62, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGildedBlightScarabs_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Reliquary Scarabs", 63, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalRustedReliquaryScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 64, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalRustedReliquaryScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Reliquary Scarabs", 65, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalPolishedReliquaryScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 66, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalPolishedReliquaryScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Reliquary Scarabs", 67, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGildedReliquaryScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 68, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGildedReliquaryScarabs_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Divination Scarabs", 69, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalRustedDivinationScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 70, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalRustedDivinationScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Divination Scarabs", 71, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalPolishedDivinationScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 72, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalPolishedDivinationScarabs_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Divination Scarabs", 73, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGildedDivinationScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 74, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGildedDivinationScarabs_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Shaper Scarabs", 75, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalRustedShaperScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 76, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalRustedShaperScarabs_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Shaper Scarabs", 77, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalPolishedShaperScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 78, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalPolishedShaperScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Shaper Scarabs", 79, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGildedShaperScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 80, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGildedShaperScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Cartography Scarabs", 81, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalRustedCartographyScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 82, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalRustedCartographyScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Cartography Scarabs", 83, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalPolishedCartographyScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 84, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalPolishedCartographyScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Cartography Scarabs", 85, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalGildedCartographyScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 86, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalGildedCartographyScarabs_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward a Unique Item", 87, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAUniqueItem { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 88, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAUniqueItem_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward a Unique Weapon", 89, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAUniqueWeapon { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 90, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAUniqueWeapon_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward a Unique Armour", 91, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAUniqueArmour { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 92, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAUniqueArmour_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward Unique Jewellery", 93, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardUniqueJewellery { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 94, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardUniqueJewellery_Weight { get; set; } = new RangeNode<int>(60, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward a Corrupted Unique Item", 95, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardACorruptedUniqueItem { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 96, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardACorruptedUniqueItem_Weight { get; set; } = new RangeNode<int>(40, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward a Map", 97, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAMap { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 98, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAMap_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward a Unique Map", 99, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAUniqueMap { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 100, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardAUniqueMap_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward a Corrupted Item", 101, 8000)]
        public ToggleNode Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardACorruptedItem { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 102, 8000)]
        public RangeNode<int> Exarch_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardACorruptedItem_Weight { get; set; } = new RangeNode<int>(20, 1, 100);



        [Menu("Searing Exarch Mod Weights - Upsides (Eldritch Minions)", 9000)]
        public EmptyNode ExarchUpsideEldritchMinionsMods { get; set; } = new EmptyNode();

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divine Orb ", 1, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivineOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 2, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivineOrb_Weight { get; set; } = new RangeNode<int>(100, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Awakened Sextant ", 3, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalAwakenedSextant { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 4, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalAwakenedSextant_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Orb of Binding ", 5, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfBinding { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 6, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfBinding_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Orb of Horizons ", 7, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfHorizons { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 8, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfHorizons_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Orb of Unmaking ", 9, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfUnmaking { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 10, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfUnmaking_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Cartographer's Chisel ", 11, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalCartographersChisel { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 12, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalCartographersChisel_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Lesser Eldritch Ember ", 13, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalLesserEldritchEmber { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 14, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalLesserEldritchEmber_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Greater Eldritch Ember ", 15, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGreaterEldritchEmber { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 16, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGreaterEldritchEmber_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Grand Eldritch Ember ", 17, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGrandEldritchEmber { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 18, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGrandEldritchEmber_Weight { get; set; } = new RangeNode<int>(45, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Eldritch Chaos Orb ", 19, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalEldritchChaosOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 20, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalEldritchChaosOrb_Weight { get; set; } = new RangeNode<int>(97, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Eldritch Exalted Orb ", 21, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalEldritchExaltedOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 22, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalEldritchExaltedOrb_Weight { get; set; } = new RangeNode<int>(92, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Eldritch Orb of Annulment ", 23, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalEldritchOrbOfAnnulment { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 24, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalEldritchOrbOfAnnulment_Weight { get; set; } = new RangeNode<int>(94, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Orb of Annulment ", 25, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfAnnulment { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 26, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfAnnulment_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Vaal Orb ", 27, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalVaalOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 28, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalVaalOrb_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Enkindling Orb ", 29, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalEnkindlingOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 30, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalEnkindlingOrb_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Instilling Orb ", 31, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalInstillingOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 32, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalInstillingOrb_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Orb of Regret ", 33, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfRegret { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 34, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalOrbOfRegret_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Glassblower's Bauble ", 35, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGlassblowersBauble { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 36, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGlassblowersBauble_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gemcutter's Prism ", 37, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGemcuttersPrism { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 38, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGemcuttersPrism_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Chaos Orb ", 39, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalChaosOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 40, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalChaosOrb_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Breach Scarab ", 41, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalRustedBreachScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 42, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalRustedBreachScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Breach Scarab ", 43, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedBreachScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 44, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedBreachScarab_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Breach Scarab ", 45, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGildedBreachScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 46, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGildedBreachScarab_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Torment Scarab ", 47, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalRustedTormentScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 48, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalRustedTormentScarab_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Torment Scarab ", 49, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedTormentScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 50, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedTormentScarab_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Torment Scarab ", 51, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGildedTormentScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 52, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGildedTormentScarab_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Metamorph Scarab ", 53, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalRustedMetamorphScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 54, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalRustedMetamorphScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Metamorph Scarab ", 55, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedMetamorphScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 56, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedMetamorphScarab_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Metamorph Scarab ", 57, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGildedMetamorphScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 58, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGildedMetamorphScarab_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Blight Scarab ", 59, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalRustedBlightScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 60, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalRustedBlightScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Blight Scarab ", 61, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedBlightScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 62, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedBlightScarab_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Blight Scarab ", 63, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGildedBlightScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 64, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGildedBlightScarab_Weight { get; set; } = new RangeNode<int>(40, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Reliquary Scarab ", 65, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalRustedReliquaryScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 66, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalRustedReliquaryScarab_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Reliquary Scarab ", 67, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedReliquaryScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 68, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedReliquaryScarab_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Reliquary Scarab ", 69, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGildedReliquaryScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 70, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGildedReliquaryScarab_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Divination Scarab ", 71, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalRustedDivinationScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 72, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalRustedDivinationScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Divination Scarab ", 73, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedDivinationScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 74, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedDivinationScarab_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Divination Scarab ", 75, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGildedDivinationScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 76, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGildedDivinationScarab_Weight { get; set; } = new RangeNode<int>(45, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Shaper Scarab ", 77, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalRustedShaperScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 78, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalRustedShaperScarab_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Shaper Scarab ", 79, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedShaperScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 80, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedShaperScarab_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Shaper Scarab ", 81, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGildedShaperScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 82, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGildedShaperScarab_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Cartography Scarab ", 83, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalRustedCartographyScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 84, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalRustedCartographyScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Cartography Scarab ", 85, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedCartographyScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 86, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalPolishedCartographyScarab_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Cartography Scarab ", 87, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalGildedCartographyScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 88, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalGildedCartographyScarab_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards a Unique Item ", 89, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAUniqueItem { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 90, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAUniqueItem_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards a Unique Weapon ", 91, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAUniqueWeapon { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 92, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAUniqueWeapon_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards a Unique Armour ", 93, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAUniqueArmour { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 94, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAUniqueArmour_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards Unique Jewellery ", 95, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsUniqueJewellery { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 96, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsUniqueJewellery_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards a Corrupted Unique Item ", 97, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsACorruptedUniqueItem { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 98, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsACorruptedUniqueItem_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards a Map ", 99, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAMap { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 100, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAMap_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards a Unique Map ", 101, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAUniqueMap { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 102, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsAUniqueMap_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards a Corrupted Item ", 103, 9000)]
        public ToggleNode Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsACorruptedItem { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 104, 9000)]
        public RangeNode<int> Exarch_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsACorruptedItem_Weight { get; set; } = new RangeNode<int>(15, 1, 100);



        [Menu("Searing Exarch Mod Weights - Upsides (Player)", 10000)]
        public EmptyNode ExarchUpsidePlayerMods { get; set; } = new EmptyNode();

        [Menu("Player:  	Unique Items dropped by slain Enemies have (15 to 30)% chance to be Duplicated  ", 1, 10000)]
        public ToggleNode Exarch_PlayerUniqueItemsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 2, 10000)]
        public RangeNode<int> Exarch_PlayerUniqueItemsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Player:  	Scarabs dropped by slain Enemies have (15 to 30)% chance to be Duplicated   ", 3, 10000)]
        public ToggleNode Exarch_PlayerScarabsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 4, 10000)]
        public RangeNode<int> Exarch_PlayerScarabsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Player:  	Maps dropped by slain Enemies have (15 to 30)% chance to be Duplicated   ", 5, 10000)]
        public ToggleNode Exarch_PlayerMapsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 6, 10000)]
        public RangeNode<int> Exarch_PlayerMapsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Player:  	Divination Cards dropped by slain Enemies have (15 to 30)% chance to be Duplicated   ", 5, 10000)]
        public ToggleNode Exarch_PlayerDivinationCardsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 6, 10000)]
        public RangeNode<int> Exarch_PlayerDivinationCardsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Player:  	(10 to 30)% increased Quantity of Items found in this Area\n" +
            "                   (15 to 35)% increased Rarity of Items found in this Area ", 7, 10000)]
        public ToggleNode Exarch_PlayerIncreasedQuantityOfItemsFoundInThisArea { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 8, 10000)]
        public RangeNode<int> Exarch_PlayerIncreasedQuantityOfItemsFoundInThisArea_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Player:  	Basic Currency Items dropped by slain Enemies have (10 to 15)% chance to be Duplicated", 9, 10000)]
        public ToggleNode Exarch_PlayerBasicCurrencyItemsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 10, 10000)]
        public RangeNode<int> Exarch_PlayerBasicCurrencyItemsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Player:  	Gems dropped by slain Enemies have (10 to 15)% chance to be Duplicated ", 11, 10000)]
        public ToggleNode Exarch_PlayerGemsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 12, 10000)]
        public RangeNode<int> Exarch_PlayerGemsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(1, 1, 100);



        [Menu("Eater of Worlds", 11000)]
        public EmptyNode EaterAltar { get; set; } = new EmptyNode();

        [Menu("Click recommended option",
            "Clicks searing exarch altars for you based on a decision tree created from your settings." +
            "\n\nIf both options are as good as each other (according to your weights), this won't click for you.", 1, 11000)]
        public ToggleNode ClickEaterAltars { get; set; } = new ToggleNode(false);

        [Menu("Highlight recommended option",
            "Highlights the recommended option for you to choose for searing exarch altars, based on a decision tree created from your settings below.", 2, 11000)]
        public ToggleNode HighlightEaterAltars { get; set; } = new ToggleNode(true);


        [Menu("Eater of Worlds Mod Weights - Downsides (Map Boss)", 12000)]
        public EmptyNode EaterDownsideMapBossMods { get; set; } = new EmptyNode();

        [Menu("Map Boss:    +10% to maximum Cold Resistance\n" +
            "                       +80% to Cold Resistance\n" +
            "                       +10% to maximum Lightning Resistance\n" +
            "                       +80% to Lightning Resistance ", 3, 12000)]
        public ToggleNode Eater_MapBosstoMaximumColdResistance
        { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 4, 12000)]
        public RangeNode<int> Eater_MapBosstoMaximumColdResistance_Weight
        { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:    (50 to 70)% additional Physical Damage Reduction ", 5, 12000)]
        public ToggleNode Eater_MapBossAdditionalPhysicalDamageReduction { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 6, 12000)]
        public RangeNode<int> Eater_MapBossAdditionalPhysicalDamageReduction_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:    All Damage with Hits can Chill ", 7, 12000)]
        public ToggleNode Eater_MapBossAllDamageWtihHitsCanChill { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 8, 12000)]
        public RangeNode<int> Eater_MapBossAllDamageWtihHitsCanChill_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:    Hits always Shock\n" +
            "                       Gain (70 to 130)% of Physical Damage as Extra Lightning Damage\n" +
            "                       All Damage can Shock ", 9, 12000)]
        public ToggleNode Eater_MapBossHitsAlwaysShock { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 10, 12000)]
        public RangeNode<int> Eater_MapBossHitsAlwaysShock_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Map Boss:    Prevent +(20 to 30)% of Suppressed Spell Damage\n" +
            "                       +100% chance to Suppress Spell Damage ", 11, 12000)]
        public ToggleNode Eater_MapBossPreventOfSuppressedSpellDamage { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 12, 12000)]
        public RangeNode<int> Eater_MapBossPreventOfSuppressedSpellDamage_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:    Gain (50–80)% of Physical Damage as Extra Damage of a random Element\n" +
            "                       Damage Penetrates (15–25)% of Enemy Elemental Resistances ", 13, 12000)]
        public ToggleNode Eater_MapBossGainOfPhysicalDamageAsExtraDamageOfARandomElement { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 14, 12000)]
        public RangeNode<int> Eater_MapBossGainOfPhysicalDamageAsExtraDamageOfARandomElement_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:    Gain (50 to 130)% of Physical Damage as Extra Cold Damage\n" +
            "                       Cover Enemies in Frost on Hit ", 15, 12000)]
        public ToggleNode Eater_MapBossGainOfPhysicalDamageAsExtraColdDamage { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 16, 12000)]
        public RangeNode<int> Eater_MapBossGainOfPhysicalDamageAsExtraColdDamage_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:    100% Global chance to Blind Enemies on hit\n" +
            "                       (100 to 200)% increased Blind Effect ", 16, 12000)]
        public ToggleNode Eater_MapBossGlobalChanceToBlindEnemiesOnHit { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 17, 12000)]
        public RangeNode<int> Eater_MapBossGlobalChanceToBlindEnemiesOnHit_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:    Gain (50 to 70)% of Maximum Life as Extra Maximum Energy Shield ", 18, 12000)]
        public ToggleNode Eater_MapBossGainOfMaximumLifeAsExtraMaximumEnergyShield { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 19, 12000)]
        public RangeNode<int> Eater_MapBossGainOfMaximumLifeAsExtraMaximumEnergyShield_Weight { get; set; } = new RangeNode<int>(40, 1, 100);

        [Menu("Map Boss:    Eldritch Tentacles", 20, 12000)]
        public ToggleNode Eater_MapBossEldritchTentacles { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 21, 12000)]
        public RangeNode<int> Eater_MapBossEldritchTentacles_Weight { get; set; } = new RangeNode<int>(40, 1, 100);




        [Menu("Eater of Worlds Mod Weights - Downsides (Eldritch Minions)", 13000)]
        public EmptyNode EaterDownsideMinionsMods { get; set; } = new EmptyNode();

        [Menu("Eldritch Minions:    Overwhelm (50 to 80)% Physical Damage Reduction ", 1, 13000)]
        public ToggleNode Eater_EldritchMinionsOverwhelmPhysicalDamageReduction { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 2, 13000)]
        public RangeNode<int> Eater_EldritchMinionsOverwhelmPhysicalDamageReduction_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:    Skills fire (3 to 5) additional Projectiles ", 3, 13000)]
        public ToggleNode Eater_EldritchMinionsSkillsFireAdditionalProjectiles { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 4, 13000)]
        public RangeNode<int> Eater_EldritchMinionsSkillsFireAdditionalProjectiles_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Eldritch Minions:    (30 to 50)% increased Attack Speed\n" +
            "                               (30 to 50)% increased Cast Speed\n" +
            "                               (30 to 50)% increased Movement Speed ", 5, 13000)]
        public ToggleNode Eater_EldritchMinionsIncreasedAttackSpeed { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 6, 13000)]
        public RangeNode<int> Eater_EldritchMinionsIncreasedAttackSpeed_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:    +10% to maximum Cold Resistance\n" +
            "                               +80% to Cold Resistance\n" +
            "                               +10% to maximum Lightning Resistance\n" +
            "                               +80% to Lightning Resistance ", 7, 13000)]
        public ToggleNode Eater_EldritchMinionsToMaximumColdResistance { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 8, 13000)]
        public RangeNode<int> Eater_EldritchMinionsToMaximumColdResistance_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:    (50 to 80)% additional Physical Damage Reduction ", 9, 13000)]
        public ToggleNode Eater_EldritchMinionsAdditionalPhysicalDamageReduction { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 10, 13000)]
        public RangeNode<int> Eater_EldritchMinionsAdditionalPhysicalDamageReduction_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:    Prevent +(20 to 30)% of Suppressed Spell Damage\n" +
            "                               +100% chance to Suppress Spell Damage ", 11, 13000)]
        public ToggleNode Eater_EldritchMinionsPreventOfSuppressedSpellDamage { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 12, 13000)]
        public RangeNode<int> Eater_EldritchMinionsPreventOfSuppressedSpellDamage_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:    100% chance to remove a random Charge from Enemy on Hit ", 13, 13000)]
        public ToggleNode Eater_EldritchMinionsChanceToRemoveARandomChargeFromEnemyOnHit { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 14, 13000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToRemoveARandomChargeFromEnemyOnHit_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:    Drops Chilled Ground on Death, lasting 3 seconds ", 15, 13000)]
        public ToggleNode Eater_EldritchMinionsDropsChilledGroundOnDeath { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 16, 13000)]
        public RangeNode<int> Eater_EldritchMinionsDropsChilledGroundOnDeath_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:    100% chance to create Shocked Ground on Death, lasting 3 seconds ", 17, 13000)]
        public ToggleNode Eater_EldritchMinionsChanceToCreateShockedGroundOnDeath { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 18, 13000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToCreateShockedGroundOnDeath_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:    Inflict 1 Grasping Vine on Hit ", 19, 13000)]
        public ToggleNode Eater_EldritchMinionsInflictGraspingVineOnHit { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 20, 13000)]
        public RangeNode<int> Eater_EldritchMinionsInflictGraspingVineOnHit_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:    Curse Enemies with Punishment on Hit ", 21, 13000)]
        public ToggleNode Eater_EldritchMinionsCurseEnemiesWithPunishmentOnHit { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 22, 13000)]
        public RangeNode<int> Eater_EldritchMinionsCurseEnemiesWithPunishmentOnHit_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:    Gain (70 to 130)% of Physical Damage as Extra Lightning Damage ", 23, 13000)]
        public ToggleNode Eater_EldritchMinionsGainOfPhysicalDamageAsExtraLightning { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 24, 13000)]
        public RangeNode<int> Eater_EldritchMinionsGainOfPhysicalDamageAsExtraLightning_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Eldritch Minions:    Gain (70 to 130)% of Physical Damage as Extra Cold Damage ", 25, 13000)]
        public ToggleNode Eater_EldritchMinionsGainOfPhysicalDamageAsExtraCold { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 26, 13000)]
        public RangeNode<int> Eater_EldritchMinionsGainOfPhysicalDamageAsExtraCold_Weight { get; set; } = new RangeNode<int>(40, 1, 100);



        [Menu("Eater of Worlds Mod Weights - Downsides (Player)", 14000)]
        public EmptyNode EaterDownsidePlayerMods { get; set; } = new EmptyNode();

        [Menu("Player:  (-60 to -40)% to Cold Resistance\n" +
            "               (-60 to -40)% to Lightning Resistance ", 1, 14000)]
        public ToggleNode Eater_PlayerToColdResistance { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 2, 14000)]
        public RangeNode<int> Eater_PlayerToColdResistance_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Player:  (-60 to -40)% additional Physical Damage Reduction ", 3, 14000)]
        public ToggleNode Eater_PlayerAdditionalPhysicalDamageReduction { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 4, 14000)]
        public RangeNode<int> Eater_PlayerAdditionalPhysicalDamageReduction_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Player:  (30 to 50)% reduced Defences per Frenzy Charge ", 5, 14000)]
        public ToggleNode Eater_PlayerReducedDefencesPerFrenzyCharge { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 6, 14000)]
        public RangeNode<int> Eater_PlayerReducedDefencesPerFrenzyCharge_Weight { get; set; } = new RangeNode<int>(40, 1, 100);

        [Menu("Player:  (10 to 20)% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge ", 7, 14000)]
        public ToggleNode Eater_PlayerReducedRecoveryRateOfLifeManaAndEnergyShieldPerEnduranceCharge { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 8, 14000)]
        public RangeNode<int> Eater_PlayerReducedRecoveryRateOfLifeManaAndEnergyShieldPerEnduranceCharge_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Player:  (-40 to -20)% to Critical Strike Multiplier per Power Charge  ", 9, 14000)]
        public ToggleNode Eater_PlayertoCriticalStrikeMultiplierPerPowerCharge { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 10, 14000)]
        public RangeNode<int> Eater_PlayerReducedCooldownRecoveryRatePerPowerCharge_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Player:  (25 to 35)% chance for Enemies to drop Chilled Ground when Hitting you, no more than once every 2 seconds ", 9, 14000)]
        public ToggleNode Eater_PlayerChanceForEnemiesToDropChilledGroundWhenHittingYou { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 11, 14000)]
        public RangeNode<int> Eater_PlayerChanceForEnemiesToDropChilledGroundWhenHittingYou_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Player:  (25 to 35)% chance for Enemies to drop Shocked Ground when Hitting you, no more than once every 2 seconds ", 12, 14000)]
        public ToggleNode Eater_PlayerChanceForEnemiesToDropShockedGroundWhenHittingYou { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 13, 14000)]
        public RangeNode<int> Eater_PlayerChanceForEnemiesToDropShockedGroundWhenHittingYou_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Player:  All Damage taken from Hits can Sap you\n" +
            "               (25 to 35)% chance to be Sapped when Hit ", 14, 14000)]
        public ToggleNode Eater_PlayerAllDamageTakenFromHitsCanSapYou { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 15, 14000)]
        public RangeNode<int> Eater_PlayerAllDamageTakenFromHitsCanSapYou_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Player:  Nearby Enemies Gain 100% of their Physical Damage as Extra Cold Damage ", 16, 14000)]
        public ToggleNode Eater_PlayerNearbyEnemiesGainOfTheirPhysicalDamageAsExtraCold { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 17, 14000)]
        public RangeNode<int> Eater_PlayerNearbyEnemiesGainOfTheirPhysicalDamageAsExtraCold_Weight { get; set; } = new RangeNode<int>(60, 1, 100);

        [Menu("Player:  Nearby Enemies Gain 100% of their Physical Damage as Extra Lightning Damage ", 18, 14000)]
        public ToggleNode Eater_PlayerNearbyEnemiesGainOfTheirPhysicalDamageAsExtraLightning { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 19, 14000)]
        public RangeNode<int> Eater_PlayerNearbyEnemiesGainOfTheirPhysicalDamageAsExtraLightning_Weight { get; set; } = new RangeNode<int>(60, 1, 100);

        [Menu("Player:  Projectiles are fired in random directions ", 20, 14000)]
        public ToggleNode Eater_PlayerProjectilesAreFiredInRandomDirections { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 21, 14000)]
        public RangeNode<int> Eater_PlayerProjectilesAreFiredInRandomDirections_Weight { get; set; } = new RangeNode<int>(100, 1, 100);

        [Menu("Player:  Spell Hits have (25 to 35)% chance to Hinder you ", 22, 14000)]
        public ToggleNode Eater_PlayerSpellHitsHaveChanceToHinder { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 23, 14000)]
        public RangeNode<int> Eater_PlayerSpellHitsHaveChanceToHinder_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Player:  Non-Damaging Ailments you inflict are reflected back to you ", 24, 14000)]
        public ToggleNode Eater_PlayerNonDamagingAilmentsYouInflictAreReflected { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 25, 14000)]
        public RangeNode<int> Eater_PlayerNonDamagingAilmentsYouInflictAreReflected_Weight { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Player:  Number of grasping vines to gain every second while stationary", 26, 14000)]
        public ToggleNode Eater_PlayerNumberOfGraspingVinesToGainEverySecondWhileStationary { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How detrimental is this mod to your build?\n\n" +
            "If you cannot do this mod, set this to 100.\n\n" +
            "If the mod is very difficult, but still doable, a value of 75 would be good.\n\n" +
            "If the mod doesn't affect your build at all, set the weight to 1.", 27, 14000)]
        public RangeNode<int> Eater_PlayerNumberOfGraspingVinesToGainEverySecondWhileStationary_Weight { get; set; } = new RangeNode<int>(100, 1, 100);



        [Menu("Eater of Worlds Mod Weights - Upsides (Map Boss)", 15000)]
        public EmptyNode EaterUpsideMapBossMods { get; set; } = new EmptyNode();

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divine Orbs ", 1, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalDivineOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 2, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalDivineOrbs_Weight { get; set; } = new RangeNode<int>(100, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Exalted Orbs", 3, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalExaltedOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 4, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalExaltedOrbs_Weight { get; set; } = new RangeNode<int>(75, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Regal Orbs", 5, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRegalOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 6, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRegalOrbs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Veiled Chaos Orbs", 7, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalVeiledChaosOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 8, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalVeiledChaosOrbs_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Orbs of Alteration", 9, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalOrbsOfAlteration { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 10, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalOrbsOfAlteration_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Blessed Orbs", 11, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalBlessedOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 12, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalBlessedOrbs_Weight { get; set; } = new RangeNode<int>(12, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Lesser Eldritch Ichors ", 13, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalLesserEldritchIchors { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 14, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalLesserEldritchIchors_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Greater Eldritch Ichors ", 15, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGreaterEldritchIchors { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 16, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGreaterEldritchIchors_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Grand Eldritch Ichors ", 17, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGrandEldritchIchors { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 18, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGrandEldritchIchors_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Eldritch Chaos Orbs", 19, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalEldritchChaosOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 20, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalEldritchChaosOrbs_Weight { get; set; } = new RangeNode<int>(85, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Eldritch Exalted Orbs", 21, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalEldritchExaltedOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 22, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalEldritchExaltedOrbs_Weight { get; set; } = new RangeNode<int>(75, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Eldritch Orbs of Annulment", 23, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalEldritchOrbsOfAnnulment { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 24, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalEldritchOrbsOfAnnulment_Weight { get; set; } = new RangeNode<int>(80, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Orbs of Scouring", 25, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalOrbsOfScouring { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 26, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalOrbsOfScouring_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Chromatic Orbs", 27, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalChromaticOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 28, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalChromaticOrbs_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Orbs of Fusing", 29, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalOrbsOfFusing { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 30, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalOrbsOfFusing_Weight { get; set; } = new RangeNode<int>(7, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Jeweller's Orbs", 31, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalJewellersOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 32, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalJewellersOrbs_Weight { get; set; } = new RangeNode<int>(1, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Chaos Orbs", 37, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalChaosOrbs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 38, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalChaosOrbs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Breach Scarabs", 39, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRustedBreachScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 40, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRustedBreachScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Breach Scarabs", 41, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalPolishedBreachScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 42, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalPolishedBreachScarabs_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Breach Scarabs", 43, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGildedBreachScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 44, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGildedBreachScarabs_Weight { get; set; } = new RangeNode<int>(40, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Elder Scarabs", 45, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRustedElderScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 46, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRustedElderScarabs_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Elder Scarabs", 47, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalPolishedElderScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 48, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalPolishedElderScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Elder Scarabs", 49, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGildedElderScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 50, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGildedElderScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Sulphite Scarabs", 51, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRustedSulphiteScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 52, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRustedSulphiteScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Sulphite Scarabs", 53, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalPolishedSulphiteScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 54, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalPolishedSulphiteScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Sulphite Scarabs", 55, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGildedSulphiteScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 56, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGildedSulphiteScarabs_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Ambush Scarabs", 57, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRustedAmbushScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 58, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRustedAmbushScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Ambush Scarabs", 59, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalPolishedAmbushScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 60, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalPolishedAmbushScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Ambush Scarabs", 61, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGildedAmbushScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 62, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGildedAmbushScarabs_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Harbinger Scarabs", 63, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRustedHarbingerScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 64, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRustedHarbingerScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Harbinger Scarabs", 65, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalPolishedHarbingerScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 66, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalPolishedHarbingerScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Harbinger Scarabs", 67, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGildedHarbingerScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 68, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGildedHarbingerScarabs_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Expedition Scarabs", 69, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRustedExpeditionScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 70, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRustedExpeditionScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Expedition Scarabs", 71, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalPolishedExpeditionScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 72, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalPolishedExpeditionScarabs_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Expedition Scarabs", 73, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGildedExpeditionScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 74, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGildedExpeditionScarabs_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Legion Scarabs", 75, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRustedLegionScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 76, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRustedLegionScarabs_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Legion Scarabs", 77, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalPolishedLegionScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 78, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalPolishedLegionScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Legion Scarabs", 79, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGildedLegionScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 80, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGildedLegionScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Rusted Abyss Scarabs", 81, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalRustedAbyssScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 82, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalRustedAbyssScarabs_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Polished Abyss Scarabs", 83, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalPolishedAbyssScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 84, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalPolishedAbyssScarabs_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Gilded Abyss Scarabs", 85, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalGildedAbyssScarabs { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 86, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalGildedAbyssScarabs_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward Currency", 87, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardCurrency { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 88, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardCurrency_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward Basic Currency", 89, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardBasicCurrency { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 90, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardBasicCurrency_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward League Currency", 91, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardLeagueCurrency { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 92, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardLeagueCurrency_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward other Divination Cards", 93, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardOtherDivinationCards { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 94, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardOtherDivinationCards_Weight { get; set; } = new RangeNode<int>(100, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward Gems", 95, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardGems { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 96, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardGems_Weight { get; set; } = new RangeNode<int>(1, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward Levelled Gems", 97, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardLevelledGems { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 98, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardLevelledGems_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Map Boss:  Final Boss drops (2 to 4) additional Divination Cards which reward Quality Gems", 99, 15000)]
        public ToggleNode Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardQualityGems { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 100, 15000)]
        public RangeNode<int> Eater_MapBossFinalBossDropsAdditionalDivinationCardsWhichRewardQualityGems_Weight { get; set; } = new RangeNode<int>(5, 1, 100);



        [Menu("Eater of Worlds Mod Weights - Upsides (Eldritch Minions)", 16000)]
        public EmptyNode EaterUpsideEldritchMinionsMods { get; set; } = new EmptyNode();

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divine Orb ", 1, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalDivineOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 2, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalDivineOrb_Weight { get; set; } = new RangeNode<int>(100, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Exalted Orb", 3, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalExaltedOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 4, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalExaltedOrb_Weight { get; set; } = new RangeNode<int>(75, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Regal Orb", 5, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRegalOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 6, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRegalOrb_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Veiled Chaos Orb", 7, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalVeiledChaosOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 8, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalVeiledChaosOrb_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Orb of Alteration", 9, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalOrbOfAlteration { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 10, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalOrbOfAlteration_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Blessed Orb ", 11, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalBlessedOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 12, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalBlessedOrb_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Lesser Eldritch Ichor ", 13, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalLesserEldritchIchor { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 14, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalLesserEldritchIchor_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Greater Eldritch Ichor ", 15, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGreaterEldritchIchor { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 16, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGreaterEldritchIchor_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Grand Eldritch Ichor ", 17, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGrandEldritchIchor { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 18, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGrandEldritchIchor_Weight { get; set; } = new RangeNode<int>(45, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Eldritch Chaos Orb ", 19, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalEldritchChaosOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 20, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalEldritchChaosOrb_Weight { get; set; } = new RangeNode<int>(97, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Eldritch Exalted Orb ", 21, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalEldritchExaltedOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 22, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalEldritchExaltedOrb_Weight { get; set; } = new RangeNode<int>(92, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Eldritch Orb of Annulment ", 23, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalEldritchOrbOfAnnulment { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 24, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalEldritchOrbOfAnnulment_Weight { get; set; } = new RangeNode<int>(94, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Orb of Scouring", 25, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalOrbOfScouring { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 26, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalOrbOfScouring_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Chromatic Orb", 27, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalChromaticOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 28, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalChromaticOrb_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Orb of Fusing", 29, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalOrbOfFusing { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 30, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalOrbOfFusing_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Jeweller's Orb ", 31, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalJewellersOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 32, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalJewellersOrb_Weight { get; set; } = new RangeNode<int>(3, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Chaos Orb ", 37, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalChaosOrb { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 38, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalChaosOrb_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Breach Scarab ", 39, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRustedBreachScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 40, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRustedBreachScarab_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Breach Scarab ", 41, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalPolishedBreachScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 42, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalPolishedBreachScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Breach Scarab ", 43, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGildedBreachScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 44, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGildedBreachScarab_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Elder Scarab ", 45, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRustedElderScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 46, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRustedElderScarab_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Elder Scarab ", 47, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalPolishedElderScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 48, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalPolishedElderScarab_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Elder Scarab ", 49, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGildedElderScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 50, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGildedElderScarab_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Sulphite Scarab ", 51, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRustedSulphiteScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 52, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRustedSulphiteScarab_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Sulphite Scarab ", 53, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalPolishedSulphiteScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 54, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalPolishedSulphiteScarab_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Sulphite Scarab ", 55, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGildedSulphiteScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 56, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGildedSulphiteScarab_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Ambush Scarab ", 57, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRustedAmbushScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 58, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRustedAmbushScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Ambush Scarab ", 59, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalPolishedAmbushScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 60, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalPolishedAmbushScarab_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Ambush Scarab ", 61, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGildedAmbushScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 62, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGildedBlightScarab_Weight { get; set; } = new RangeNode<int>(40, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Harbinger Scarab ", 63, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRustedHarbingerScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 64, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRustedHarbingerScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Harbinger Scarab ", 65, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalPolishedHarbingerScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 66, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalPolishedHarbingerScarab_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Harbinger Scarab ", 67, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGildedHarbingerScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 68, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGildedHarbingerScarab_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Expedition Scarab ", 69, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRustedExpeditionScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 70, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRustedExpeditionScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Expedition Scarab ", 71, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalPolishedExpeditionScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 72, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalPolishedExpeditionScarab_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Expedition Scarab ", 73, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGildedExpeditionScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 74, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGildedExpeditionScarab_Weight { get; set; } = new RangeNode<int>(45, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Legion Scarab ", 75, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRustedLegionScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 76, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRustedLegionScarab_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Legion Scarab ", 77, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalPolishedLegionScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 78, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalPolishedLegionScarab_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Legion Scarab ", 79, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGildedLegionScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 80, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGildedLegionScarab_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Rusted Abyss Scarab ", 81, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalRustedAbyssScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 82, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalRustedAbyssScarab_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Polished Abyss Scarab ", 83, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalPolishedAbyssScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 84, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalPolishedAbyssScarab_Weight { get; set; } = new RangeNode<int>(25, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Gilded Abyss Scarab ", 85, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalGildedAbyssScarab { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 86, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalGildedAbyssScarab_Weight { get; set; } = new RangeNode<int>(35, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards Currency ", 87, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsCurrency { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 88, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsCurrency_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards Basic Currency ", 89, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsBasicCurrency { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 90, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsBasicCurrency_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards a League Currency ", 91, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsLeagueCurrency { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 92, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsLeagueCurrency_Weight { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards other Divination Cards ", 93, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsOtherDivinationCards { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 94, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsOtherDivinationCards_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards Gems ", 95, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsGems { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 96, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsGems_Weight { get; set; } = new RangeNode<int>(1, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards Levelled Gems ", 97, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsLevelledGems { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 98, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsLevelledGems_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Eldritch Minions:  (1.6 to 3.2)% chance to drop an additional Divination Card which rewards Quality Gems ", 99, 16000)]
        public ToggleNode Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsQualityGems { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 100, 16000)]
        public RangeNode<int> Eater_EldritchMinionsChanceToDropAnAdditionalDivinationCardWhichRewardsQualityGems_Weight { get; set; } = new RangeNode<int>(5, 1, 100);



        [Menu("Eater of Worlds Mod Weights - Upsides (Player)", 17000)]
        public EmptyNode EaterUpsidePlayerMods { get; set; } = new EmptyNode();

        [Menu("Player:  	Unique Items dropped by slain Enemies have (15 to 30)% chance to be Duplicated  ", 1, 17000)]
        public ToggleNode Eater_PlayerUniqueItemsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 2, 17000)]
        public RangeNode<int> Eater_PlayerUniqueItemsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(5, 1, 100);

        [Menu("Player:  	Scarabs dropped by slain Enemies have (15 to 30)% chance to be Duplicated   ", 3, 17000)]
        public ToggleNode Eater_PlayerScarabsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 4, 17000)]
        public RangeNode<int> Eater_PlayerScarabsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Player:  	Maps dropped by slain Enemies have (15 to 30)% chance to be Duplicated   ", 5, 17000)]
        public ToggleNode Eater_PlayerMapsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 6, 17000)]
        public RangeNode<int> Eater_PlayerMapsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Player:  	Divination Cards dropped by slain Enemies have (15 to 30)% chance to be Duplicated   ", 7, 17000)]
        public ToggleNode Eater_PlayerDivinationCardsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 8, 17000)]
        public RangeNode<int> Eater_PlayerDivinationCardsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(15, 1, 100);

        [Menu("Player:  	(10 to 20)% increased Quantity of Items found in this Area\n" +
            "                   (15 to 35)% increased Rarity of Items found in this Area ", 9, 17000)]
        public ToggleNode Eater_PlayerIncreasedQuantityOfItemsFoundInThisArea { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 10, 17000)]
        public RangeNode<int> Eater_PlayerIncreasedQuantityOfItemsFoundInThisArea_Weight { get; set; } = new RangeNode<int>(20, 1, 100);

        [Menu("Player:  	Basic Currency Items dropped by slain Enemies have (10 to 15)% chance to be Duplicated", 11, 17000)]
        public ToggleNode Eater_PlayerBasicCurrencyItemsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 12, 17000)]
        public RangeNode<int> Eater_PlayerBasicCurrencyItemsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(10, 1, 100);

        [Menu("Player:  	Gems dropped by slain Enemies have (10 to 15)% chance to be Duplicated ", 13, 17000)]
        public ToggleNode Eater_PlayerGemsDroppedBySlainEnemiesHaveChanceToBeDuplicated { get; set; } = new ToggleNode(false);

        [Menu("Weight", "How good is this reward?\n\n" +
            "If this reward is perfect, and couldn't be better, set it to 100.\n\n" +
            "If the mod is very good, but not amazing, set it to 75.\n\n" +
            "If the mod is garbage, set it to 0.", 14, 17000)]
        public RangeNode<int> Eater_PlayerGemsDroppedBySlainEnemiesHaveChanceToBeDuplicated_Weight { get; set; } = new RangeNode<int>(1, 1, 100);

    }
}
