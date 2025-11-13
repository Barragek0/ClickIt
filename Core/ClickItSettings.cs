using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Forms;
using ClickIt.Constants;
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
        [ConditionalDisplay("RenderDebug")]
        [Menu("    Status", "Show/hide the Status debug section", 1, 2)]
        public ToggleNode DebugShowStatus { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("    Performance", "Show/hide the Performance debug section", 2, 2)]
        public ToggleNode DebugShowPerformance { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("    Game State", "Show/hide the Game State debug section", 3, 2)]
        public ToggleNode DebugShowGameState { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("    Altar Service", "Show/hide the Altar Service debug section", 4, 2)]
        public ToggleNode DebugShowAltarService { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("    Altar Detection", "Show/hide the Altar Detection debug section", 5, 2)]
        public ToggleNode DebugShowAltarDetection { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("    Labels", "Show/hide the Labels debug section", 6, 2)]
        public ToggleNode DebugShowLabels { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("    Recent Errors", "Show/hide the Recent Errors debug section", 7, 2)]
        public ToggleNode DebugShowRecentErrors { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("    Debug Frames", "Show/hide the debug screen area frames", 8, 2)]
        public ToggleNode DebugShowFrames { get; set; } = new ToggleNode(true);
        [Menu("Log messages", "This will flood your log with debug information. You should only enable this if you want to report a bug.", 3, 900)]
        public ToggleNode LogMessages { get; set; } = new ToggleNode(false);
        [Menu("Enable internal locking", "Enable internal locking for thread-safety. Disable to turn locks into no-ops for testing/debugging.", 4, 900)]
        public ToggleNode UseLocking { get; set; } = new ToggleNode(true);
        [Menu("Report Bug", "If you run into a bug that hasn't already been reported, please report it here.", 5, 900)]
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
        [Menu("Settlers Ore Deposits", "Click settlers league ore deposits (CrimsonIron, Orichalcum, Verisium, etc)", 15, 3000)]
        public ToggleNode ClickSettlersOre { get; set; } = new ToggleNode(true);
        [Menu("Alva Temple Doors", "Click alva temple doors", 16, 3000)]
        public ToggleNode ClickAlvaTempleDoors { get; set; } = new ToggleNode(true);
        [Menu("Legion Encounters", "Click legion encounter pillars", 17, 3000)]
        public ToggleNode ClickLegionPillars { get; set; } = new ToggleNode(true);
        [Menu("Block when Left or Right Panel open", "Prevent clicks when the inventory or character screen are open", 18, 3000)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);
        [Menu("Chest Height Offset", "If you're experiencing a lot of missclicking for chests specifically (clicking too high or low),\n" +
            "change this value. If you're clicking too high, lower the value, if you're clicking too low, raise the value", 19, 3000)]
        public RangeNode<int> ChestHeightOffset { get; set; } = new RangeNode<int>(0, -100, 100);
        [Menu("Block User Input", "Prevents mouse movement and clicks while the hotkey is held. Will help stop missclicking, but may cause issues.", 20, 3000)]
        public ToggleNode BlockUserInput { get; set; } = new ToggleNode(false);
        [Menu("Toggle Item View", "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels", 21, 3000)]
        public ToggleNode ToggleItems { get; set; } = new ToggleNode(true);
        [Menu("Toggle Items Hotkey", "Hotkey to toggle the display of ground items / labels", 22, 3000)]
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
            AltarModWeights = new CustomNode { DrawDelegate = DrawAltarModWeights };
        }
        private void DrawAltarModWeights()
        {
            DrawUpsideModsSection();
            DrawDownsideModsSection();
        }
        private void DrawUpsideModsSection()
        {
            if (!ImGui.TreeNode("Altar Upside Mods")) return;
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale:");
            DrawWeightScale(bestAtHigh: true);
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar("##UpsideSearch", "Clear##UpsideClear", ref upsideSearchFilter);
            ImGui.Spacing();
            DrawUpsideModsTable();
            ImGui.TreePop();
        }
        private void DrawDownsideModsSection()
        {
            if (!ImGui.TreeNode("Altar Downside Mods")) return;
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale (Higher = More Dangerous):");
            DrawWeightScale(bestAtHigh: false);
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar("##DownsideSearch", "Clear##DownsideClear", ref downsideSearchFilter);
            ImGui.Spacing();
            DrawDownsideModsTable();
            ImGui.TreePop();
        }
        private static void DrawSearchBar(string searchId, string clearId, ref string searchFilter)
        {
            ImGui.SetNextItemWidth(300);
            ImGui.InputTextWithHint(searchId, "Search", ref searchFilter, 256);
            ImGui.SameLine();
            if (ImGui.Button(clearId))
            {
                searchFilter = "";
            }
        }
        private void DrawUpsideModsTable()
        {
            if (!ImGui.BeginTable("UpsideModsConfig", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
                return;
            SetupModTableColumns();
            string currentSection = "";
            foreach ((string id, string name, string type, int _) in AltarModsConstants.UpsideMods)
            {
                if (!MatchesSearchFilter(name, type, upsideSearchFilter))
                    continue;
                string sectionHeader = GetUpsideSectionHeader(type);
                DrawSectionHeaderIfNeeded(ref currentSection, sectionHeader, type);
                DrawUpsideModRow(id, name, type);
            }
            ImGui.EndTable();
        }
        private void DrawDownsideModsTable()
        {
            if (!ImGui.BeginTable("DownsideModsConfig", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
                return;
            SetupModTableColumns();
            string lastProcessedSection = "";
            foreach ((string id, string name, string type, int defaultWeight) in AltarModsConstants.DownsideMods)
            {
                if (!MatchesSearchFilter(name, type, downsideSearchFilter))
                    continue;
                string sectionHeader = GetDownsideSectionHeader(defaultWeight);
                DrawDownsideSectionHeaderIfNeeded(ref lastProcessedSection, sectionHeader);
                DrawDownsideModRow(id, name, type, sectionHeader);
            }
            ImGui.EndTable();
        }
        private static void SetupModTableColumns()
        {
            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 125);
            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthFixed, 900);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 75);
            ImGui.TableHeadersRow();
        }
        private static bool MatchesSearchFilter(string name, string type, string filter)
        {
            return string.IsNullOrEmpty(filter) ||
                   name.ToLower().Contains(filter.ToLower()) ||
                   type.ToLower().Contains(filter.ToLower());
        }
        private static string GetUpsideSectionHeader(string type)
        {
            return type switch
            {
                "Minion" => "Minion Drops",
                "Boss" => "Boss Drops",
                "Player" => "Player Bonuses",
                _ => ""
            };
        }
        private static string GetDownsideSectionHeader(int defaultWeight)
        {
            return defaultWeight switch
            {
                100 => "Build Bricking Modifiers",
                >= 70 => "Very Dangerous Modifiers",
                >= 40 => "Dangerous Modifiers",
                >= 2 => "Ok Modifiers",
                _ => "Free Modifiers"
            };
        }
        private static void DrawSectionHeaderIfNeeded(ref string currentSection, string sectionHeader, string type)
        {
            if (string.IsNullOrEmpty(sectionHeader) || sectionHeader == currentSection)
                return;
            currentSection = sectionHeader;
            DrawUpsideSectionHeader(sectionHeader, type);
        }
        private static void DrawUpsideSectionHeader(string sectionHeader, string type)
        {
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
            ImGui.Text("");
            ImGui.TableNextColumn();
            ImGui.Text($"{sectionHeader}");
        }
        private static void DrawDownsideSectionHeaderIfNeeded(ref string lastProcessedSection, string sectionHeader)
        {
            if (string.IsNullOrEmpty(sectionHeader) || sectionHeader == lastProcessedSection)
                return;
            lastProcessedSection = sectionHeader;
            DrawDownsideSectionHeader(sectionHeader);
        }
        private static void DrawDownsideSectionHeader(string sectionHeader)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableNextColumn();
            Vector4 headerColor = sectionHeader switch
            {
                "Build Bricking Modifiers" => new Vector4(1.0f, 0.0f, 0.0f, 0.6f),
                "Very Dangerous Modifiers" => new Vector4(0.9f, 0.1f, 0.1f, 0.5f),
                "Dangerous Modifiers" => new Vector4(1.0f, 0.5f, 0.0f, 0.4f),
                "Ok Modifiers" => new Vector4(1.0f, 1.0f, 0.0f, 0.3f),
                "Free Modifiers" => new Vector4(0.0f, 0.7f, 0.0f, 0.3f),
                _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f)
            };
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(headerColor));
            ImGui.Text("");
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), sectionHeader);
        }
        private void DrawUpsideModRow(string id, string name, string type)
        {
            ImGui.PushID($"upside_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            _ = ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(125);
            int currentValue = GetModTier(id, type);
            if (ImGui.SliderInt($"", ref currentValue, 1, 100))
            {
                ModTiers[BuildCompositeKey(type, id)] = currentValue;
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
            _ = ImGui.TableNextColumn();
            ImGui.Text(type);
            ImGui.PopID();
        }
        private void DrawDownsideModRow(string id, string name, string type, string sectionHeader)
        {
            ImGui.PushID($"downside_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            _ = ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(125);
            int currentValue = GetModTier(id, type);
            if (ImGui.SliderInt($"", ref currentValue, 1, 100))
            {
                ModTiers[BuildCompositeKey(type, id)] = currentValue;
            }
            ImGui.SetNextItemWidth(1000);
            _ = ImGui.TableNextColumn();
            Vector4 textColor = sectionHeader switch
            {
                "Build Bricking Modifiers" => new Vector4(1.0f, 0.2f, 0.2f, 1.0f),
                "Very Dangerous Modifiers" => new Vector4(1.0f, 0.4f, 0.4f, 1.0f),
                "Dangerous Modifiers" => new Vector4(1.0f, 0.7f, 0.3f, 1.0f),
                "Ok Modifiers" => new Vector4(1.0f, 1.0f, 0.5f, 1.0f),
                "Free Modifiers" => new Vector4(0.5f, 1.0f, 0.5f, 1.0f),
                _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
            };
            ImGui.TextColored(textColor, name);
            _ = ImGui.TableNextColumn();
            ImGui.Text(type);
            ImGui.PopID();
        }
        internal void InitializeDefaultWeights()
        {
            // Initialize composite (type|id) defaults only. Do NOT migrate or remove legacy id-only keys.
            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.UpsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                if (ModTiers.ContainsKey(compositeKey))
                    continue;

                ModTiers[compositeKey] = defaultValue;
            }

            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.DownsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                if (ModTiers.ContainsKey(compositeKey))
                    continue;

                ModTiers[compositeKey] = defaultValue;
            }
        }

        private static string BuildCompositeKey(string type, string id)
        {
            return $"{type}|{id}";
        }
        public void EnsureAllModsHaveWeights()
        {
            InitializeDefaultWeights();
        }
        // Backward compatible single-argument lookup (tries id-only then returns 1)
        public int GetModTier(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return 1;
            return ModTiers.TryGetValue(modId, out int value) ? value : 1;
        }

        // New getter that queries by both type and id. Does NOT fall back to id-only lookup
        // to ensure per-type weights are independent. Returns 1 if composite key not present.
        public int GetModTier(string modId, string type)
        {
            if (string.IsNullOrEmpty(modId)) return 1;
            string compositeKey = BuildCompositeKey(type, modId);
            if (ModTiers.TryGetValue(compositeKey, out int value)) return value;
            return 1;
        }
        public Dictionary<string, int> ModTiers { get; set; } = new Dictionary<string, int>();
        private static void DrawWeightScale(bool bestAtHigh = true, float width = 400f, float height = 20f)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 p = ImGui.GetCursorScreenPos();
            Vector4 colGood = new(0.2f, 1.0f, 0.2f, 1.0f);
            Vector4 colBad = new(1.0f, 0.2f, 0.2f, 1.0f);
            uint colLeft = ImGui.GetColorU32(bestAtHigh ? colBad : colGood);
            uint colRight = ImGui.GetColorU32(bestAtHigh ? colGood : colBad);
            Vector2 rectMin = p;
            Vector2 rectMax = new(p.X + width, p.Y + height);
            drawList.AddRectFilledMultiColor(rectMin, rectMax, colLeft, colRight, colRight, colLeft);
            uint borderCol = ImGui.GetColorU32(ImGuiCol.Border);
            drawList.AddRect(rectMin, rectMax, borderCol);
            int steps = 4;
            float stepPx = width / steps;
            float tickTop = rectMax.Y;
            float tickBottom = rectMax.Y + 6f;
            float labelY = rectMax.Y + 8f;
            for (int i = 0; i <= steps; i++)
            {
                float x = rectMin.X + (i * stepPx);
                drawList.AddLine(new Vector2(x, tickTop), new Vector2(x, tickBottom), ImGui.GetColorU32(ImGuiCol.Text), 1.0f);
                string label = (i == 0 ? 1 : i * 25).ToString();
                Vector2 textSize = ImGui.CalcTextSize(label);
                Vector2 textPos = new(x - (textSize.X * 0.5f), labelY);
                drawList.AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), label);
            }
            string leftLegend = bestAtHigh ? "Worst" : "Best";
            string rightLegend = bestAtHigh ? "Best" : "Worst";
            Vector2 leftLegendSize = ImGui.CalcTextSize(leftLegend);
            Vector2 rightLegendSize = ImGui.CalcTextSize(rightLegend);
            float margin = 2f;
            Vector2 leftPos = new(rectMin.X + margin, labelY + leftLegendSize.Y + 4f);
            Vector2 rightPos = new(rectMax.X - rightLegendSize.X - margin, labelY + rightLegendSize.Y + 4f);
            drawList.AddText(leftPos, ImGui.GetColorU32(ImGuiCol.Text), leftLegend);
            drawList.AddText(rightPos, ImGui.GetColorU32(ImGuiCol.Text), rightLegend);
            ImGui.Dummy(new Vector2(width, height + 28f + leftLegendSize.Y));
        }
    }
}
