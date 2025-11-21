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
        [Menu("Debug Mode", "Enables debug mode to help with troubleshooting issues.", 1, 900)]
        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
        [Menu("Additional Debug Information", "Provides more debug text related to rendering the overlay. ", 2, 900)]
        public ToggleNode RenderDebug { get; set; } = new ToggleNode(false);
        [ConditionalDisplay("RenderDebug")]
        [Menu("Status", "Show/hide the Status debug section", 1, 2)]
        public ToggleNode DebugShowStatus { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("Performance", "Show/hide the Performance debug section", 2, 2)]
        public ToggleNode DebugShowPerformance { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("Game State", "Show/hide the Game State debug section", 3, 2)]
        public ToggleNode DebugShowGameState { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("Altar Service", "Show/hide the Altar Service debug section", 4, 2)]
        public ToggleNode DebugShowAltarService { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("Altar Detection", "Show/hide the Altar Detection debug section", 5, 2)]
        public ToggleNode DebugShowAltarDetection { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("Labels", "Show/hide the Labels debug section", 6, 2)]
        public ToggleNode DebugShowLabels { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("Recent Errors", "Show/hide the Recent Errors debug section", 7, 2)]
        public ToggleNode DebugShowRecentErrors { get; set; } = new ToggleNode(true);
        [ConditionalDisplay("RenderDebug")]
        [Menu("Debug Frames", "Show/hide the debug screen area frames", 8, 2)]
        public ToggleNode DebugShowFrames { get; set; } = new ToggleNode(true);
        [Menu("Log messages", "This will flood your log and screen with debug text.", 3, 900)]
        public ToggleNode LogMessages { get; set; } = new ToggleNode(false);
        [Menu("Report Bug", "If you run into a bug that hasn't already been reported, please report it here.", 5, 900)]
        public ButtonNode ReportBugButton { get; set; } = new ButtonNode();

        // ----- General -----
        [Menu("General", 3000)]
        public EmptyNode Click { get; set; } = new EmptyNode();
        [Menu("Hotkey", "Held hotkey to start clicking", 1, 3000)]
        [System.Obsolete("Can be safely ignored for now.")]
        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);
        [Menu("Search Radius", "Radius the plugin will search in for interactable objects. A value of 100 is recommended for 1080p, though, you may need to increase this on higher resolutions.", 2, 3000)]
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(100, 0, 300);
        [Menu("Click Frequency Target (ms)", "Target milliseconds between clicks for non-altar/shrine actions. Higher = less frequent clicks.\n\nThe plugin will try to maintain this target as best it can, but heavy CPU load or many visible labels may increase delays.", 3, 3000)]
        public RangeNode<int> ClickFrequencyTarget { get; set; } = new RangeNode<int>(80, 80, 250);
        [Menu("Chest Height Offset", "If you're experiencing a lot of missclicking for chests specifically (clicking too high or low),\n" +
            "change this value. If you're clicking too high, lower the value, if you're clicking too low, raise the value", 4, 3000)]
        public RangeNode<int> ChestHeightOffset { get; set; } = new RangeNode<int>(0, -100, 100);
        [Menu("---", 5, 3000)]
        public EmptyNode GeneralSeparator1 { get; set; } = new EmptyNode();
        [Menu("Items", "Click items", 6, 3000)]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);
        [Menu("Ignore Unique Items", "Ignore unique items", 7, 3000)]
        public ToggleNode IgnoreUniques { get; set; } = new ToggleNode(false);
        [Menu("Basic Chests", "Click normal (non-league related) chests", 8, 3000)]
        public ToggleNode ClickBasicChests { get; set; } = new ToggleNode(false);
        [Menu("League Mechanic 'Chests'", "Click league mechanic related 'chests' (blight pustules, legion war hoards / chests, sentinel caches, etc)", 9, 3000)]
        public ToggleNode ClickLeagueChests { get; set; } = new ToggleNode(true);
        [Menu("Shrines", "Click shrines", 10, 3000)]
        public ToggleNode ClickShrines { get; set; } = new ToggleNode(true);
        [Menu("Area Transitions", "Click area transitions", 11, 3000)]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(false);
        [Menu("Crafting Recipes", "Click crafting recipes", 12, 3000)]
        public ToggleNode ClickCraftingRecipes { get; set; } = new ToggleNode(true);
        [Menu("---", 13, 3000)]
        public EmptyNode GeneralSeparator2 { get; set; } = new EmptyNode();
        [Menu("Block when Left or Right Panel open", "Prevent clicks when the inventory or character screen are open", 14, 3000)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);
        [Menu("Left-handed", "Changes the primary mouse button the plugin uses from left to right.", 15, 3000)]
        public ToggleNode LeftHanded { get; set; } = new ToggleNode(false);
        [Menu("Block User Input", "Prevents mouse movement and clicks while the hotkey is held. Will help stop missclicking, but may cause issues.\n\nYou must run ExileAPI as Administrator for this to function.", 16, 3000)]
        public ToggleNode BlockUserInput { get; set; } = new ToggleNode(false);
        [Menu("Toggle Item View", "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels", 17, 3000)]
        public ToggleNode ToggleItems { get; set; } = new ToggleNode(true);
        [Menu("Toggle Items Hotkey", "Hotkey to toggle the display of ground items / labels", 18, 3000)]
        public HotkeyNode ToggleItemsHotkey { get; set; } = new HotkeyNode(Keys.Z);
        [Menu("Lazy Mode (not recommended, make sure you read the tooltip for this setting before enabling it) ->", "Will automatically click most things for you, without you needing to hold the key.\n\nThere are inherent limitations to this feature that cannot be fixed:\n\n-> If you are holding down a skill, for instance, Cyclone, you cannot interact with most things in the game.\n   If you use a skill that requires you to hold a key, you must set it to right-click or left-click for lazy mode to function correctly.\n\n-> This will take control away from you at crucial moments, potentially causing you to die.\n\n-> The plugin cannot detect if a strongbox has been activated, if a chest is locked, or if a settlers tree\n   has been activated. This is a limitation with exileapi and not the plugin and for this reason, Lazy Mode\n   is not allowed to click strongboxes, chests or the settlers tree. When one of these is on-screen,\n   Lazy Mode will be temporarily disabled, until the blacklisted item is off of the screen, which will\n   allow you to manually press the hotkey to click these items specifically if you want to.\n\nBehaviour of the 'Hotkey' will be inverted:\n-> When the hotkey is released, the plugin will be allowed to click.\n-> When the hotkey is held, the plugin will not be allowed to click.", 19, 3000)]
        public ToggleNode LazyMode { get; set; } = new ToggleNode(false);
        [Menu("Lazy Mode Click Limiting (ms)", "When Lazy Mode is enabled, this sets the minimum delay (in milliseconds)\nthat must pass between consecutive clicks performed by the plugin.\nThis limiter applies to all automated clicks (shrines, altars, strongboxes, etc.)\nonly while Lazy Mode is active. Increase this value to reduce click spam and\nprevent the plugin from taking control away from the user.", 20, 3000)]
        public RangeNode<int> LazyModeClickLimiting { get; set; } = new RangeNode<int>(150, 80, 1000);
        [Menu("Lazy Mode Disable Hotkey", "When Lazy Mode is enabled and active, holding this key will temporarily disable lazy mode clicking.\nThis allows you to pause automated clicking without disabling lazy mode entirely.\nThe main hotkey will still work for manual clicking and other functions.", 21, 3000)]
        public HotkeyNode LazyModeDisableKey { get; set; } = new HotkeyNode(Keys.F2);
        // ----- Mechanics -----
        [Menu("Mechanics", 3100)]
        public EmptyNode Mechanics { get; set; } = new EmptyNode();
        [Menu("Alva Temple Doors", "Click alva temple doors", 11, 3100)]
        public ToggleNode ClickAlvaTempleDoors { get; set; } = new ToggleNode(true);
        [Menu("Betrayal", "Click betrayal labels", 12, 3100)]
        public ToggleNode ClickBetrayal { get; set; } = new ToggleNode(true);
        [Menu("Blight", "Click blight pumps", 13, 3100)]
        public ToggleNode ClickBlight { get; set; } = new ToggleNode(true);
        [Menu("Breach Nodes", "Click breach nodes", 14, 3100)]
        public ToggleNode ClickBreachNodes { get; set; } = new ToggleNode(false);
        [Menu("Harvest", "Click nearest harvest", 15, 3100)]
        public ToggleNode NearestHarvest { get; set; } = new ToggleNode(true);
        [Menu("Legion Encounters", "Click legion encounter pillars", 16, 3100)]
        public ToggleNode ClickLegionPillars { get; set; } = new ToggleNode(true);
        [Menu("Sanctum", "Click sanctum related stuff", 17, 3100)]
        public ToggleNode ClickSanctum { get; set; } = new ToggleNode(true);
        [Menu("Settlers Ore Deposits", "Click settlers league ore deposits (CrimsonIron, Orichalcum, Verisium, etc)\n\nThere is a known issue with this feature meaning the plugin will repeatedly try to click on trees that have already been activated.\n\nI don't currently think there is any way to fix this due to limitations with the game memory and ExileAPI.", 19, 3100)]
        public ToggleNode ClickSettlersOre { get; set; } = new ToggleNode(true);

        // ----- Delve -----
        [Menu("Delve", 3200)]
        public EmptyNode Delve { get; set; } = new EmptyNode();
        [Menu("Azurite Veins", "Click azurite veins", 1, 3200)]
        public ToggleNode ClickAzuriteVeins { get; set; } = new ToggleNode(true);
        [Menu("Sulphite Veins", "Click sulphite veins", 2, 3200)]
        public ToggleNode ClickSulphiteVeins { get; set; } = new ToggleNode(true);
        [Menu("Spawners", "Click spawners", 3, 3200)]
        public ToggleNode ClickDelveSpawners { get; set; } = new ToggleNode(true);
        [Menu("Flares", "Use flares when all of these conditions are true:\n\n-> Your darkness debuff stacks are at least the 'Darkness Debuff Stacks' value.\n-> Your health is below the 'Use flare below Health' value.\n-> Your energy shield is below the 'Use flare below Energy Shield' value.\n\nIf you're playing CI and have 1 max life, set Health to 100.\n\nIf you have no energy shield, set Energy Shield to 100.", 4, 3200)]
        public ToggleNode ClickDelveFlares { get; set; } = new ToggleNode(false);
        [Menu("Flare Hotkey", "Set this to your in-game keybind for flares, the plugin will press this button to use a flare", 5, 3200)]
        public HotkeyNode DelveFlareHotkey { get; set; } = new HotkeyNode(Keys.D6);
        [Menu("Darkness Debuff Stacks", 6, 3200)]
        public RangeNode<int> DarknessDebuffStacks { get; set; } = new RangeNode<int>(5, 1, 10);
        [Menu("Flare Health %", 7, 3200)]
        public RangeNode<int> DelveFlareHealthThreshold { get; set; } = new RangeNode<int>(75, 2, 100);
        [Menu("Flare Energy Shield %", 8, 3200)]
        public RangeNode<int> DelveFlareEnergyShieldThreshold { get; set; } = new RangeNode<int>(75, 2, 100);

        // ----- Essences -----
        [Menu("Essences", 3500)]
        public EmptyNode Essences { get; set; } = new EmptyNode();
        [Menu("Essences", "Click essences", 1, 3500)]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);
        [Menu("Corrupt ALL Essences (Warning: This overrides all settings below)", "Corrupt all essences, overriding the settings below.", 3, 3500)]
        public ToggleNode CorruptAllEssences { get; set; } = new ToggleNode(false);
        [Menu("Corrupt Misery, Envy, Dread, Scorn", "Corrupt misery, envy, dread, scorn.", 4, 3500)]
        public ToggleNode CorruptMEDSEssences { get; set; } = new ToggleNode(true);

        // ----- Ritual -----
        [Menu("Ritual", 3505)]
        public EmptyNode Ritual { get; set; } = new EmptyNode();
        [Menu("Initiate Ritual Altars", "Click ritual altars that have not been completed yet", 1, 3505)]
        public ToggleNode ClickRitualInitiate { get; set; } = new ToggleNode(true);
        [Menu("Completed Ritual Altars", "Click ritual altars that have been completed", 2, 3505)]
        public ToggleNode ClickRitualCompleted { get; set; } = new ToggleNode(true);

        // ----- Strongboxes -----
        [Menu("Strongboxes", 3510)]
        public EmptyNode Strongboxes { get; set; } = new EmptyNode();
        [Menu("Regular Strongbox", "Click regular strongboxes", 1, 3510)]
        public ToggleNode RegularStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Arcanist Strongbox (currency)", "Click arcanist strongboxes", 2, 3510)]
        public ToggleNode ArcanistStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Armourer Strongbox (armour)", "Click armourer strongboxes", 3, 3510)]
        public ToggleNode ArmourerStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Artisan Strongbox (quality currency)", "Click artisan strongboxes", 4, 3510)]
        public ToggleNode ArtisanStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Blacksmith Strongbox (weapons)", "Click blacksmith strongboxes", 5, 3510)]
        public ToggleNode BlacksmithStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Cartographer Strongbox (maps)", "Click cartographer strongboxes", 6, 3510)]
        public ToggleNode CartographerStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Diviner Strongbox (divination cards)", "Click diviner strongboxes", 7, 3510)]
        public ToggleNode DivinerStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Gemcutter Strongbox (gems)", "Click gemcutter strongboxes", 8, 3510)]
        public ToggleNode GemcutterStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Jeweller Strongbox (jewellery)", "Click jeweller strongboxes", 9, 3510)]
        public ToggleNode JewellerStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Large Strongbox (+ quantity)", "Click large strongboxes", 10, 3510)]
        public ToggleNode LargeStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Ornate Strongbox (+ rarity)", "Click ornate strongboxes", 11, 3510)]
        public ToggleNode OrnateStrongbox { get; set; } = new ToggleNode(true);
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
