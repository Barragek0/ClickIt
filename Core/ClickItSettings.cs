using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using System.Numerics;
using ClickIt.Constants;

namespace ClickIt
{
    public class ClickItSettings : ISettings
    {
        private const string AltarTypeMinion = "Minion";
        private const string AltarTypeBoss = "Boss";
        private const string AltarTypePlayer = "Player";

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
        [Menu("Controls", 1050)]
        public EmptyNode InputAndSafetyCategory { get; set; } = new EmptyNode();
        [Menu("Block when Left or Right Panel open", "Prevent clicks when the inventory or character screen are open", 1, 1050)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);
        [Menu("Verify cursor is within game window before clicking", "When enabled the plugin will verify the OS cursor is inside the Path of Exile window before performing any automated clicks. If the cursor is outside the window the click will be skipped.", 2, 1050)]
        public ToggleNode VerifyCursorInGameWindowBeforeClick { get; set; } = new ToggleNode(true);
        [Menu("Left-handed", "Changes the primary mouse button the plugin uses from left to right.", 3, 1050)]
        public ToggleNode LeftHanded { get; set; } = new ToggleNode(false);
        [Menu("Toggle Item View", "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels", 4, 1050)]
        public ToggleNode ToggleItems { get; set; } = new ToggleNode(true);
        [Menu("UIHover Verification (non-lazy)", "When enabled, the plugin verifies UIHover before clicking while NOT in Lazy Mode.\n\nThis extra verification step can make clicking slower and less frequent, however, enabling this helps prevent accidentally picking up blacklisted items.\n\nI'd recommend keeping this disabled unless you frequently encounter issues with blacklisted items being picked up.", 5, 1050)]
        public ToggleNode VerifyUIHoverWhenNotLazy { get; set; } = new ToggleNode(false);
        [Menu("Toggle Items Hotkey", "Hotkey to toggle the display of ground items / labels", 6, 1050)]
        public HotkeyNode ToggleItemsHotkey { get; set; } = new HotkeyNode(Keys.Z);

        // ----- Lazy Mode -----
        [Menu("Lazy Mode", 1075)]
        public EmptyNode LazyModeCategory { get; set; } = new EmptyNode();
        [Menu("Lazy Mode - IMPORTANT INFO IN TOOLTIP ->", "Will automatically click most things for you, without you needing to hold the key.\n\nThere are inherent limitations to this feature that cannot be fixed:\n\n-> If you are holding down a skill, for instance, Cyclone, you cannot interact with most things in the game.\n   If you use a skill that requires you to hold a key, you must set it to left or right click and enable\n   the 'disable lazy mode while x click held' setting below for lazy mode to function correctly.\n\n-> The plugin cannot detect when a chest becomes unlocked, or if a settlers tree has been activated.\n   This is a limitation with exileapi and not the plugin and for this reason, Lazy Mode is not allowed\n   to click chests that were locked when spawned or the settlers tree. When one of these is on-screen,\n   Lazy Mode will be temporarily disabled, until the blacklisted item is off of the screen, which will\n   allow you to manually press the hotkey to click these items specifically if you want to.\n\n-> This will take control away from you at crucial moments, potentially causing you to die.\n\nHolding the click items hotkey you have set in Controls will override lazy mode blocking.", 1, 1075)]
        public ToggleNode LazyMode { get; set; } = new ToggleNode(false);
        [Menu("Click Limiting (ms)", "When Lazy Mode is enabled, this sets the minimum delay (in milliseconds)\nthat must pass between consecutive clicks performed by the plugin.\nThis limiter applies to all automated clicks (shrines, altars, strongboxes, etc.)\nonly while Lazy Mode is active. Increase this value to reduce click spam and\nprevent the plugin from taking control away from you.", 2, 1075)]
        public RangeNode<int> LazyModeClickLimiting { get; set; } = new RangeNode<int>(80, 80, 1000);
        [Menu("Disable Hotkey", "When Lazy Mode is enabled and active, holding this key will temporarily disable lazy mode clicking.\nThis allows you to pause automated clicking without disabling lazy mode entirely.", 3, 1075)]
        public HotkeyNode LazyModeDisableKey { get; set; } = new HotkeyNode(Keys.F2);
        [Menu("Restore cursor position after each click", "When enabled, restores cursor to original position after clicking in lazy mode.", 4, 1075)]
        public ToggleNode RestoreCursorInLazyMode { get; set; } = new ToggleNode(true);
        [Menu("Item Hover Sleep (ms)", "Sleep duration before UIHover verification in lazy mode.\nIncrease if you notice the mouse moving and not successfully clicking on things when it should.\n\nA value of 20 is recommended.", 5, 1075)]
        public RangeNode<int> LazyModeUIHoverSleep { get; set; } = new RangeNode<int>(20, 20, 40);
        [Menu("Disable lazy mode while left click held", "When enabled, holding left mouse button will disable lazy mode auto-clicking.", 6, 1075)]
        public ToggleNode DisableLazyModeLeftClickHeld { get; set; } = new ToggleNode(true);
        [Menu("Disable lazy mode while right click held", "When enabled, holding right mouse button will disable lazy mode auto-clicking.", 7, 1075)]
        public ToggleNode DisableLazyModeRightClickHeld { get; set; } = new ToggleNode(true);
        [Menu("Lever Reclick Delay (ms)", "When Lazy Mode is enabled, prevents repeatedly clicking the same lever too quickly.\nIncrease this value if a lever is being clicked repeatedly.", 8, 1075)]
        public RangeNode<int> LazyModeLeverReclickDelay { get; set; } = new RangeNode<int>(10000, 10000, 30000);

        // ----- Items -----
        [Menu("Items", 1100)]
        public EmptyNode ItemPickupCategory { get; set; } = new EmptyNode();
        [Menu("Items", "Click items", 1, 1100)]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);

        [Menu("Item Type Filters", "Metadata-based whitelist/blacklist. Use arrows to move entries between lists and click a row to open subtype options.", 2, 1100)]
        [JsonIgnore]
        public CustomNode ItemTypeFiltersPanel { get; }

        public HashSet<string> ItemTypeWhitelistIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ItemTypeBlacklistIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> ItemTypeWhitelistSubtypeIds { get; set; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> ItemTypeBlacklistSubtypeIds { get; set; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private string _expandedItemTypeRowKey = string.Empty;

        private sealed record ItemSubtypeDefinition(string Id, string DisplayName, IReadOnlyList<string> MetadataIdentifiers);

        private static readonly Dictionary<string, ItemSubtypeDefinition[]> ItemSubtypeCatalog = new(StringComparer.OrdinalIgnoreCase)
        {
            ["armour"] =
            [
                new("helmets", "Helmets", ["Items/Armours/Helmets/"]),
                new("body-armours", "Body Armours", ["Items/Armours/BodyArmours/"]),
                new("gloves", "Gloves", ["Items/Armours/Gloves/"]),
                new("boots", "Boots", ["Items/Armours/Boots/"]),
                new("shields", "Shields", ["Items/Armours/Shields/"])
            ],
            ["weapons"] =
            [
                new("swords", "Swords", ["Items/Weapons/OneHandWeapons/OneHandSwords/", "Items/Weapons/TwoHandWeapons/TwoHandSwords/", "Items/Weapons/TwoHandWeapon/TwoHandSwords/"]),
                new("axes", "Axes", ["Items/Weapons/OneHandWeapons/OneHandAxes/", "Items/Weapons/TwoHandWeapons/TwoHandAxes/", "Items/Weapons/TwoHandWeapon/TwoHandAxes/"]),
                new("maces-sceptres", "Maces & Sceptres", ["Items/Weapons/OneHandWeapons/OneHandMaces/", "Items/Weapons/OneHandWeapons/Sceptres/", "Items/Weapons/TwoHandWeapons/TwoHandMaces/", "Items/Weapons/TwoHandWeapon/TwoHandMaces/"]),
                new("bows", "Bows", ["Items/Weapons/TwoHandWeapons/Bows/", "Items/Weapons/TwoHandWeapon/Bows/"]),
                new("wands", "Wands", ["Items/Weapons/OneHandWeapons/Wands/"]),
                new("daggers", "Daggers", ["Items/Weapons/OneHandWeapons/Daggers/", "Items/Weapons/OneHandWeapons/RuneDaggers/"]),
                new("claws", "Claws", ["Items/Weapons/OneHandWeapons/Claws/"]),
                new("staves", "Staves & Warstaves", ["Items/Weapons/TwoHandWeapons/Staves/", "Items/Weapons/TwoHandWeapon/Staves/", "Items/Weapons/TwoHandWeapons/Warstaves/", "Items/Weapons/TwoHandWeapon/Warstaves/"])
            ],
            ["flasks"] =
            [
                new("life", "Life Flasks", ["Items/Flasks/LifeFlask"]),
                new("mana", "Mana Flasks", ["Items/Flasks/ManaFlask"]),
                new("hybrid", "Hybrid Flasks", ["Items/Flasks/HybridFlask"]),
                new("utility", "Utility Flasks", ["Items/Flasks/UtilityFlask"])
            ]
        };

        // ----- General Interactions -----
        [Menu("General", 1200)]
        public EmptyNode WorldInteractionsCategory { get; set; } = new EmptyNode();
        [Menu("Basic Chests", "Click normal (non-league related) chests", 1, 1200)]
        public ToggleNode ClickBasicChests { get; set; } = new ToggleNode(false);
        [Menu("League Mechanic 'Chests'", "Click league mechanic related 'chests' (blight pustules, legion war hoards / chests, sentinel caches, etc)", 2, 1200)]
        public ToggleNode ClickLeagueChests { get; set; } = new ToggleNode(true);
        [Menu("Shrines", "Click shrines", 3, 1200)]
        public ToggleNode ClickShrines { get; set; } = new ToggleNode(true);
        [Menu("Area Transitions", "Click area transitions", 4, 1200)]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(false);
        [Menu("Crafting Recipes", "Click crafting recipes", 5, 1200)]
        public ToggleNode ClickCraftingRecipes { get; set; } = new ToggleNode(true);
        [Menu("Doors", "Click doors", 6, 1200)]
        public ToggleNode ClickDoors { get; set; } = new ToggleNode(false);
        [Menu("Levers", "Click levers", 7, 1200)]
        public ToggleNode ClickLevers { get; set; } = new ToggleNode(false);

        // ----- Mechanics -----
        [Menu("Mechanics", 2000)]
        public EmptyNode Mechanics { get; set; } = new EmptyNode();
        [Menu("Alva Temple Doors", "Click alva temple doors", 1, 2000)]
        public ToggleNode ClickAlvaTempleDoors { get; set; } = new ToggleNode(true);
        [Menu("Betrayal", "Click betrayal labels", 2, 2000)]
        public ToggleNode ClickBetrayal { get; set; } = new ToggleNode(false);
        [Menu("Blight", "Click blight pumps", 3, 2000)]
        public ToggleNode ClickBlight { get; set; } = new ToggleNode(true);
        [Menu("Breach Nodes", "Click breach nodes", 4, 2000)]
        public ToggleNode ClickBreachNodes { get; set; } = new ToggleNode(false);
        [Menu("Legion Pillars", "Click legion encounter pillars", 5, 2000)]
        public ToggleNode ClickLegionPillars { get; set; } = new ToggleNode(true);
        [Menu("Nearest Harvest Plot", "Click nearest harvest plot", 6, 2000)]
        public ToggleNode NearestHarvest { get; set; } = new ToggleNode(true);
        [Menu("Sanctum", "Click sanctum related stuff", 7, 2000)]
        public ToggleNode ClickSanctum { get; set; } = new ToggleNode(true);
        [Menu("Settlers Ore Deposits", "Click settlers league ore deposits (CrimsonIron, Orichalcum, etc)\n\nThere is a known issue with this feature meaning the plugin will repeatedly try to click on trees that have already been activated.\n\nI don't currently think there is any way to fix this due to limitations with the game memory and ExileAPI.", 8, 2000)]
        public ToggleNode ClickSettlersOre { get; set; } = new ToggleNode(true);

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

        // ----- Essences -----
        [Menu("Essences", 2200)]
        public EmptyNode Essences { get; set; } = new EmptyNode();
        [Menu("Essences", "Click essences", 1, 2200)]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);
        [Menu("Corrupt ALL Essences (Warning: This overrides all settings below)", "Corrupt all essences, overriding the settings below.", 2, 2200)]
        public ToggleNode CorruptAllEssences { get; set; } = new ToggleNode(false);
        [Menu("Corrupt Misery, Envy, Dread, Scorn", "Corrupt misery, envy, dread, scorn.", 3, 2200)]
        public ToggleNode CorruptMEDSEssences { get; set; } = new ToggleNode(true);

        // ----- Ritual -----
        [Menu("Ritual", 2300)]
        public EmptyNode Ritual { get; set; } = new EmptyNode();
        [Menu("Initiate Ritual Altars", "Click ritual altars that have not been completed yet", 1, 2300)]
        public ToggleNode ClickRitualInitiate { get; set; } = new ToggleNode(true);
        [Menu("Completed Ritual Altars", "Click ritual altars that have been completed", 2, 2300)]
        public ToggleNode ClickRitualCompleted { get; set; } = new ToggleNode(true);

        // ----- Strongboxes -----
        [Menu("Strongboxes", 2400)]
        public EmptyNode Strongboxes { get; set; } = new EmptyNode();
        [Menu("Show Strongbox Frames", "When enabled, draws a visual frame around strongboxes indicating whether or not they are locked", 1, 2400)]
        public ToggleNode ShowStrongboxFrames { get; set; } = new ToggleNode(true);
        [Menu("Regular Strongbox", "Click regular strongboxes", 2, 2400)]
        public ToggleNode RegularStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Arcanist Strongbox (currency)", "Click arcanist strongboxes", 3, 2400)]
        public ToggleNode ArcanistStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Armourer Strongbox (armour)", "Click armourer strongboxes", 4, 2400)]
        public ToggleNode ArmourerStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Artisan Strongbox (quality currency)", "Click artisan strongboxes", 5, 2400)]
        public ToggleNode ArtisanStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Blacksmith Strongbox (weapons)", "Click blacksmith strongboxes", 6, 2400)]
        public ToggleNode BlacksmithStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Cartographer Strongbox (maps)", "Click cartographer strongboxes", 7, 2400)]
        public ToggleNode CartographerStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Diviner Strongbox (divination cards)", "Click diviner strongboxes", 8, 2400)]
        public ToggleNode DivinerStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Gemcutter Strongbox (gems)", "Click gemcutter strongboxes", 9, 2400)]
        public ToggleNode GemcutterStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Jeweller Strongbox (jewellery)", "Click jeweller strongboxes", 10, 2400)]
        public ToggleNode JewellerStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Large Strongbox (+ quantity)", "Click large strongboxes", 11, 2400)]
        public ToggleNode LargeStrongbox { get; set; } = new ToggleNode(true);
        [Menu("Ornate Strongbox (+ rarity)", "Click ornate strongboxes", 12, 2400)]
        public ToggleNode OrnateStrongbox { get; set; } = new ToggleNode(true);

        // ----- Altars -----
        [Menu("Altars", 3000)]
        public EmptyNode AltarsCategory { get; set; } = new EmptyNode();

        [Menu("Settings", 1, 3000)]
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

        private string upsideSearchFilter = "";
        private string downsideSearchFilter = "";
        private string itemTypeSearchFilter = "";
        private string _lastSettingsUiError = string.Empty;
        public ClickItSettings()
        {
            InitializeDefaultWeights();
            EnsureItemTypeFiltersInitialized();
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
        }

        private void DrawPanelSafe(string panelName, Action drawAction)
        {
            try
            {
                drawAction();
            }
            catch (Exception ex)
            {
                _lastSettingsUiError = $"{panelName}: {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ClickItSettings UI Error] {_lastSettingsUiError}{Environment.NewLine}{ex}");

                ImGui.Separator();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Settings UI error caught");
                ImGui.TextWrapped(_lastSettingsUiError);

                if (ImGui.Button($"Throw Last UI Error##{panelName}"))
                {
                    throw new InvalidOperationException(_lastSettingsUiError, ex);
                }
            }
        }

        private void DrawAltarsPanel()
        {
            DrawExarchSection();
            DrawEaterSection();
            DrawAltarWeightingSection();
            DrawAlertSoundSection();
        }
        private void DrawDebugTestingPanel()
        {
            DrawToggleNodeControl(
                "Debug Mode",
                DebugMode,
                "Enables debug mode to help with troubleshooting issues.");

            DrawToggleNodeControl(
                "Additional Debug Information",
                RenderDebug,
                "Provides more debug text related to rendering the overlay.");

            if (RenderDebug.Value)
            {
                ImGui.Indent();
                DrawToggleNodeControl("Status", DebugShowStatus, "Show/hide the Status debug section");
                DrawToggleNodeControl("Game State", DebugShowGameState, "Show/hide the Game State debug section");
                DrawToggleNodeControl("Performance", DebugShowPerformance, "Show/hide the Performance debug section");
                DrawToggleNodeControl("Click Frequency Target", DebugShowClickFrequencyTarget, "Show/hide the Click Frequency Target debug section");
                DrawToggleNodeControl("Altar Detection", DebugShowAltarDetection, "Show/hide the Altar Detection debug section");
                DrawToggleNodeControl("Altar Service", DebugShowAltarService, "Show/hide the Altar Service debug section");
                DrawToggleNodeControl("Labels", DebugShowLabels, "Show/hide the Labels debug section");
                DrawToggleNodeControl("Hovered Item Metadata", DebugShowHoveredItemMetadata, "Show/hide the hovered item metadata debug section");
                DrawToggleNodeControl("Recent Errors", DebugShowRecentErrors, "Show/hide the Recent Errors debug section");
                DrawToggleNodeControl("Debug Frames", DebugShowFrames, "Show/hide the debug screen area frames");
                ImGui.Unindent();
            }

            DrawToggleNodeControl(
                "Log messages",
                LogMessages,
                "This will flood your log and screen with debug text.");

            if (ImGui.Button("Report Bug"))
            {
                TriggerButtonNode(ReportBugButton);
            }
            DrawInlineTooltip("If you run into a bug that hasn't already been reported, please report it here.");
        }

        private void DrawItemTypeFiltersPanel()
        {
            EnsureItemTypeFiltersInitialized();

            ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            bool sectionOpen = ImGui.TreeNode("Item Type Whitelist / Blacklist");
            if (!sectionOpen)
                return;

            try
            {

                ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Click a table row to open subtype filter options.");
                ImGui.TextWrapped("Use arrow buttons to move item types between lists. Row-click only opens the subtype menu.");
                ImGui.Spacing();

                DrawSearchBar("##ItemTypeSearch", "Clear##ItemTypeClear", ref itemTypeSearchFilter);
                ImGui.SameLine();
                if (ImGui.Button("Reset Defaults##ItemTypeDefaults"))
                {
                    ItemTypeWhitelistIds = new HashSet<string>(ItemCategoryCatalog.DefaultWhitelistIds, StringComparer.OrdinalIgnoreCase);
                    ItemTypeBlacklistIds = new HashSet<string>(ItemCategoryCatalog.DefaultBlacklistIds, StringComparer.OrdinalIgnoreCase);
                    ItemTypeWhitelistSubtypeIds.Clear();
                    ItemTypeBlacklistSubtypeIds.Clear();
                    _expandedItemTypeRowKey = string.Empty;
                }

                ImGui.Spacing();

                bool tableOpen = ImGui.BeginTable("ItemTypeFilterLists", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable);
                if (!tableOpen)
                    return;

                try
                {
                    ImGui.TableSetupColumn("Whitelist", ImGuiTableColumnFlags.WidthStretch, 0.5f);
                    ImGui.TableSetupColumn("Blacklist", ImGuiTableColumnFlags.WidthStretch, 0.5f);
                    ImGui.TableHeadersRow();

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    DrawItemTypeList("Whitelist##ItemType", ItemTypeWhitelistIds, moveToWhitelist: false);

                    ImGui.TableSetColumnIndex(1);
                    DrawItemTypeList("Blacklist##ItemType", ItemTypeBlacklistIds, moveToWhitelist: true);
                }
                finally
                {
                    ImGui.EndTable();
                }
            }
            finally
            {
                ImGui.TreePop();
            }
        }

        private void DrawItemTypeList(string id, HashSet<string> sourceSet, bool moveToWhitelist)
        {
            // Avoid BeginChild here for compatibility with older ImGuiNET builds bundled by ExileAPI.
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (ItemCategoryDefinition category in ItemCategoryCatalog.All)
            {
                if (!sourceSet.Contains(category.Id))
                    continue;
                if (!MatchesItemTypeSearch(category, itemTypeSearchFilter))
                    continue;

                hasEntries = true;
                bool hasSubtypeMenu = TryGetSubtypeDefinitions(category.Id, out _);
                string submenuIndicator = hasSubtypeMenu ? " [v]" : string.Empty;
                string label = $"{category.DisplayName}{submenuIndicator}##{id}_{category.Id}";

                float availableWidth = Math.Max(80f, ImGui.GetContentRegionAvail().X);
                const float arrowWidth = 28f;
                float rowWidth = Math.Max(40f, availableWidth - arrowWidth - 6f);

                bool rowClicked;
                bool arrowClicked;
                bool rowHovered;

                if (moveToWhitelist)
                {
                    arrowClicked = ImGui.Button($"<-##Move_{id}_{category.Id}", new Vector2(arrowWidth, 0));
                    ImGui.SameLine();
                    rowClicked = ImGui.Selectable(label, IsExpandedRow(id, category.Id), ImGuiSelectableFlags.AllowDoubleClick, new Vector2(rowWidth, 0));
                    rowHovered = ImGui.IsItemHovered();
                }
                else
                {
                    rowClicked = ImGui.Selectable(label, IsExpandedRow(id, category.Id), ImGuiSelectableFlags.AllowDoubleClick, new Vector2(rowWidth, 0));
                    rowHovered = ImGui.IsItemHovered();
                    ImGui.SameLine();
                    arrowClicked = ImGui.Button($"->##Move_{id}_{category.Id}", new Vector2(arrowWidth, 0));
                }

                if (arrowClicked)
                {
                    MoveItemTypeCategory(category.Id, moveToWhitelist);
                    _expandedItemTypeRowKey = string.Empty;
                    break;
                }

                if (rowClicked)
                {
                    ToggleExpandedRow(id, category.Id);
                }

                if (rowHovered && category.ExampleItems.Count > 0)
                {
                    string examples = string.Join(", ", category.ExampleItems);
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
                    ImGui.Indent();
                    ImGui.TextWrapped($"Examples: {examples}");
                    ImGui.Unindent();
                    ImGui.PopStyleColor();
                }

                if (IsExpandedRow(id, category.Id))
                {
                    DrawItemTypeSubtypePanel(id, category, isSourceWhitelist: !moveToWhitelist);
                }
            }

            if (!hasEntries)
            {
                ImGui.TextDisabled("No entries");
            }

            ImGui.PopID();
        }

        private void DrawItemTypeSubtypePanel(string listId, ItemCategoryDefinition category, bool isSourceWhitelist)
        {
            if (!TryGetSubtypeDefinitions(category.Id, out ItemSubtypeDefinition[] subtypeDefinitions))
            {
                return;
            }

            HashSet<string> selectedSubtypeIds = GetOrCreateSubtypeSelection(isSourceWhitelist, category.Id);

            ImGui.Indent();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1f));
            ImGui.TextWrapped("Subtype filter: select subtypes to narrow this category. Example: choosing only Helmets means Body Armours/Gloves/Boots/Shields will be treated as being in the opposite list.");
            ImGui.PopStyleColor();

            foreach (ItemSubtypeDefinition subtype in subtypeDefinitions)
            {
                bool isSelected = selectedSubtypeIds.Contains(subtype.Id);
                if (ImGui.Checkbox($"{subtype.DisplayName}##Subtype_{listId}_{category.Id}_{subtype.Id}", ref isSelected))
                {
                    if (isSelected)
                    {
                        selectedSubtypeIds.Add(subtype.Id);
                    }
                    else
                    {
                        selectedSubtypeIds.Remove(subtype.Id);
                    }
                }

                if (ImGui.IsItemHovered())
                {
                    string metadataPreview = string.Join("\n", subtype.MetadataIdentifiers);
                    ImGui.SetTooltip(metadataPreview);
                }
            }

            ImGui.Unindent();
        }

        private static bool TryGetSubtypeDefinitions(string categoryId, out ItemSubtypeDefinition[] definitions)
        {
            return ItemSubtypeCatalog.TryGetValue(categoryId, out definitions!);
        }

        private HashSet<string> GetOrCreateSubtypeSelection(bool isWhitelist, string categoryId)
        {
            Dictionary<string, HashSet<string>> source = isWhitelist ? ItemTypeWhitelistSubtypeIds : ItemTypeBlacklistSubtypeIds;
            if (!source.TryGetValue(categoryId, out HashSet<string>? subtypeSelection))
            {
                subtypeSelection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                source[categoryId] = subtypeSelection;
            }

            return subtypeSelection;
        }

        private static string BuildExpandedRowKey(string listId, string categoryId)
        {
            return $"{listId}:{categoryId}";
        }

        private bool IsExpandedRow(string listId, string categoryId)
        {
            return string.Equals(_expandedItemTypeRowKey, BuildExpandedRowKey(listId, categoryId), StringComparison.Ordinal);
        }

        private void ToggleExpandedRow(string listId, string categoryId)
        {
            string rowKey = BuildExpandedRowKey(listId, categoryId);
            if (string.Equals(_expandedItemTypeRowKey, rowKey, StringComparison.Ordinal))
            {
                _expandedItemTypeRowKey = string.Empty;
            }
            else
            {
                _expandedItemTypeRowKey = rowKey;
            }
        }

        private void MoveItemTypeCategory(string categoryId, bool moveToWhitelist)
        {
            HashSet<string> sourceSet = moveToWhitelist ? ItemTypeBlacklistIds : ItemTypeWhitelistIds;
            HashSet<string> targetSet = moveToWhitelist ? ItemTypeWhitelistIds : ItemTypeBlacklistIds;
            Dictionary<string, HashSet<string>> sourceSubtypeDict = moveToWhitelist ? ItemTypeBlacklistSubtypeIds : ItemTypeWhitelistSubtypeIds;
            Dictionary<string, HashSet<string>> targetSubtypeDict = moveToWhitelist ? ItemTypeWhitelistSubtypeIds : ItemTypeBlacklistSubtypeIds;

            sourceSet.Remove(categoryId);
            targetSet.Add(categoryId);

            if (sourceSubtypeDict.TryGetValue(categoryId, out HashSet<string>? subtypeSelection))
            {
                sourceSubtypeDict.Remove(categoryId);
                targetSubtypeDict[categoryId] = new HashSet<string>(subtypeSelection, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                targetSubtypeDict.Remove(categoryId);
            }
        }

        private static bool MatchesItemTypeSearch(ItemCategoryDefinition category, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            string term = filter.Trim();
            return category.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || category.MetadataIdentifiers.Any(x => x.Contains(term, StringComparison.OrdinalIgnoreCase))
                || category.Id.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        public IReadOnlyList<string> GetItemTypeWhitelistMetadataIdentifiers()
        {
            EnsureItemTypeFiltersInitialized();
            return ItemTypeWhitelistIds
                .SelectMany(id => GetEffectiveMetadataIdentifiers(id, isWhitelist: true, includeOppositeSubtypeSelections: false))
                .Concat(ItemTypeBlacklistIds.SelectMany(id => GetEffectiveMetadataIdentifiers(id, isWhitelist: false, includeOppositeSubtypeSelections: true)))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> GetItemTypeBlacklistMetadataIdentifiers()
        {
            EnsureItemTypeFiltersInitialized();
            return ItemTypeBlacklistIds
                .SelectMany(id => GetEffectiveMetadataIdentifiers(id, isWhitelist: false, includeOppositeSubtypeSelections: false))
                .Concat(ItemTypeWhitelistIds.SelectMany(id => GetEffectiveMetadataIdentifiers(id, isWhitelist: true, includeOppositeSubtypeSelections: true)))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private IEnumerable<string> GetEffectiveMetadataIdentifiers(string categoryId, bool isWhitelist, bool includeOppositeSubtypeSelections)
        {
            if (!ItemCategoryCatalog.TryGet(categoryId, out ItemCategoryDefinition? category))
            {
                return Array.Empty<string>();
            }

            if (!TryGetSubtypeDefinitions(categoryId, out ItemSubtypeDefinition[] subtypeDefinitions))
            {
                if (includeOppositeSubtypeSelections)
                {
                    return Array.Empty<string>();
                }

                return category.MetadataIdentifiers;
            }

            Dictionary<string, HashSet<string>> subtypeConfig = isWhitelist ? ItemTypeWhitelistSubtypeIds : ItemTypeBlacklistSubtypeIds;
            if (!subtypeConfig.TryGetValue(categoryId, out HashSet<string>? selectedSubtypeIds) || selectedSubtypeIds.Count == 0)
            {
                if (includeOppositeSubtypeSelections)
                {
                    return Array.Empty<string>();
                }

                return category.MetadataIdentifiers;
            }

            return subtypeDefinitions
                .Where(x => includeOppositeSubtypeSelections
                    ? !selectedSubtypeIds.Contains(x.Id)
                    : selectedSubtypeIds.Contains(x.Id))
                .SelectMany(x => x.MetadataIdentifiers)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void EnsureItemTypeFiltersInitialized()
        {
            ItemTypeWhitelistIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ItemTypeBlacklistIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ItemTypeWhitelistSubtypeIds ??= new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            ItemTypeBlacklistSubtypeIds ??= new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            if (ItemTypeWhitelistIds.Count == 0 && ItemTypeBlacklistIds.Count == 0)
            {
                ItemTypeWhitelistIds = new HashSet<string>(ItemCategoryCatalog.DefaultWhitelistIds, StringComparer.OrdinalIgnoreCase);
                ItemTypeBlacklistIds = new HashSet<string>(ItemCategoryCatalog.DefaultBlacklistIds, StringComparer.OrdinalIgnoreCase);
                return;
            }

            ItemTypeWhitelistIds.RemoveWhere(x => !ItemCategoryCatalog.AllIds.Contains(x));
            ItemTypeBlacklistIds.RemoveWhere(x => !ItemCategoryCatalog.AllIds.Contains(x));

            foreach (string id in ItemTypeWhitelistIds.ToArray())
            {
                ItemTypeBlacklistIds.Remove(id);
            }

            SanitizeSubtypeDictionary(ItemTypeWhitelistSubtypeIds, ItemTypeWhitelistIds);
            SanitizeSubtypeDictionary(ItemTypeBlacklistSubtypeIds, ItemTypeBlacklistIds);
        }

        private static void SanitizeSubtypeDictionary(Dictionary<string, HashSet<string>> subtypeSelections, HashSet<string> parentCategoryIds)
        {
            string[] invalidParentIds = subtypeSelections.Keys
                .Where(id => !parentCategoryIds.Contains(id) || !ItemSubtypeCatalog.ContainsKey(id))
                .ToArray();

            foreach (string invalidParentId in invalidParentIds)
            {
                subtypeSelections.Remove(invalidParentId);
            }

            foreach ((string parentId, HashSet<string> selectedSubtypes) in subtypeSelections.ToArray())
            {
                if (!ItemSubtypeCatalog.TryGetValue(parentId, out ItemSubtypeDefinition[]? subtypeDefinitions))
                {
                    subtypeSelections.Remove(parentId);
                    continue;
                }

                HashSet<string> validSubtypeIds = new HashSet<string>(subtypeDefinitions.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
                selectedSubtypes.RemoveWhere(id => !validSubtypeIds.Contains(id));
            }
        }

        private void DrawExarchSection()
        {
            if (!ImGui.TreeNode("Searing Exarch"))
                return;

            DrawToggleNodeControl(
                "Click recommended option##Exarch",
                ClickExarchAltars,
                "Clicks searing exarch altars for you based on a decision tree created from your settings.\n\nIf both options are as good as each other (according to your weights), this won't click for you.");

            DrawToggleNodeControl(
                "Highlight recommended option##Exarch",
                HighlightExarchAltars,
                "Highlights the recommended option for you to choose for searing exarch altars, based on a decision tree created from your settings below.");

            ImGui.TreePop();
        }
        private void DrawEaterSection()
        {
            if (!ImGui.TreeNode("Eater of Worlds"))
                return;

            DrawToggleNodeControl(
                "Click recommended option##Eater",
                ClickEaterAltars,
                "Clicks eater of worlds altars for you based on a decision tree created from your settings.\n\nIf both options are as good as each other (according to your weights), this won't click for you.");

            DrawToggleNodeControl(
                "Highlight recommended option##Eater",
                HighlightEaterAltars,
                "Highlights the recommended option for you to choose for eater of worlds altars, based on a decision tree created from your settings below.");

            ImGui.TreePop();
        }
        private void DrawAltarWeightingSection()
        {
            if (!ImGui.TreeNode("Altar Weighting"))
                return;

            DrawAltarModWeights();

            DrawToggleNodeControl(
                "Valuable Upside",
                ValuableUpside,
                "When enabled, automatically chooses the altar option with modifiers that have weights above the threshold, even if the overall weight calculation would suggest otherwise.");

            DrawRangeNodeControl(
                "Valuable Upside Threshold",
                ValuableUpsideThreshold,
                1,
                100,
                "Minimum weight threshold for upside modifiers to trigger the high value override. Modifiers with weights at or above this value will cause the plugin to choose that altar option.");

            DrawToggleNodeControl(
                "Unvaluable Upside",
                UnvaluableUpside,
                "When enabled, automatically chooses the opposite altar option when modifiers have weights at or below the threshold, avoiding potentially undesirable choices.");

            DrawRangeNodeControl(
                "Unvaluable Threshold",
                UnvaluableUpsideThreshold,
                1,
                100,
                "Weight threshold that triggers the low value override. When any modifier has a weight at or below this value, the plugin will choose the opposite altar option.");

            DrawToggleNodeControl(
                "Dangerous Downside",
                DangerousDownside,
                "When enabled, automatically avoids altar options with dangerous downside modifiers that have weights above the threshold.");

            DrawRangeNodeControl(
                "Dangerous Downside Threshold",
                DangerousDownsideThreshold,
                1,
                100,
                "Maximum weight threshold for downside modifiers to trigger the dangerous override. Modifiers with weights at or above this value will cause the plugin to choose the opposite altar option.");

            DrawToggleNodeControl(
                "Minimum Weight Threshold",
                MinWeightThresholdEnabled,
                "When enabled, the plugin will enforce a minimum final weight for altar options. If an option's final weight is below this value the plugin will avoid picking it (and will choose the opposite option if available).");

            DrawRangeNodeControl(
                "Minimum Weight Value",
                MinWeightThreshold,
                1,
                100,
                "Minimum final weight (1 - 100) an option must have to be considered valid. If both options are below this value, neither will be auto-chosen.");

            ImGui.TreePop();
        }
        private void DrawAlertSoundSection()
        {
            if (!ImGui.TreeNode("Alert Sound"))
                return;

            DrawToggleNodeControl(
                "Auto-download Default Alert Sound",
                AutoDownloadAlertSound,
                "When enabled the plugin will attempt to download a default 'alert.wav' from the project's GitHub repository into your plugin config folder if the file is missing.");

            if (ImGui.Button("Open Config Directory"))
            {
                TriggerButtonNode(OpenConfigDirectory);
            }
            DrawInlineTooltip("Open the plugin config directory where you should put 'alert.wav'");

            if (ImGui.Button("Reload Alert Sound"))
            {
                TriggerButtonNode(ReloadAlertSound);
            }
            DrawInlineTooltip("Reloads the 'alert.wav' sound file from the config directory");

            DrawRangeNodeControl(
                "Alert Volume",
                AlertSoundVolume,
                0,
                100,
                "Volume to play alert sound at (0-100)");

            ImGui.TreePop();
        }
        private static void DrawToggleNodeControl(string label, ToggleNode node, string tooltip)
        {
            bool value = node.Value;
            if (ImGui.Checkbox(label, ref value))
            {
                node.Value = value;
            }
            DrawInlineTooltip(tooltip);
        }
        private static void DrawRangeNodeControl(string label, RangeNode<int> node, int min, int max, string tooltip)
        {
            int value = node.Value;
            if (ImGui.SliderInt(label, ref value, min, max))
            {
                node.Value = value;
            }
            DrawInlineTooltip(tooltip);
        }
        private static void DrawInlineTooltip(string tooltip)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
        }
        private static void TriggerButtonNode(ButtonNode buttonNode)
        {
            if (buttonNode == null)
                return;

            try
            {
                var buttonType = buttonNode.GetType();
                var candidateMethods = new[] { "Press", "Click", "Invoke", "Trigger" };
                foreach (var methodName in candidateMethods)
                {
                    var method = buttonType.GetMethod(methodName);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        method.Invoke(buttonNode, null);
                        return;
                    }
                }

                var onPressedProperty = buttonType.GetProperty("OnPressed");
                if (onPressedProperty?.GetValue(buttonNode) is Delegate propertyDelegate)
                {
                    propertyDelegate.DynamicInvoke();
                    return;
                }

                var onPressedField = buttonType.GetField("OnPressed");
                if (onPressedField?.GetValue(buttonNode) is Delegate fieldDelegate)
                {
                    fieldDelegate.DynamicInvoke();
                }
            }
            catch
            {
                // Best effort fallback: button invocation API may vary by ExileCore build.
            }
        }
        private void DrawAltarModWeights()
        {
            DrawUpsideModsSection();
            DrawDownsideModsSection();
        }
        private void DrawUpsideModsSection()
        {
            bool isOpen = ImGui.TreeNode("Altar Upside Weights");
            DrawInlineTooltip("Set weights for upside modifiers. Higher values are more desirable and can influence recommended altar choices.");
            if (!isOpen) return;
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale (Higher = More Valuable):");
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
            bool isOpen = ImGui.TreeNode("Altar Downside Weights");
            DrawInlineTooltip("Set weights for downside modifiers. Higher values are more dangerous and can influence recommended altar choices.");
            if (!isOpen) return;
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
            // Upside table includes an extra "Alert" checkbox column
            // Use NoHostExtendX + NoPadOuterX so the table keeps the fixed column widths
            // and doesn't stretch to the window width when the settings window is resized.
            if (!ImGui.BeginTable("UpsideModsConfig", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;
            SetupModTableColumns(isUpside: true);
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
            if (!ImGui.BeginTable("DownsideModsConfig", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;
            SetupModTableColumns(isUpside: false);
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
        private static void SetupModTableColumns(bool isUpside = false)
        {
            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 125);
            var modWidth = isUpside ? 760 : 830;
            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthFixed, modWidth);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 50);
            if (isUpside)
            {
                ImGui.TableSetupColumn("Alert", ImGuiTableColumnFlags.WidthFixed, 55);
            }
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
                AltarTypeMinion => "Minion Drops",
                AltarTypeBoss => "Boss Drops",
                AltarTypePlayer => "Player Bonuses",
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
                AltarTypeMinion => new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                AltarTypeBoss => new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                AltarTypePlayer => new Vector4(0.2f, 0.2f, 0.6f, 0.3f),
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
            // Use a unique internal id for the slider so it does not collide with other widgets
            if (ImGui.SliderInt("##weight", ref currentValue, 1, 100))
            {
                ModTiers[BuildCompositeKey(type, id)] = currentValue;
            }
            // Mod column
            // Field width should match locked Mod column width
            ImGui.SetNextItemWidth(760);
            _ = ImGui.TableNextColumn();
            Vector4 textColor = type switch
            {
                AltarTypeMinion => new Vector4(0.4f, 0.8f, 0.4f, 1.0f),
                AltarTypeBoss => new Vector4(0.8f, 0.4f, 0.4f, 1.0f),
                AltarTypePlayer => new Vector4(0.4f, 0.7f, 0.9f, 1.0f),
                _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
            };
            ImGui.TextColored(textColor, name);
            _ = ImGui.TableNextColumn();
            ImGui.Text(type);

            // Final column: Alert checkbox for upside mods
            if (ModAlerts != null)
            {
                _ = ImGui.TableNextColumn();
                // center the checkbox inside the fixed-width alert cell
                var avail = ImGui.GetContentRegionAvail();
                float checkboxSize = 18f; // small visual estimate for a checkbox
                float currentX = ImGui.GetCursorPosX();
                float offset = (avail.X - checkboxSize) * 0.5f;
                if (offset > 0)
                {
                    ImGui.SetCursorPosX(currentX + offset);
                }

                bool currentAlert = GetModAlert(id, type);
                // Use a unique internal id for the checkbox so it doesn't share an id with the slider
                if (ImGui.Checkbox("##alert", ref currentAlert))
                {
                    ModAlerts[BuildCompositeKey(type, id)] = currentAlert;
                }
            }
            ImGui.PopID();
        }
        private void DrawDownsideModRow(string id, string name, string type, string sectionHeader)
        {
            ImGui.PushID($"downside_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            _ = ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(125);
            int currentValue = GetModTier(id, type);
            // Unique internal id for the slider prevents conflicts with other widgets in the same row
            if (ImGui.SliderInt("##weight", ref currentValue, 1, 100))
            {
                ModTiers[BuildCompositeKey(type, id)] = currentValue;
            }
            ImGui.SetNextItemWidth(830);
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
            // Add per-upside mod alert defaults - most are off by default, but enable
            // a couple of very-high-value mods (Divine Orb drops) by default.
            foreach ((string id, _, string type, int _) in AltarModsConstants.UpsideMods)
            {
                var compositeKey = BuildCompositeKey(type, id);
                if (!ModAlerts.ContainsKey(compositeKey))
                {
                    // Default to enabled for Divine Orb related modifiers
                    if ((type == AltarTypeMinion && id == "#% chance to drop an additional Divine Orb") ||
                        (type == AltarTypeBoss && id == "Final Boss drops # additional Divine Orbs"))
                    {
                        ModAlerts[compositeKey] = true;
                    }
                    else
                    {
                        ModAlerts[compositeKey] = false;
                    }
                }
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

        public bool GetModAlert(string modId, string type)
        {
            if (string.IsNullOrEmpty(modId)) return false;
            string compositeKey = BuildCompositeKey(type, modId);
            if (ModAlerts.TryGetValue(compositeKey, out bool enabled)) return enabled;
            // fallback to id-only key if present
            if (ModAlerts.TryGetValue(modId, out enabled)) return enabled;
            return false;
        }
        public Dictionary<string, int> ModTiers { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Per-upside mod alert flags (composite key = "type|id")
        public Dictionary<string, bool> ModAlerts { get; set; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
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


