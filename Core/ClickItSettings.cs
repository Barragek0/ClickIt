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
        private const int MechanicIgnoreDistanceWithinDefault = 100;
        private const int MechanicIgnoreDistanceWithinMin = 10;
        private const int MechanicIgnoreDistanceWithinMax = 500;
        private static readonly StringComparer PriorityComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly Vector4 WhitelistTextColor = new(0.4f, 0.8f, 0.4f, 1.0f);
        private static readonly Vector4 BlacklistTextColor = new(0.8f, 0.4f, 0.4f, 1.0f);

        public ToggleNode Enable { get; set; } = new ToggleNode(true);

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
        [Menu("Additional Debug Information", "Provides more debug text related to rendering the overlay.", 2, 900)]
        public ToggleNode RenderDebug { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Auto-Copy Additional Debug Information", "Automatically copies the current Additional Debug Information text to clipboard.", 3, 900)]
        public ToggleNode AutoCopyAdditionalDebugInfoToClipboard { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Auto-Copy Interval (ms)", "Minimum delay between clipboard updates when auto-copy is enabled.", 4, 900)]
        public RangeNode<int> AutoCopyAdditionalDebugInfoIntervalMs { get; set; } = new RangeNode<int>(1000, 250, 10000);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Status", "Show/hide the status debug section", 1, 2)]
        public ToggleNode DebugShowStatus { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Game State", "Show/hide the Game State debug section", 2, 2)]
        public ToggleNode DebugShowGameState { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Performance", "Show/hide the performance debug section", 3, 2)]
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
        [Menu("Labels", "Show/hide the labels debug section", 7, 2)]
        public ToggleNode DebugShowLabels { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Hovered Item Metadata", "Show/hide the hovered item metadata debug section", 8, 2)]
        public ToggleNode DebugShowHoveredItemMetadata { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Pathfinding", "Show/hide offscreen pathfinding debug section", 9, 2)]
        public ToggleNode DebugShowPathfinding { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Recent Errors", "Show/hide the Recent Errors debug section", 10, 2)]
        public ToggleNode DebugShowRecentErrors { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Debug Frames", "Show/hide the debug screen area frames", 11, 2)]
        public ToggleNode DebugShowFrames { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Log messages", "This will flood your log and screen with debug text.", 5, 900)]
        public ToggleNode LogMessages { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Report Bug", "If you run into a bug that hasn't already been reported, please report it here.", 6, 900)]
        public ButtonNode ReportBugButton { get; set; } = new ButtonNode();



        [Menu("Controls", 1100)]

        public EmptyNode Click { get; set; } = new EmptyNode();
        [Menu("Click Hotkey", "Held hotkey to start clicking", 1, 1100)]

        [Obsolete("Can be safely ignored for now.")]

        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);
        [Menu("", 10001, 1100)]
        [JsonIgnore]
        public CustomNode ControlsSliderWidthStart { get; }
        [Menu("Search Radius", "Radius the plugin will search in for interactable objects. A value of 100 is recommended for 1080p, though, you may need to increase this on higher resolutions.", 2, 1100)]

        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(100, 0, 300);
        [Menu("Click Frequency Target (ms)", "Target milliseconds between clicks for non-altar/shrine actions. Higher = less frequent clicks.\n\nThe plugin will try to maintain this target as best it can, but heavy CPU load or many visible labels may increase delays.", 3, 1100)]

        public RangeNode<int> ClickFrequencyTarget { get; set; } = new RangeNode<int>(80, 80, 250);
        [Menu("Chest Height Offset", "If you're experiencing a lot of missclicking for chests specifically (clicking too high or low),\n" +
            "change this value. If you're clicking too high, lower the value, if you're clicking too low, raise the value", 4, 1100)]

        public RangeNode<int> ChestHeightOffset { get; set; } = new RangeNode<int>(0, -100, 100);

        public EmptyNode InputAndSafetyCategory { get; set; } = new EmptyNode();
        [Menu("Block when Left or Right Panel open", "Prevent clicks when the inventory or character screen are open", 5, 1100)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);
        [Menu("Verify Cursor is within Game Window before Clicking", "When enabled, the plugin will verify the OS cursor is inside the Path of Exile window before performing any automated clicks. If the cursor is outside the window, the click will be skipped.", 6, 1100)]
        public ToggleNode VerifyCursorInGameWindowBeforeClick { get; set; } = new ToggleNode(true);
        [Menu("Left-handed", "Changes the primary mouse button the plugin uses from left to right.", 7, 1100)]
        public ToggleNode LeftHanded { get; set; } = new ToggleNode(false);
        [Menu("Toggle Item View", "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels.", 8, 1100)]
        public ToggleNode ToggleItems { get; set; } = new ToggleNode(true);
        [Menu("Toggle Items Hotkey", "Hotkey to toggle the display of ground items / labels.", 9, 1100)]
        public HotkeyNode ToggleItemsHotkey { get; set; } = new HotkeyNode(Keys.Z);
        [Menu("Toggle Item View Interval (ms)", "How often Toggle Item View is allowed to trigger.\n1000 ms = 1 second.", 10, 1100)]
        public RangeNode<int> ToggleItemsIntervalMs { get; set; } = new RangeNode<int>(1500, 500, 10000);
        [Menu("Disable Clicking after Toggle Items (ms)", "Temporarily blocks further clicks after Toggle Item View triggers.\n\nIncrease this if clicks right after toggling are clicking incorrect labels.", 11, 1100)]
        public RangeNode<int> ToggleItemsPostToggleClickBlockMs { get; set; } = new RangeNode<int>(20, 0, 250);
        [Menu("", 10002, 1100)]
        [JsonIgnore]
        public CustomNode ControlsSliderWidthEnd { get; }
        [Menu("UIHover Verification (non-lazy)", "When enabled, the plugin verifies UIHover before clicking while not in Lazy Mode.\n\nThis extra verification step can make clicking slower and less frequent, however, enabling this helps prevent accidentally picking up blacklisted items.\n\nI'd recommend keeping this disabled unless you frequently encounter issues with blacklisted items being picked up.", 12, 1100)]
        public ToggleNode VerifyUIHoverWhenNotLazy { get; set; } = new ToggleNode(false);
        [Menu("Avoid Overlapping Labels when Clicking", "When enabled, the plugin attempts to click a visible, non-overlapped part of the target label instead of always clicking center. Helps when one label partially covers another.", 13, 1100)]
        public ToggleNode AvoidOverlappingLabelClickPoints { get; set; } = new ToggleNode(true);
        [Menu("Pathfinding", 14, 1100)]
        public EmptyNode PathfindingCategory { get; set; } = new EmptyNode();
        [Menu("Walk toward Offscreen Labels", "When enabled and no clickable labels are on screen, attempt to walk toward the nearest offscreen interactable target using terrain pathfinding data.\n\nI would be careful enabling this feature as its somewhat likely GGG could flag you as a bot.\n\nWhile that hasn't happen to me while testing the feature, I wouldn't be surprised if it did happen during prolonged use.", 1, 14)]
        public ToggleNode WalkTowardOffscreenLabels { get; set; } = new ToggleNode(false);
        [Menu("", 10003, 14)]
        [JsonIgnore]
        public CustomNode PathfindingSliderWidthStart { get; }
        [Menu("Offscreen Pathfinding Search Budget", "Controls pathfinding search complexity for offscreen walking. Higher values search deeper but increase CPU usage.", 2, 14)]
        public RangeNode<int> OffscreenPathfindingSearchBudget { get; set; } = new RangeNode<int>(6000, 1000, 50000);
        [Menu("Offscreen Path Line Timeout (ms)", "Maximum age of the red pathfinding line. If pathfinding has not run within this timeout, the line is automatically cleared.", 3, 14)]
        public RangeNode<int> OffscreenPathfindingLineTimeoutMs { get; set; } = new RangeNode<int>(1500, 250, 10000);
        [Menu("Use Movement Skills for Offscreen Pathfinding", "When enabled, the plugin will attempt to use an equipped movement skill keybind while pathing to offscreen targets. Supports common travel/blink gems when they are off cooldown and have a keyboard keybind.", 4, 14)]
        public ToggleNode UseMovementSkillsForOffscreenPathfinding { get; set; } = new ToggleNode(false);
        [Menu("Movement Skill Minimum Path Subsection Length", "Minimum remaining path node count required before a movement skill cast is attempted. Lower values cast more often; higher values are more conservative.", 5, 14)]
        public RangeNode<int> OffscreenMovementSkillMinPathSubsectionLength { get; set; } = new RangeNode<int>(8, 1, 100);
        [Menu("Shield Charge Post-Cast Delay (ms)", "Delay before normal clicking resumes after Shield Charge is used for offscreen pathing. Lower values cast/recover faster; higher values are safer for slower attack speed setups.", 6, 14)]
        public RangeNode<int> OffscreenShieldChargePostCastClickDelayMs { get; set; } = new RangeNode<int>(100, 0, 1000);
        [Menu("", 10004, 14)]
        [JsonIgnore]
        public CustomNode PathfindingSliderWidthEnd { get; }

        [Menu("Lazy Mode", 15, 1100)]
        public EmptyNode LazyModeCategory { get; set; } = new EmptyNode();
        [Menu("Lazy Mode - Important Info in Tooltip ->", "Will automatically click most things for you, without you needing to hold the key.\n\nThere are inherent limitations to this feature that cannot be fixed:\n\n-> If you are holding down a skill, for instance, Cyclone, you cannot interact with most things in the game.\n   If you use a skill that requires you to hold a key, you must set it to left or right click and enable\n   the 'Disable Lazy Mode while Left Click Held' or 'Disable Lazy Mode while Right Click Held' setting below for lazy mode to function correctly.\n\n-> The plugin cannot detect when a chest becomes unlocked, or if a settlers tree has been activated.\n   This is a limitation with ExileAPI and not the plugin and for this reason, lazy mode is not allowed\n   to click chests that were locked when spawned or the settlers tree. When one of these is on-screen,\n   lazy mode will be temporarily disabled, until the blacklisted item is off of the screen, which will\n   allow you to manually press the hotkey to click these items specifically if you want to.\n\n-> This will take control away from you at crucial moments, potentially causing you to die.\n\nHolding the click items hotkey you have set in Controls will override lazy mode blocking.", 1, 15)]
        public ToggleNode LazyMode { get; set; } = new ToggleNode(false);
        [Menu("", 10005, 15)]
        [JsonIgnore]
        public CustomNode LazyModeSliderWidthStart { get; }
        [Menu("Click Limiting (ms)", "When lazy mode is enabled, this sets the minimum delay (in milliseconds)\nthat must pass between consecutive clicks performed by the plugin.\nThis limiter applies to all automated clicks (shrines, altars, strongboxes, etc.)\nonly while lazy mode is active. Increase this value to reduce click spam and\nprevent the plugin from taking control away from you.", 2, 15)]
        public RangeNode<int> LazyModeClickLimiting { get; set; } = new RangeNode<int>(80, 80, 1000);
        [Menu("Disable Hotkey", "When lazy mode is enabled and active, holding this key will temporarily disable lazy mode clicking.\nThis allows you to pause automated clicking without disabling lazy mode entirely.", 3, 15)]
        public HotkeyNode LazyModeDisableKey { get; set; } = new HotkeyNode(Keys.F2);
        [Menu("Disable Hotkey Toggle Mode", "When enabled, pressing the Disable Hotkey toggles lazy mode clicking on/off until you press it again.\nWhen disabled, the hotkey works as hold-to-disable.", 4, 15)]
        public ToggleNode LazyModeDisableKeyToggleMode { get; set; } = new ToggleNode(false);
        [Menu("Restore Cursor Position after Each Click", "When enabled, restores cursor to original position after clicking in lazy mode.", 5, 15)]
        public ToggleNode RestoreCursorInLazyMode { get; set; } = new ToggleNode(true);
        [Menu("Restore Cursor Delay (ms)", "Delay before restoring cursor position after a lazy-mode click when cursor restore is enabled.\n\nWhen set below 20, this may cause the plugin to have to click an item multiple times to pick it up.", 6, 15)]
        public RangeNode<int> LazyModeRestoreCursorDelayMs { get; set; } = new RangeNode<int>(20, 0, 40);
        [Menu("Item Hover Sleep (ms)", "Sleep duration before UIHover verification in lazy mode.\nIncrease if you notice the mouse moving and not successfully clicking on things when it should.\n\nA value of 20 is recommended.", 7, 15)]
        public RangeNode<int> LazyModeUIHoverSleep { get; set; } = new RangeNode<int>(20, 20, 40);
        [Menu("Disable Lazy Mode while Left Click Held", "When enabled, holding left mouse button will disable lazy mode auto-clicking.", 8, 15)]
        public ToggleNode DisableLazyModeLeftClickHeld { get; set; } = new ToggleNode(true);
        [Menu("Disable Lazy Mode while Right Click Held", "When enabled, holding right mouse button will disable lazy mode auto-clicking.", 9, 15)]
        public ToggleNode DisableLazyModeRightClickHeld { get; set; } = new ToggleNode(true);
        [Menu("Lever Reclick Delay (ms)", "When lazy mode is enabled, prevents repeatedly clicking the same lever too quickly.\nIncrease this value if a lever is being clicked repeatedly.", 10, 15)]
        public RangeNode<int> LazyModeLeverReclickDelay { get; set; } = new RangeNode<int>(10000, 10000, 30000);
        [Menu("", 10006, 15)]
        [JsonIgnore]
        public CustomNode LazyModeSliderWidthEnd { get; }
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> MechanicPriorityOrder { get; set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> MechanicPriorityIgnoreDistanceIds { get; set; } = new(PriorityComparer);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, int> MechanicPriorityIgnoreDistanceWithinById { get; set; } = new(PriorityComparer);

        private string _expandedMechanicPriorityRowId = string.Empty;
        private string _expandedMechanicsTableRowId = string.Empty;

        [Menu("Mechanics", 1400)]
        public EmptyNode WorldInteractionsCategory { get; set; } = new EmptyNode();
        [Menu("", 1, 1400)]
        [JsonIgnore]
        public CustomNode MechanicsTablePanel { get; }
        [JsonIgnore]
        public bool ShowRawMechanicNodesInSettings => false;
        [Menu("Basic Chests", "Click normal (non-league related) chests.", 1, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickBasicChests { get; set; } = new ToggleNode(false);
        [Menu("League Mechanic 'Chests'", "Click league mechanic related 'chests' (blight pustules, legion war hoards / chests, sentinel caches, etc).", 2, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickLeagueChests { get; set; } = new ToggleNode(true);
        [Menu("Shrines", "Click shrines", 3, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickShrines { get; set; } = new ToggleNode(true);
        [Menu("Lost Shipment", "Click Lost Shipment crates.", 3, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickLostShipmentCrates { get; set; } = new ToggleNode(true);
        [Menu("Area Transitions", "Click area transitions.", 4, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(false);
        [Menu("Labyrinth Trials", "Click labyrinth trial portals.", 5, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickLabyrinthTrials { get; set; } = new ToggleNode(false);
        [Menu("Crafting Recipes", "Click crafting recipes.", 6, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickCraftingRecipes { get; set; } = new ToggleNode(true);
        [Menu("Doors", "Click doors", 7, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickDoors { get; set; } = new ToggleNode(false);
        [Menu("Levers", "Click levers", 8, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickLevers { get; set; } = new ToggleNode(false);

        [JsonIgnore]
        public EmptyNode Mechanics { get; set; } = new EmptyNode();
        [Menu("Alva Temple Doors", "Click Alva Temple Doors.", 9, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickAlvaTempleDoors { get; set; } = new ToggleNode(true);
        [Menu("Betrayal", "Click betrayal labels", 10, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickBetrayal { get; set; } = new ToggleNode(false);
        [Menu("Blight", "Click blight pumps", 11, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickBlight { get; set; } = new ToggleNode(true);
        [Menu("Breach Nodes", "Click breach nodes.", 12, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickBreachNodes { get; set; } = new ToggleNode(false);
        [Menu("Legion Pillars", "Click legion encounter pillars.", 13, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickLegionPillars { get; set; } = new ToggleNode(true);
        [Menu("Nearest Harvest Plot", "Click nearest harvest plot.", 14, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode NearestHarvest { get; set; } = new ToggleNode(true);
        [Menu("Sanctum", "Click sanctum related stuff", 15, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickSanctum { get; set; } = new ToggleNode(true);
        [Menu("Settlers Ore Deposits", "Click settlers league ore deposits (CrimsonIron, Orichalcum, etc).\n\nThere is a known issue with this feature meaning the plugin will repeatedly try to click on trees that have already been activated.\n\nI don't currently think there is any way to fix this due to limitations with the game memory and ExileAPI.", 16, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickSettlersOre { get; set; } = new ToggleNode(true);
        [Menu("Items", "Click items", 30, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);
        [Menu("Essences", "Click essences", 31, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);
        [Menu("Click Initial Ultimatum", "Click the first Ultimatum interaction from the ground label, then click Begin using configured modifier priority.", 32, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickInitialUltimatum { get; set; } = new ToggleNode(false);
        [Menu("Click Ultimatum Choices", "Click later Ultimatum panel choices/confirm interactions using configured modifier priority.", 33, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickUltimatumChoices { get; set; } = new ToggleNode(false);
        [Menu("Searing Exarch", "Clicks searing exarch altars for you based on a decision tree created from your settings.\n\nIf both options are as good as each other (according to your weights), this won't click for you.", 34, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickExarchAltars { get; set; } = new ToggleNode(false);
        [Menu("Eater of Worlds", "Clicks eater of worlds altars for you based on a decision tree created from your settings.\n\nIf both options are as good as each other (according to your weights), this won't click for you.", 35, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickEaterAltars { get; set; } = new ToggleNode(false);
        [Menu("Ritual (Initiate)", "Click ritual altars that have not been completed yet.", 36, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickRitualInitiate { get; set; } = new ToggleNode(true);
        [Menu("Ritual (Completed)", "Click ritual altars that have been completed.", 37, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickRitualCompleted { get; set; } = new ToggleNode(true);
        [Menu("Azurite Veins", "Click azurite veins.", 38, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickAzuriteVeins { get; set; } = new ToggleNode(true);
        [Menu("Sulphite Veins", "Click sulphite veins.", 39, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickSulphiteVeins { get; set; } = new ToggleNode(true);
        [Menu("Encounter Initiators", "Click delve encounter initiators.", 40, 1400)]
        [ConditionalDisplay(nameof(ShowRawMechanicNodesInSettings))]
        public ToggleNode ClickDelveSpawners { get; set; } = new ToggleNode(true);

        [Menu("Priorities", 24, 1400)]
        public EmptyNode PrioritiesCategory { get; set; } = new EmptyNode();

        [Menu("", 10007, 24)]
        [JsonIgnore]
        public CustomNode PrioritiesSliderWidthStart { get; }
        [Menu("Priority Distance Penalty", "Applies an extra distance cost per lower-priority row when comparing non-ignored mechanics.\n\nHigher values make table order matter more while still considering distance.\n\nSetting this to 0 will effectively disable the priorities feature, however, ignore distance values will still be respected.\n\nWhen priorities are disabled, distance will be the only factor considered in what to click.", 1, 24)]
        public RangeNode<int> MechanicPriorityDistancePenalty { get; set; } = new RangeNode<int>(25, 0, 100);

        [Menu("", 2, 24)]
        [JsonIgnore]
        public CustomNode MechanicPriorityTablePanel { get; }
        [Menu("", 10008, 24)]
        [JsonIgnore]
        public CustomNode PrioritiesSliderWidthEnd { get; }

        [Menu("Items", 17, 1400)]
        public EmptyNode ItemPickupCategory { get; set; } = new EmptyNode();

        [Menu("", 2, 17)]
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

        [Menu("Essences", 18, 1400)]
        public EmptyNode Essences { get; set; } = new EmptyNode();
        [Menu("", 2, 18)]
        [JsonIgnore]
        public CustomNode EssenceCorruptionTablePanel { get; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> EssenceCorruptNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> EssenceDontCorruptNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [Menu("Ultimatum", 19, 1400)]
        public EmptyNode Ultimatum { get; set; } = new EmptyNode();
        [Menu("Show Option Overlay", "Draws outlines on Ultimatum options: green for the selected option and priority colors for the other options.", 3, 19)]
        public ToggleNode ShowUltimatumOptionOverlay { get; set; } = new ToggleNode(true);
        [Menu("", 4, 19)]
        [JsonIgnore]
        public CustomNode UltimatumModifierTablePanel { get; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> UltimatumModifierPriority { get; set; } = new();

        [Menu("Strongboxes", 20, 1400)]
        public EmptyNode Strongboxes { get; set; } = new EmptyNode();
        [Menu("Show Strongbox Overlay", "When enabled, draws a visual frame around strongboxes indicating whether or not they are locked.", 1, 20)]
        public ToggleNode ShowStrongboxFrames { get; set; } = new ToggleNode(true);
        [Menu("", 2, 20)]
        [JsonIgnore]
        public CustomNode StrongboxFilterTablePanel { get; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> StrongboxClickIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> StrongboxDontClickIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [Menu("Altars", 21, 1400)]
        public EmptyNode AltarsCategory { get; set; } = new EmptyNode();

        [Menu("Settings", 1, 21)]
        [JsonIgnore]
        public CustomNode AltarsPanel { get; }

        [JsonIgnore]
        public bool ShowRawAltarNodesInSettings => false;

        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public EmptyNode ExarchAltar { get; set; } = new EmptyNode();
        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public ToggleNode HighlightExarchAltars { get; set; } = new ToggleNode(true);

        [ConditionalDisplay(nameof(ShowRawAltarNodesInSettings))]
        public EmptyNode EaterAltar { get; set; } = new EmptyNode();
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

        [Menu("Ritual", 22, 1400)]
        public EmptyNode Ritual { get; set; } = new EmptyNode();

        [Menu("Delve", 23, 1400)]
        public EmptyNode Delve { get; set; } = new EmptyNode();
        [Menu("Flares", "Use flares when all of these conditions are true:\n\n-> Your darkness debuff stacks are at least the 'Darkness Debuff Stacks' value.\n-> Your health is below the 'Use flare below Health' value.\n-> Your energy shield is below the 'Use flare below Energy Shield' value.\n\nIf you're playing CI and have 1 max life, set Health to 100.\n\nIf you have no energy shield, set Energy Shield to 100.", 4, 23)]
        public ToggleNode ClickDelveFlares { get; set; } = new ToggleNode(false);
        [Menu("Flare Hotkey", "Set this to your in-game keybind for flares. The plugin will press this button to use a flare.", 5, 23)]
        public HotkeyNode DelveFlareHotkey { get; set; } = new HotkeyNode(Keys.D6);
        [Menu("", 10009, 23)]
        [JsonIgnore]
        public CustomNode DelveSliderWidthStart { get; }
        [Menu("Darkness Debuff Stacks", 6, 23)]
        public RangeNode<int> DarknessDebuffStacks { get; set; } = new RangeNode<int>(5, 1, 10);
        [Menu("Flare Health %", 7, 23)]
        public RangeNode<int> DelveFlareHealthThreshold { get; set; } = new RangeNode<int>(75, 2, 100);
        [Menu("Flare Energy Shield %", 8, 23)]
        public RangeNode<int> DelveFlareEnergyShieldThreshold { get; set; } = new RangeNode<int>(75, 2, 100);
        [Menu("", 10010, 23)]
        [JsonIgnore]
        public CustomNode DelveSliderWidthEnd { get; }

        private string upsideSearchFilter = "";
        private string downsideSearchFilter = "";
        private string itemTypeSearchFilter = "";
        private string essenceSearchFilter = "";
        private string strongboxSearchFilter = "";
        private string mechanicsSearchFilter = "";
        private string ultimatumSearchFilter = "";
        private string _lastSettingsUiError = string.Empty;
        private string[] _ultimatumPrioritySnapshot = [];
        private string[] _mechanicPrioritySnapshot = [];
        private string[] _mechanicIgnoreDistanceSnapshot = [];
        private KeyValuePair<string, int>[] _mechanicIgnoreDistanceWithinSnapshot = [];
        private IReadOnlyDictionary<string, int> _mechanicIgnoreDistanceWithinMapSnapshot = new Dictionary<string, int>(PriorityComparer);
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
            ControlsSliderWidthStart = new CustomNode
            {
                DrawDelegate = PushStandardSliderWidth
            };
            ControlsSliderWidthEnd = new CustomNode
            {
                DrawDelegate = PopStandardSliderWidth
            };
            PathfindingSliderWidthStart = new CustomNode
            {
                DrawDelegate = PushStandardSliderWidth
            };
            PathfindingSliderWidthEnd = new CustomNode
            {
                DrawDelegate = PopStandardSliderWidth
            };
            LazyModeSliderWidthStart = new CustomNode
            {
                DrawDelegate = PushStandardSliderWidth
            };
            LazyModeSliderWidthEnd = new CustomNode
            {
                DrawDelegate = PopStandardSliderWidth
            };
            PrioritiesSliderWidthStart = new CustomNode
            {
                DrawDelegate = PushStandardSliderWidth
            };
            PrioritiesSliderWidthEnd = new CustomNode
            {
                DrawDelegate = PopStandardSliderWidth
            };
            DelveSliderWidthStart = new CustomNode
            {
                DrawDelegate = PushStandardSliderWidth
            };
            DelveSliderWidthEnd = new CustomNode
            {
                DrawDelegate = PopStandardSliderWidth
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
            MechanicsTablePanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("MechanicsTablePanel", DrawMechanicsTablePanel)
            };
            UltimatumModifierTablePanel = new CustomNode
            {
                DrawDelegate = () => DrawPanelSafe("UltimatumModifierTablePanel", DrawUltimatumModifierTablePanel)
            };
        }

    }
}
