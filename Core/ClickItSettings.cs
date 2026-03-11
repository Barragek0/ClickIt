using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using System.Numerics;
using ClickIt.Definitions;

namespace ClickIt
{
    public partial class ClickItSettings : ISettings
    {
        private const string AltarTypeMinion = "Minion";
        private const string AltarTypeBoss = "Boss";
        private const string AltarTypePlayer = "Player";
        private static readonly Vector4 WhitelistTextColor = new(0.4f, 0.8f, 0.4f, 1.0f);
        private static readonly Vector4 BlacklistTextColor = new(0.8f, 0.4f, 0.4f, 1.0f);

        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        // ----- General Interactions -----
        [Menu("General", 1300)]
        public EmptyNode WorldInteractionsCategory { get; set; } = new EmptyNode();
        [Menu("Basic Chests", "Click normal (non-league related) chests", 1, 1300)]
        public ToggleNode ClickBasicChests { get; set; } = new ToggleNode(false);
        [Menu("League Mechanic 'Chests'", "Click league mechanic related 'chests' (blight pustules, legion war hoards / chests, sentinel caches, etc)", 2, 1300)]
        public ToggleNode ClickLeagueChests { get; set; } = new ToggleNode(true);
        [Menu("Shrines", "Click shrines", 3, 1300)]
        public ToggleNode ClickShrines { get; set; } = new ToggleNode(true);
        [Menu("Area Transitions", "Click area transitions", 4, 1300)]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(false);
        [Menu("Crafting Recipes", "Click crafting recipes", 5, 1300)]
        public ToggleNode ClickCraftingRecipes { get; set; } = new ToggleNode(true);
        [Menu("Doors", "Click doors", 6, 1300)]
        public ToggleNode ClickDoors { get; set; } = new ToggleNode(false);
        [Menu("Levers", "Click levers", 7, 1300)]
        public ToggleNode ClickLevers { get; set; } = new ToggleNode(false);

        // ----- Mechanics -----
        [Menu("Mechanics", 1400)]
        public EmptyNode Mechanics { get; set; } = new EmptyNode();
        [Menu("Alva Temple Doors", "Click alva temple doors", 1, 1400)]
        public ToggleNode ClickAlvaTempleDoors { get; set; } = new ToggleNode(true);
        [Menu("Betrayal", "Click betrayal labels", 2, 1400)]
        public ToggleNode ClickBetrayal { get; set; } = new ToggleNode(false);
        [Menu("Blight", "Click blight pumps", 3, 1400)]
        public ToggleNode ClickBlight { get; set; } = new ToggleNode(true);
        [Menu("Breach Nodes", "Click breach nodes", 4, 1400)]
        public ToggleNode ClickBreachNodes { get; set; } = new ToggleNode(false);
        [Menu("Legion Pillars", "Click legion encounter pillars", 5, 1400)]
        public ToggleNode ClickLegionPillars { get; set; } = new ToggleNode(true);
        [Menu("Nearest Harvest Plot", "Click nearest harvest plot", 6, 1400)]
        public ToggleNode NearestHarvest { get; set; } = new ToggleNode(true);
        [Menu("Sanctum", "Click sanctum related stuff", 7, 1400)]
        public ToggleNode ClickSanctum { get; set; } = new ToggleNode(true);
        [Menu("Settlers Ore Deposits", "Click settlers league ore deposits (CrimsonIron, Orichalcum, etc)\n\nThere is a known issue with this feature meaning the plugin will repeatedly try to click on trees that have already been activated.\n\nI don't currently think there is any way to fix this due to limitations with the game memory and ExileAPI.", 8, 1400)]
        public ToggleNode ClickSettlersOre { get; set; } = new ToggleNode(true);

        // ----- Items -----
        [Menu("Items", 1500)]
        public EmptyNode ItemPickupCategory { get; set; } = new EmptyNode();
        [Menu("Items", "Click items", 1, 1500)]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);

        [Menu("Item Type Filters", "", 2, 1500)]
        [JsonIgnore]
        public CustomNode ItemTypeFiltersPanel { get; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> ItemTypeWhitelistIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> ItemTypeBlacklistIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, HashSet<string>> ItemTypeWhitelistSubtypeIds { get; set; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, HashSet<string>> ItemTypeBlacklistSubtypeIds { get; set; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private string _expandedItemTypeRowKey = string.Empty;

        // ----- Essences -----
        [Menu("Essences", 1600)]
        public EmptyNode Essences { get; set; } = new EmptyNode();
        [Menu("Essences", "Click essences", 1, 1600)]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);
        [Menu("Essence Corruption Table", "", 2, 1600)]
        [JsonIgnore]
        public CustomNode EssenceCorruptionTablePanel { get; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> EssenceCorruptNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> EssenceDontCorruptNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ----- Ultimatum -----
        [Menu("Ultimatum", 1700)]
        public EmptyNode Ultimatum { get; set; } = new EmptyNode();
        [Menu("Click Ultimatum", "Click and select Ultimatum modifier options by configured priority.", 1, 1700)]
        public ToggleNode ClickUltimatum { get; set; } = new ToggleNode(false);
        [Menu("Show Option Overlay", "Draws outlines on Ultimatum options: green for the selected option and priority colors for the other options.", 2, 1700)]
        public ToggleNode ShowUltimatumOptionOverlay { get; set; } = new ToggleNode(true);
        [Menu("Modifier Priority Table", "", 3, 1700)]
        [JsonIgnore]
        public CustomNode UltimatumModifierTablePanel { get; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> UltimatumModifierPriority { get; set; } = new();

        // ----- Strongboxes -----
        [Menu("Strongboxes", 1800)]
        public EmptyNode Strongboxes { get; set; } = new EmptyNode();
        [Menu("Show Strongbox Frames", "When enabled, draws a visual frame around strongboxes indicating whether or not they are locked", 1, 1800)]
        public ToggleNode ShowStrongboxFrames { get; set; } = new ToggleNode(true);
        [Menu("Strongbox Table", "", 2, 1800)]
        [JsonIgnore]
        public CustomNode StrongboxFilterTablePanel { get; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> StrongboxClickIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> StrongboxDontClickIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ----- Altars -----
        [Menu("Altars", 1900)]
        public EmptyNode AltarsCategory { get; set; } = new EmptyNode();

        [Menu("Settings", 1, 1900)]
        [JsonIgnore]
        public CustomNode AltarsPanel { get; }

        [JsonIgnore]
        public bool ShowRawAltarNodesInSettings => false;

        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public EmptyNode ExarchAltar { get; set; } = new EmptyNode();
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode ClickExarchAltars { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode HighlightExarchAltars { get; set; } = new ToggleNode(true);

        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public EmptyNode EaterAltar { get; set; } = new EmptyNode();
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode ClickEaterAltars { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode HighlightEaterAltars { get; set; } = new ToggleNode(true);

        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public EmptyNode WeightOverrides { get; set; } = new EmptyNode();
        [JsonIgnore]
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public CustomNode AltarModWeights { get; }

        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode ValuableUpside { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public RangeNode<int> ValuableUpsideThreshold { get; set; } = new RangeNode<int>(90, 1, 100);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode UnvaluableUpside { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public RangeNode<int> UnvaluableUpsideThreshold { get; set; } = new RangeNode<int>(1, 1, 100);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode DangerousDownside { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public RangeNode<int> DangerousDownsideThreshold { get; set; } = new RangeNode<int>(90, 1, 100);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode MinWeightThresholdEnabled { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public RangeNode<int> MinWeightThreshold { get; set; } = new RangeNode<int>(25, 1, 100);

        // ----- Alert Sound -----
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public EmptyNode AlertSoundCategory { get; set; } = new EmptyNode();
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode AutoDownloadAlertSound { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ButtonNode OpenConfigDirectory { get; set; } = new ButtonNode();
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ButtonNode ReloadAlertSound { get; set; } = new ButtonNode();
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public RangeNode<int> AlertSoundVolume { get; set; } = new RangeNode<int>(5, 0, 100);

        // ----- Ritual -----
        [Menu("Ritual", 2000)]
        public EmptyNode Ritual { get; set; } = new EmptyNode();
        [Menu("Initiate Ritual Altars", "Click ritual altars that have not been completed yet", 1, 2000)]
        public ToggleNode ClickRitualInitiate { get; set; } = new ToggleNode(true);
        [Menu("Completed Ritual Altars", "Click ritual altars that have been completed", 2, 2000)]
        public ToggleNode ClickRitualCompleted { get; set; } = new ToggleNode(true);

        // ----- Delve -----
        [Menu("Delve", 2100)]
        public EmptyNode Delve { get; set; } = new EmptyNode();
        [Menu("Azurite Veins", "Click azurite veins", 1, 2100)]
        public ToggleNode ClickAzuriteVeins { get; set; } = new ToggleNode(true);
        [Menu("Sulphite Veins", "Click sulphite veins", 2, 2100)]
        public ToggleNode ClickSulphiteVeins { get; set; } = new ToggleNode(true);
        [Menu("Encounter Initiators", "Click delve encounter initiators", 3, 2100)]
        public ToggleNode ClickDelveSpawners { get; set; } = new ToggleNode(true);
        [Menu("Flares", "Use flares when all of these conditions are true:\n\n-> Your darkness debuff stacks are at least the 'Darkness Debuff Stacks' value.\n-> Your health is below the 'Use flare below Health' value.\n-> Your energy shield is below the 'Use flare below Energy Shield' value.\n\nIf you're playing CI and have 1 max life, set Health to 100.\n\nIf you have no energy shield, set Energy Shield to 100.", 4, 2100)]
        public ToggleNode ClickDelveFlares { get; set; } = new ToggleNode(false);
        [Menu("Flare Hotkey", "Set this to your in-game keybind for flares, the plugin will press this button to use a flare", 5, 2100)]
        public HotkeyNode DelveFlareHotkey { get; set; } = new HotkeyNode(Keys.D6);
        [Menu("Darkness Debuff Stacks", 6, 2100)]
        public RangeNode<int> DarknessDebuffStacks { get; set; } = new RangeNode<int>(5, 1, 10);
        [Menu("Flare Health %", 7, 2100)]
        public RangeNode<int> DelveFlareHealthThreshold { get; set; } = new RangeNode<int>(75, 2, 100);
        [Menu("Flare Energy Shield %", 8, 2100)]
        public RangeNode<int> DelveFlareEnergyShieldThreshold { get; set; } = new RangeNode<int>(75, 2, 100);

        private string upsideSearchFilter = "";
        private string downsideSearchFilter = "";
        private string itemTypeSearchFilter = "";
        private string essenceSearchFilter = "";
        private string strongboxSearchFilter = "";
        private string ultimatumSearchFilter = "";
        private string _lastSettingsUiError = string.Empty;
        private string[] _ultimatumPrioritySnapshot = Array.Empty<string>();
        public ClickItSettings()
        {
            InitializeDefaultWeights();
            EnsureItemTypeFiltersInitialized();
            EnsureEssenceCorruptionFiltersInitialized();
            EnsureStrongboxFiltersInitialized();
            EnsureUltimatumModifiersInitialized();
            DebugTestingPanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("DebugTestingPanel", DrawDebugTestingPanel)
            };
            AltarsPanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("AltarsPanel", DrawAltarsPanel)
            };
            AltarModWeights = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("AltarModWeights", DrawAltarModWeights)
            };
            ItemTypeFiltersPanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("ItemTypeFiltersPanel", DrawItemTypeFiltersPanel)
            };
            EssenceCorruptionTablePanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("EssenceCorruptionTablePanel", DrawEssenceCorruptionTablePanel)
            };
            StrongboxFilterTablePanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("StrongboxFilterTablePanel", DrawStrongboxFilterTablePanel)
            };
            UltimatumModifierTablePanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("UltimatumModifierTablePanel", DrawUltimatumModifierTablePanel)
            };
        }

    }
}
