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
        private static readonly StringComparer PriorityComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly Vector4 WhitelistTextColor = new(0.4f, 0.8f, 0.4f, 1.0f);
        private static readonly Vector4 BlacklistTextColor = new(0.8f, 0.4f, 0.4f, 1.0f);

        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        // ----- Debug/Testing -----
        [Menu("Debug/Testing", 900)]
        public EmptyNode EmptyTesting { get; set; } = new EmptyNode();

        [Menu(" ", 1, 900)]
        [JsonIgnore]
        public CustomNode DebugTestingPanel { get; }

        [JsonIgnore]
        public bool ShowRawDebugNodesInSettings => false;

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Debug Mode", "Enables debug mode to help with troubleshooting issues.", 1, 900)]
        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Additional Debug Information", "Provides more debug text related to rendering the overlay. ", 2, 900)]
        public ToggleNode RenderDebug { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Status", "Show/hide the Status debug section", 1, 2)]
        public ToggleNode DebugShowStatus { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Game State", "Show/hide the Game State debug section", 2, 2)]
        public ToggleNode DebugShowGameState { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Performance", "Show/hide the Performance debug section", 3, 2)]
        public ToggleNode DebugShowPerformance { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Click Frequency Target", "Show/hide the Click Frequency Target debug section", 4, 2)]
        public ToggleNode DebugShowClickFrequencyTarget { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Altar Detection", "Show/hide the Altar Detection debug section", 5, 2)]
        public ToggleNode DebugShowAltarDetection { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Altar Service", "Show/hide the Altar Service debug section", 6, 2)]
        public ToggleNode DebugShowAltarService { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Labels", "Show/hide the Labels debug section", 7, 2)]
        public ToggleNode DebugShowLabels { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Hovered Item Metadata", "Show/hide the hovered item metadata debug section", 8, 2)]
        public ToggleNode DebugShowHoveredItemMetadata { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Recent Errors", "Show/hide the Recent Errors debug section", 9, 2)]
        public ToggleNode DebugShowRecentErrors { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Debug Frames", "Show/hide the debug screen area frames", 10, 2)]
        public ToggleNode DebugShowFrames { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Log messages", "This will flood your log and screen with debug text.", 3, 900)]
        public ToggleNode LogMessages { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Report Bug", "If you run into a bug that hasn't already been reported, please report it here.", 4, 900)]
        public ButtonNode ReportBugButton { get; set; } = new ButtonNode();

        // ----- General -----
        [Menu("General", 1000)]
        public EmptyNode Click { get; set; } = new EmptyNode();
        [Menu("Click Hotkey", "Held hotkey to start clicking", 1, 1000)]
        [Obsolete("Can be safely ignored for now.")]
        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);
        [Menu("Search Radius", "Radius the plugin will search in for interactable objects. A value of 100 is recommended for 1080p, though, you may need to increase this on higher resolutions.", 2, 1000)]
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(100, 0, 300);
        [Menu("Click Frequency Target (ms)", "Target milliseconds between clicks for non-altar/shrine actions. Higher = less frequent clicks.\n\nThe plugin will try to maintain this target as best it can, but heavy CPU load or many visible labels may increase delays.", 3, 1000)]
        public RangeNode<int> ClickFrequencyTarget { get; set; } = new RangeNode<int>(80, 80, 250);
        [Menu("Chest Height Offset", "If you're experiencing a lot of missclicking for chests specifically (clicking too high or low),\n" +
            "change this value. If you're clicking too high, lower the value, if you're clicking too low, raise the value", 4, 1000)]
        public RangeNode<int> ChestHeightOffset { get; set; } = new RangeNode<int>(0, -100, 100);

        // ----- Controls -----
        [Menu("Controls", 1100)]
        public EmptyNode InputAndSafetyCategory { get; set; } = new EmptyNode();
        [Menu("Block when Left or Right Panel open", "Prevent clicks when the inventory or character screen are open", 1, 1100)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);
        [Menu("Verify cursor is within game window before clicking", "When enabled the plugin will verify the OS cursor is inside the Path of Exile window before performing any automated clicks. If the cursor is outside the window the click will be skipped.", 2, 1100)]
        public ToggleNode VerifyCursorInGameWindowBeforeClick { get; set; } = new ToggleNode(true);
        [Menu("Left-handed", "Changes the primary mouse button the plugin uses from left to right.", 3, 1100)]
        public ToggleNode LeftHanded { get; set; } = new ToggleNode(false);
        [Menu("Toggle Item View", "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels", 4, 1100)]
        public ToggleNode ToggleItems { get; set; } = new ToggleNode(true);
        [Menu("Toggle Items Hotkey", "Hotkey to toggle the display of ground items / labels", 5, 1100)]
        public HotkeyNode ToggleItemsHotkey { get; set; } = new HotkeyNode(Keys.Z);
        [Menu("Toggle Item View Interval (ms)", "How often Toggle Item View is allowed to trigger.\n1000 ms = 1 second.", 6, 1100)]
        public RangeNode<int> ToggleItemsIntervalMs { get; set; } = new RangeNode<int>(1500, 500, 10000);
        [Menu("Disable Clicking After Toggle Items (ms)", "Temporarily blocks further clicks after Toggle Item View triggers.\n\nIncrease this if clicks right after toggling are clicking incorrect labels.", 7, 1100)]
        public RangeNode<int> ToggleItemsPostToggleClickBlockMs { get; set; } = new RangeNode<int>(20, 0, 250);
        [Menu("UIHover Verification (non-lazy)", "When enabled, the plugin verifies UIHover before clicking while NOT in Lazy Mode.\n\nThis extra verification step can make clicking slower and less frequent, however, enabling this helps prevent accidentally picking up blacklisted items.\n\nI'd recommend keeping this disabled unless you frequently encounter issues with blacklisted items being picked up.", 8, 1100)]
        public ToggleNode VerifyUIHoverWhenNotLazy { get; set; } = new ToggleNode(false);
        [Menu("Avoid overlapping labels when clicking", "When enabled, the plugin attempts to click a visible, non-overlapped part of the target label instead of always clicking center. Helps when one label partially covers another.", 9, 1100)]
        public ToggleNode AvoidOverlappingLabelClickPoints { get; set; } = new ToggleNode(true);

        // ----- Lazy Mode -----
        [Menu("Lazy Mode", 1200)]
        public EmptyNode LazyModeCategory { get; set; } = new EmptyNode();
        [Menu("Lazy Mode - IMPORTANT INFO IN TOOLTIP ->", "Will automatically click most things for you, without you needing to hold the key.\n\nThere are inherent limitations to this feature that cannot be fixed:\n\n-> If you are holding down a skill, for instance, Cyclone, you cannot interact with most things in the game.\n   If you use a skill that requires you to hold a key, you must set it to left or right click and enable\n   the 'disable lazy mode while x click held' setting below for lazy mode to function correctly.\n\n-> The plugin cannot detect when a chest becomes unlocked, or if a settlers tree has been activated.\n   This is a limitation with exileapi and not the plugin and for this reason, Lazy Mode is not allowed\n   to click chests that were locked when spawned or the settlers tree. When one of these is on-screen,\n   Lazy Mode will be temporarily disabled, until the blacklisted item is off of the screen, which will\n   allow you to manually press the hotkey to click these items specifically if you want to.\n\n-> This will take control away from you at crucial moments, potentially causing you to die.\n\nHolding the click items hotkey you have set in Controls will override lazy mode blocking.", 1, 1200)]
        public ToggleNode LazyMode { get; set; } = new ToggleNode(false);
        [Menu("Click Limiting (ms)", "When Lazy Mode is enabled, this sets the minimum delay (in milliseconds)\nthat must pass between consecutive clicks performed by the plugin.\nThis limiter applies to all automated clicks (shrines, altars, strongboxes, etc.)\nonly while Lazy Mode is active. Increase this value to reduce click spam and\nprevent the plugin from taking control away from you.", 2, 1200)]
        public RangeNode<int> LazyModeClickLimiting { get; set; } = new RangeNode<int>(80, 80, 1000);
        [Menu("Disable Hotkey", "When Lazy Mode is enabled and active, holding this key will temporarily disable lazy mode clicking.\nThis allows you to pause automated clicking without disabling lazy mode entirely.", 3, 1200)]
        public HotkeyNode LazyModeDisableKey { get; set; } = new HotkeyNode(Keys.F2);
        [Menu("Disable Hotkey Toggle Mode", "When enabled, pressing the Disable Hotkey toggles lazy mode clicking on/off until you press it again.\nWhen disabled, the hotkey works as hold-to-disable.", 4, 1200)]
        public ToggleNode LazyModeDisableKeyToggleMode { get; set; } = new ToggleNode(false);
        [Menu("Restore cursor position after each click", "When enabled, restores cursor to original position after clicking in lazy mode.", 5, 1200)]
        public ToggleNode RestoreCursorInLazyMode { get; set; } = new ToggleNode(true);
        [Menu("Restore Cursor Delay (ms)", "Delay before restoring cursor position after a lazy-mode click when cursor restore is enabled.\n\nWhen set below 20, this may cause the plugin to have to click an item multiple times to pick it up.", 6, 1200)]
        public RangeNode<int> LazyModeRestoreCursorDelayMs { get; set; } = new RangeNode<int>(20, 0, 40);
        [Menu("Item Hover Sleep (ms)", "Sleep duration before UIHover verification in lazy mode.\nIncrease if you notice the mouse moving and not successfully clicking on things when it should.\n\nA value of 20 is recommended.", 7, 1200)]
        public RangeNode<int> LazyModeUIHoverSleep { get; set; } = new RangeNode<int>(20, 20, 40);
        [Menu("Disable lazy mode while left click held", "When enabled, holding left mouse button will disable lazy mode auto-clicking.", 8, 1200)]
        public ToggleNode DisableLazyModeLeftClickHeld { get; set; } = new ToggleNode(true);
        [Menu("Disable lazy mode while right click held", "When enabled, holding right mouse button will disable lazy mode auto-clicking.", 9, 1200)]
        public ToggleNode DisableLazyModeRightClickHeld { get; set; } = new ToggleNode(true);
        [Menu("Lever Reclick Delay (ms)", "When Lazy Mode is enabled, prevents repeatedly clicking the same lever too quickly.\nIncrease this value if a lever is being clicked repeatedly.", 10, 1200)]
        public RangeNode<int> LazyModeLeverReclickDelay { get; set; } = new RangeNode<int>(10000, 10000, 30000);


        // ----- Priorities -----
        [Menu("Priorities", 1450)]
        public EmptyNode PrioritiesCategory { get; set; } = new EmptyNode();

        [Menu("Priority Distance Penalty", "Applies an extra distance cost per lower-priority row when comparing non-ignored mechanics.\n\nHigher values make table order matter more while still considering distance.\n\nSetting this to 0 will effectively disable the priorities feature, however, Ignore Distance values will still be respected.\n\nWhen priorities are disabled, distance will be the only factor considered in what to click.", 2, 1450)]
        public RangeNode<int> MechanicPriorityDistancePenalty { get; set; } = new RangeNode<int>(25, 0, 100);

        [Menu("Mechanic Priority Table", "", 1, 1450)]
        [JsonIgnore]
        public CustomNode MechanicPriorityTablePanel { get; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> MechanicPriorityOrder { get; set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> MechanicPriorityIgnoreDistanceIds { get; set; } = new(PriorityComparer);

        private string _expandedMechanicPriorityRowId = string.Empty;

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
        [Menu("Labyrinth Trials", "Click labyrinth trial portals", 5, 1300)]
        public ToggleNode ClickLabyrinthTrials { get; set; } = new ToggleNode(false);
        [Menu("Crafting Recipes", "Click crafting recipes", 6, 1300)]
        public ToggleNode ClickCraftingRecipes { get; set; } = new ToggleNode(true);
        [Menu("Doors", "Click doors", 7, 1300)]
        public ToggleNode ClickDoors { get; set; } = new ToggleNode(false);
        [Menu("Levers", "Click levers", 8, 1300)]
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
        [Menu("Click Initial Ultimatum", "Click the first Ultimatum interaction from the ground label, then click Begin using configured modifier priority.", 1, 1700)]
        public ToggleNode ClickInitialUltimatum { get; set; } = new ToggleNode(false);
        [Menu("Click Ultimatum Choices", "Click later Ultimatum panel choices/confirm interactions using configured modifier priority.", 2, 1700)]
        public ToggleNode ClickUltimatumChoices { get; set; } = new ToggleNode(false);
        [Menu("Show Option Overlay", "Draws outlines on Ultimatum options: green for the selected option and priority colors for the other options.", 3, 1700)]
        public ToggleNode ShowUltimatumOptionOverlay { get; set; } = new ToggleNode(true);
        [Menu("Modifier Priority Table", "", 4, 1700)]
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
        private string[] _ultimatumPrioritySnapshot = [];
        private string[] _mechanicPrioritySnapshot = [];
        private string[] _mechanicIgnoreDistanceSnapshot = [];
        public ClickItSettings()
        {
            InitializeDefaultWeights();
            EnsureItemTypeFiltersInitialized();
            EnsureMechanicPrioritiesInitialized();
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
            MechanicPriorityTablePanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("MechanicPriorityTablePanel", DrawMechanicPriorityTablePanel)
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
