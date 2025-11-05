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
        [System.Obsolete("Can be safely ignored for now.")]
        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);

        [Menu("Search Radius", "Radius the plugin will search in for interactable objects.", 1, 3000)]
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(95, 0, 300);

        [Menu("Items", "Click items", 2, 3000)]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);

        [Menu("Ignore Unique Items", "Ignore unique items", 3, 3000)]
        public ToggleNode IgnoreUniques { get; set; } = new ToggleNode(false);

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
        [System.Obsolete("Can be safely ignored for now.")]
        public HotkeyNode ToggleItemsHotkey { get; set; } = new HotkeyNode(Keys.Z);


        [Menu("Essences", 3500)]
        public EmptyNode Essences { get; set; } = new EmptyNode();

        [Menu("Essences", "Click essences", 1, 3500)]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);

        [Menu("Corrupt ALL Essences (Warning: This overrides all settings below)", "Corrupt all essences, overriding the settings below.", 3, 3500)]
        public ToggleNode CorruptAllEssences { get; set; } = new ToggleNode(false);

        [Menu("Corrupt Misery, Envy, Dread, Scorn", "Corrupt misery, envy, dread, scorn.", 4, 3500)]
        public ToggleNode CorruptMEDSEssences { get; set; } = new ToggleNode(true);

        [Menu("Corrupt Essences which don't contain a shrieking essence", "Corrupt any essences that don't have a shrieking essence on them.\n\n" +
            "This is for if you use the 'Crystal Resonance' atlas passive, which duplicates monsters when they contain a shrieking essence.", 6, 3500)]
        public ToggleNode CorruptAnyNonShrieking { get; set; } = new ToggleNode(true);

        [Menu("Searing Exarch", 4000)]
        public EmptyNode ExarchAltar { get; set; } = new EmptyNode();

        [Menu("Click recommended option",
            "Clicks searing exarch altars for you based on a decision tree created from your settings." +
            "\n\nIf both options are as good as each other (according to your weights), this won't click for you.", 1, 4000)]
        public ToggleNode ClickExarchAltars { get; set; } = new ToggleNode(false);

        [Menu("Highlight recommended option",
            "Highlights the recommended option for you to choose for searing exarch altars, based on a decision tree created from your settings below.", 2, 4000)]
        public ToggleNode HighlightExarchAltars { get; set; } = new ToggleNode(true);

        [Menu("Eater of Worlds", 4500)]
        public EmptyNode EaterAltar { get; set; } = new EmptyNode();

        [Menu("Click recommended option",
            "Clicks eater of worlds altars for you based on a decision tree created from your settings." +
            "\n\nIf both options are as good as each other (according to your weights), this won't click for you.", 1, 4500)]
        public ToggleNode ClickEaterAltars { get; set; } = new ToggleNode(false);

        [Menu("Highlight recommended option",
            "Highlights the recommended option for you to choose for eater of worlds altars, based on a decision tree created from your settings below.", 2, 4500)]
        public ToggleNode HighlightEaterAltars { get; set; } = new ToggleNode(true);

        [JsonIgnore]
        public CustomNode AltarModWeights { get; }

        private string upsideSearchFilter = "";
        private string downsideSearchFilter = "";

        public ClickItSettings()
        {
            InitializeDefaultWeights();

            AltarModWeights = new CustomNode
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

                        // Search bar
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputTextWithHint("##UpsideSearch", "Search", ref upsideSearchFilter, 256);
                        ImGui.SameLine();
                        if (ImGui.Button("Clear##UpsideClear"))
                        {
                            upsideSearchFilter = "";
                        }

                        ImGui.Spacing();

                        if (ImGui.BeginTable("UpsideModsConfig", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
                        {
                            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);
                            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthFixed, 800);
                            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 75);
                            ImGui.TableHeadersRow();

                            string currentSection = "";

                            foreach ((string id, string name, string type, int _) in AltarModsConstants.UpsideMods)
                            {
                                bool matchesSearch = string.IsNullOrEmpty(upsideSearchFilter) ||
                                                   name.ToLower().Contains(upsideSearchFilter.ToLower()) ||
                                                   type.ToLower().Contains(upsideSearchFilter.ToLower());

                                if (!matchesSearch) continue;

                                // Determine section based on mod type
                                string sectionHeader = type switch
                                {
                                    "Minion" => "Minion Drops",
                                    "Boss" => "Boss Drops",
                                    "Player" => "Player Bonuses",
                                    _ => ""
                                };

                                if (!string.IsNullOrEmpty(sectionHeader) && sectionHeader != currentSection)
                                {
                                    currentSection = sectionHeader;

                                    ImGui.TableNextRow(ImGuiTableRowFlags.None);
                                    ImGui.TableNextColumn();
                                    Vector4 headerColor = type switch
                                    {
                                        "Minion" => new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                                        "Boss" => new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                                        "Player" => new Vector4(0.2f, 0.2f, 0.6f, 0.3f),
                                        _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f)
                                    };

                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(headerColor));

                                    ImGui.Text(""); // Empty weight column for header
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{sectionHeader}");
                                }

                                ImGui.PushID($"upside_{id}");
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

                                Vector4 textColor = type switch
                                {
                                    "Minion" => new Vector4(0.4f, 0.8f, 0.4f, 1.0f),
                                    "Boss" => new Vector4(0.8f, 0.4f, 0.4f, 1.0f),
                                    "Player" => new Vector4(0.4f, 0.7f, 0.9f, 1.0f),
                                    _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
                                };

                                ImGui.TextColored(textColor, name);

                                // Third column - Type
                                _ = ImGui.TableNextColumn();
                                ImGui.Text(type);

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

                        ImGui.TextWrapped("Weight Scale (Higher = More Dangerous):");
                        DrawWeightScale(bestAtHigh: false); // Red → Green (100 = Worst)

                        ImGui.Spacing();
                        ImGui.Spacing();

                        // Search bar
                        ImGui.SetNextItemWidth(300);
                        ImGui.InputTextWithHint("##DownsideSearch", "Search", ref downsideSearchFilter, 256);
                        ImGui.SameLine();
                        if (ImGui.Button("Clear##DownsideClear"))
                        {
                            downsideSearchFilter = "";
                        }

                        ImGui.Spacing();

                        if (ImGui.BeginTable("DownsideModsConfig", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
                        {
                            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 100);
                            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthFixed, 800);
                            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 75);
                            ImGui.TableHeadersRow();

                            string lastProcessedSection = "";
                            int modIndex = 0;

                            foreach ((string id, string name, string type, int defaultWeight) in AltarModsConstants.DownsideMods)
                            {
                                // Check if this matches the search filter
                                bool matchesSearch = string.IsNullOrEmpty(downsideSearchFilter) ||
                                                   name.ToLower().Contains(downsideSearchFilter.ToLower()) ||
                                                   type.ToLower().Contains(downsideSearchFilter.ToLower());

                                if (!matchesSearch) continue;

                                // Determine section header based on the weight (simplified to 5 categories only)
                                string sectionHeader = "";

                                // Use the weight to determine category - all modifiers go into these 5 categories regardless of type
                                if (defaultWeight == 100)
                                {
                                    sectionHeader = "Common Build Bricking Modifiers";
                                }
                                else if (defaultWeight >= 70)
                                {
                                    sectionHeader = "Very Dangerous Modifiers";
                                }
                                else if (defaultWeight >= 40)
                                {
                                    sectionHeader = "Dangerous Modifiers";
                                }
                                else if (defaultWeight >= 2)
                                {
                                    sectionHeader = "Ok Modifiers";
                                }
                                else
                                {
                                    sectionHeader = "Free Modifiers";
                                }

                                // Draw section header if we have one and it's different from the last
                                if (!string.IsNullOrEmpty(sectionHeader) && sectionHeader != lastProcessedSection)
                                {
                                    lastProcessedSection = sectionHeader;

                                    // Section header row with colored background
                                    ImGui.TableNextRow(ImGuiTableRowFlags.None);
                                    ImGui.TableNextColumn();

                                    // Color the header based on danger level
                                    Vector4 headerColor = sectionHeader switch
                                    {
                                        "Build Bricking Modifiers" => new Vector4(1.0f, 0.0f, 0.0f, 0.6f),  // Bright red - extremely dangerous
                                        "Very Dangerous Modifiers" => new Vector4(0.9f, 0.1f, 0.1f, 0.5f),          // Red - very dangerous
                                        "Dangerous Modifiers" => new Vector4(1.0f, 0.5f, 0.0f, 0.4f),               // Orange - dangerous
                                        "Ok Modifiers" => new Vector4(1.0f, 1.0f, 0.0f, 0.3f),                      // Yellow - manageable
                                        "Free Modifiers" => new Vector4(0.0f, 0.7f, 0.0f, 0.3f),                    // Green - easy/beneficial
                                        _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f)                                     // Gray default
                                    };

                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(headerColor));

                                    ImGui.Text(""); // Empty weight column for header
                                    ImGui.TableNextColumn();
                                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), sectionHeader);
                                }

                                // Regular mod row
                                ImGui.PushID($"downside_{id}");
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

                                // Color the mod name based on danger level
                                Vector4 textColor = sectionHeader switch
                                {
                                    "Build Bricking Modifiers" => new Vector4(1.0f, 0.2f, 0.2f, 1.0f),  // Bright red text
                                    "Very Dangerous Modifiers" => new Vector4(1.0f, 0.4f, 0.4f, 1.0f),         // Light red text
                                    "Dangerous Modifiers" => new Vector4(1.0f, 0.7f, 0.3f, 1.0f),              // Orange text
                                    "Ok Modifiers" => new Vector4(1.0f, 1.0f, 0.5f, 1.0f),                     // Yellow text
                                    "Free Modifiers" => new Vector4(0.5f, 1.0f, 0.5f, 1.0f),                   // Green text
                                    _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)                                    // White default
                                };

                                ImGui.TextColored(textColor, name);

                                // Third column - Type
                                _ = ImGui.TableNextColumn();
                                ImGui.Text(type);

                                ImGui.PopID();
                                modIndex++;
                            }

                            ImGui.EndTable();
                        }

                        ImGui.TreePop();
                    }
                }
            };

        }

        internal void InitializeDefaultWeights()
        {
            // Initialize with UpsideMods default values (only if not already set)
            foreach ((string id, _, _, int defaultValue) in AltarModsConstants.UpsideMods)
            {
                if (!ModTiers.ContainsKey(id))
                {
                    ModTiers[id] = defaultValue;
                }
            }

            // Initialize with DownsideMods default values (only if not already set)
            foreach ((string id, _, _, int defaultValue) in AltarModsConstants.DownsideMods)
            {
                if (!ModTiers.ContainsKey(id))
                {
                    ModTiers[id] = defaultValue;
                }
            }
        }

        public void EnsureAllModsHaveWeights()
        {
            // This method can be called after settings load to ensure new mods get default weights
            InitializeDefaultWeights();
        }
        public int GetModTier(string modId)
        {
            return ModTiers.TryGetValue(modId ?? "", out int value) ? value : 1;
        }

        public Dictionary<string, int> ModTiers { get; set; } = new Dictionary<string, int>();

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
            int steps = 4; // 1,25,50,75,100 -> 4 intervals
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
                string label = (i == 0 ? 1 : i * 25).ToString();
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

    }
}