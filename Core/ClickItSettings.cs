namespace ClickIt
{
    public partial class ClickItSettings : ISettings
    {
        internal const int MechanicIgnoreDistanceWithinDefault = 100;
        internal const int MechanicIgnoreDistanceWithinMin = 10;
        internal const int MechanicIgnoreDistanceWithinMax = 500;
        internal static readonly StringComparer PriorityComparer = StringComparer.OrdinalIgnoreCase;
        [JsonIgnore]
        internal ClickItSettingsTransientState TransientState { get; } = new();
        [JsonIgnore]
        internal ClickItSettingsUiState UiState => TransientState.UiState;

        public ToggleNode Enable { get; set; } = new ToggleNode(true);
        public int SettingsVersion { get; set; } = ClickItSettingsMigrationService.CurrentVersion;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> MechanicPriorityOrder { get; set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> MechanicPriorityIgnoreDistanceIds { get; set; } = new(PriorityComparer);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, int> MechanicPriorityIgnoreDistanceWithinById { get; set; } = new(PriorityComparer);

        [Menu("Mechanics", 1400)]
        public EmptyNode WorldInteractionsCategory { get; set; } = new EmptyNode();
        [Menu("", 101, 1400)]
        [JsonIgnore]
        public CustomNode MechanicsTablePanel { get; internal set; } = new();
        [JsonIgnore]
        public bool ShowLegacySettingsTreeNodes => false;
        [Menu("Basic Chests", "Click normal (non-league related) chests.", 1, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickBasicChests { get; set; } = new ToggleNode(false);
        [Menu("League Mechanic 'Chests'", "Click league mechanic related 'chests' (blight pustules, legion war hoards / chests, sentinel caches, etc).", 2, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickLeagueChests { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickLeagueChestsOther { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickMirageGoldenDjinnCache { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickMirageSilverDjinnCache { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickMirageBronzeDjinnCache { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickHeistSecureLocker { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickBreachGraspingCoffers { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickBlightCyst { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSynthesisSynthesisedStash { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode PauseAfterOpeningBasicChests { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> PauseAfterOpeningBasicChestsInitialDelayMs { get; set; } = new RangeNode<int>(500, 100, 1500);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> PauseAfterOpeningBasicChestsPollIntervalMs { get; set; } = new RangeNode<int>(100, 50, 500);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> PauseAfterOpeningBasicChestsQuietWindowMs { get; set; } = new RangeNode<int>(500, 100, 2000);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode AllowNearbyMechanicsWhileWaitingForChestDropsToSettle { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance { get; set; } = new RangeNode<int>(20, 1, 100);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode PauseAfterOpeningLeagueChests { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> PauseAfterOpeningLeagueChestsInitialDelayMs { get; set; } = new RangeNode<int>(500, 100, 1500);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> PauseAfterOpeningLeagueChestsPollIntervalMs { get; set; } = new RangeNode<int>(100, 50, 500);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> PauseAfterOpeningLeagueChestsQuietWindowMs { get; set; } = new RangeNode<int>(500, 100, 2000);
        [Menu("Shrines", "Click shrines", 3, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickShrines { get; set; } = new ToggleNode(true);
        [Menu("Lost Shipment", "Click Lost Shipment crates.", 41, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickLostShipmentCrates { get; set; } = new ToggleNode(true);
        [Menu("Area Transitions", "Click area transitions.", 4, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(false);
        [Menu("Labyrinth Trials", "Click labyrinth trial portals.", 5, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickLabyrinthTrials { get; set; } = new ToggleNode(false);
        [Menu("Crafting Recipes", "Click crafting recipes.", 6, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickCraftingRecipes { get; set; } = new ToggleNode(true);
        [Menu("Doors", "Click doors", 7, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickDoors { get; set; } = new ToggleNode(false);
        [Menu("Levers", "Click levers", 8, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickLevers { get; set; } = new ToggleNode(false);

        [JsonIgnore]
        public EmptyNode Mechanics { get; set; } = new EmptyNode();
        [Menu("Alva Temple Doors", "Click Alva Temple Doors.", 9, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickAlvaTempleDoors { get; set; } = new ToggleNode(true);
        [Menu("Betrayal", "Click betrayal labels", 10, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickBetrayal { get; set; } = new ToggleNode(false);
        [Menu("Blight", "Click blight pumps", 11, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickBlight { get; set; } = new ToggleNode(true);
        [Menu("Breach Nodes", "Click breach nodes.", 12, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickBreachNodes { get; set; } = new ToggleNode(false);
        [Menu("Legion Pillars", "Click legion encounter pillars.", 13, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickLegionPillars { get; set; } = new ToggleNode(true);
        [Menu("Nearest Harvest Plot", "Click nearest harvest plot.", 14, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode NearestHarvest { get; set; } = new ToggleNode(true);
        [Menu("Sanctum", "Click sanctum related stuff", 15, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSanctum { get; set; } = new ToggleNode(true);
        [Menu("Settlers Ore Deposits", "Click settlers league ore deposits (CrimsonIron, Orichalcum, etc).", 16, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSettlersOre { get; set; } = new ToggleNode(true);
        [Menu("Settlers Crimson Iron", "Click settlers Crimson Iron deposits.", 17, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSettlersCrimsonIron { get; set; } = new ToggleNode(true);
        [Menu("Settlers Copper", "Click settlers Copper deposits.", 18, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSettlersCopper { get; set; } = new ToggleNode(true);
        [Menu("Settlers Petrified Wood", "Click settlers Petrified Wood deposits.", 19, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSettlersPetrifiedWood { get; set; } = new ToggleNode(true);
        [Menu("Settlers Bismuth", "Click settlers Bismuth deposits.", 20, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSettlersBismuth { get; set; } = new ToggleNode(true);
        [Menu("Settlers Verisium", "Click settlers Verisium deposits.", 21, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSettlersVerisium { get; set; } = new ToggleNode(true);
        [Menu("Items", "Click items", 30, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);
        [Menu("Essences", "Click essences", 31, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);
        [Menu("Strongboxes", "Click strongboxes based on the configured strongbox filter lists.", 42, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickStrongboxes { get; set; } = new ToggleNode(true);
        [Menu("Click Initial Ultimatum", "Click the first Ultimatum interaction from the ground label, then click Begin using configured modifier priority.", 32, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickInitialUltimatum { get; set; } = new ToggleNode(false);
        [Menu("Click Ultimatum Choices", "Click later Ultimatum panel choices/confirm interactions using configured modifier priority.", 33, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickUltimatumChoices { get; set; } = new ToggleNode(false);
        [Menu("Searing Exarch", "Clicks searing exarch altars for you based on a decision tree created from your settings.\n\nIf both options are as good as each other (according to your weights), this won't click for you.", 34, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickExarchAltars { get; set; } = new ToggleNode(false);
        [Menu("Eater of Worlds", "Clicks eater of worlds altars for you based on a decision tree created from your settings.\n\nIf both options are as good as each other (according to your weights), this won't click for you.", 35, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickEaterAltars { get; set; } = new ToggleNode(false);
        [Menu("Ritual (Initiate)", "Click ritual altars that have not been completed yet.", 36, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickRitualInitiate { get; set; } = new ToggleNode(true);
        [Menu("Ritual (Completed)", "Click ritual altars that have been completed.", 37, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickRitualCompleted { get; set; } = new ToggleNode(true);
        [Menu("Azurite Veins", "Click azurite veins.", 38, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickAzuriteVeins { get; set; } = new ToggleNode(true);
        [Menu("Sulphite Veins", "Click sulphite veins.", 39, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickSulphiteVeins { get; set; } = new ToggleNode(true);
        [Menu("Encounter Initiators", "Click delve encounter initiators.", 40, 1400)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickDelveSpawners { get; set; } = new ToggleNode(true);

        [Menu("Priorities", 124, 1400)]
        public EmptyNode PrioritiesCategory { get; set; } = new EmptyNode();

        [Menu("", 10007, 124)]
        [JsonIgnore]
        public CustomNode PrioritiesSliderWidthStart { get; internal set; } = new();
        [Menu("Priority Distance Penalty", "Applies an extra distance cost per lower-priority row when comparing non-ignored mechanics.\n\nHigher values make table order matter more while still considering distance.\n\nSetting this to 0 will effectively disable the priorities feature, however, ignore distance values will still be respected.\n\nWhen priorities are disabled, distance will be the only factor considered in what to click.", 1, 124)]
        public RangeNode<int> MechanicPriorityDistancePenalty { get; set; } = new RangeNode<int>(25, 0, 100);

        [Menu("", 2, 124)]
        [JsonIgnore]
        public CustomNode MechanicPriorityTablePanel { get; internal set; } = new();
        [Menu("", 10008, 124)]
        [JsonIgnore]
        public CustomNode PrioritiesSliderWidthEnd { get; internal set; } = new();

        [Menu("", 2, 117)]
        [JsonIgnore]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public CustomNode ItemTypeFiltersPanel { get; internal set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> ItemTypeWhitelistIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> ItemTypeBlacklistIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, HashSet<string>> ItemTypeWhitelistSubtypeIds { get; set; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, HashSet<string>> ItemTypeBlacklistSubtypeIds { get; set; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        [Menu("Corrupt ALL Essences", "Overrides the essence table and attempts to corrupt every eligible essence encounter.", 1, 118)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode CorruptAllEssences { get; set; } = new ToggleNode(false);
        [JsonIgnore]
        public bool ShowEssenceCorruptionTablePanel => CorruptAllEssences?.Value != true;
        [Menu("", 2, 118)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        [JsonIgnore]
        public CustomNode EssenceCorruptionTablePanel { get; internal set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> EssenceCorruptNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> EssenceDontCorruptNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [Menu("Show Option Overlay", "Draws outlines on Ultimatum options: green for the selected option and priority colors for the other options.", 3, 119)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ShowUltimatumOptionOverlay { get; set; } = new ToggleNode(true);
        [Menu("", 4, 119)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        [JsonIgnore]
        public CustomNode UltimatumModifierTablePanel { get; internal set; } = new();

        [Menu("Click Take Reward Button", "When enabled, ClickIt can press the Take Reward button for Grueling Gauntlet based on your table decisions.\nWhen disabled, ClickIt will never press Take Reward and will only continue/confirm.", 1, 1195)]
        [JsonProperty("GruelingGauntletAutoDecision")]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickUltimatumTakeRewardButton { get; set; } = new ToggleNode(true);
        [Menu("", 2, 1195)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        [JsonIgnore]
        public CustomNode UltimatumTakeRewardModifierTablePanel { get; internal set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> UltimatumModifierPriority { get; set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> UltimatumTakeRewardModifierNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> UltimatumContinueModifierNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [Menu("Show Strongbox Overlay", "When enabled, draws a visual frame around strongboxes indicating whether or not they are locked.", 1, 120)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ShowStrongboxFrames { get; set; } = new ToggleNode(true);
        [Menu("", 2, 120)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        [JsonIgnore]
        public CustomNode StrongboxFilterTablePanel { get; internal set; } = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> StrongboxClickIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public HashSet<string> StrongboxDontClickIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [Menu("Settings", 1, 121)]
        [JsonIgnore]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public CustomNode AltarsPanel { get; internal set; } = new();

        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public EmptyNode ExarchAltar { get; set; } = new EmptyNode();
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode HighlightExarchAltars { get; set; } = new ToggleNode(true);

        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public EmptyNode EaterAltar { get; set; } = new EmptyNode();
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode HighlightEaterAltars { get; set; } = new ToggleNode(true);

        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public EmptyNode WeightOverrides { get; set; } = new EmptyNode();
        [JsonIgnore]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public CustomNode AltarModWeights { get; internal set; } = new();

        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ValuableUpside { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> ValuableUpsideThreshold { get; set; } = new RangeNode<int>(90, 1, 100);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode UnvaluableUpside { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> UnvaluableUpsideThreshold { get; set; } = new RangeNode<int>(1, 1, 100);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode DangerousDownside { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> DangerousDownsideThreshold { get; set; } = new RangeNode<int>(90, 1, 100);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode MinWeightThresholdEnabled { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> MinWeightThreshold { get; set; } = new RangeNode<int>(25, 1, 100);

        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public EmptyNode AlertSoundCategory { get; set; } = new EmptyNode();
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode AutoDownloadAlertSound { get; set; } = new ToggleNode(true);
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ButtonNode OpenConfigDirectory { get; set; } = new ButtonNode();
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ButtonNode ReloadAlertSound { get; set; } = new ButtonNode();
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> AlertSoundVolume { get; set; } = new RangeNode<int>(5, 0, 100);

        [Menu("Ritual", 122, 1400)]
        public EmptyNode Ritual { get; set; } = new EmptyNode();

        [Menu("Flares", "Use flares when all of these conditions are true:\n\n-> Your darkness debuff stacks are at least the 'Darkness Debuff Stacks' value.\n-> Your health is below the 'Use flare below Health' value.\n-> Your energy shield is below the 'Use flare below Energy Shield' value.\n\nIf you're playing CI and have 1 max life, set Health to 100.\n\nIf you have no energy shield, set Energy Shield to 100.", 4, 123)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public ToggleNode ClickDelveFlares { get; set; } = new ToggleNode(false);
        [Menu("Flare Hotkey", "Set this to your in-game keybind for flares. The plugin will press this button to use a flare.", 5, 123)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public HotkeyNodeV2 DelveFlareHotkey { get; set; } = new HotkeyNodeV2(Keys.D6);
        [Menu("", 10009, 123)]
        [JsonIgnore]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public CustomNode DelveSliderWidthStart { get; internal set; } = new();
        [Menu("Darkness Debuff Stacks", 6, 123)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> DarknessDebuffStacks { get; set; } = new RangeNode<int>(5, 1, 10);
        [Menu("Flare Health %", 7, 123)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> DelveFlareHealthThreshold { get; set; } = new RangeNode<int>(75, 2, 100);
        [Menu("Flare Energy Shield %", 8, 123)]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> DelveFlareEnergyShieldThreshold { get; set; } = new RangeNode<int>(75, 2, 100);
        [Menu("", 10010, 123)]
        [JsonIgnore]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public CustomNode DelveSliderWidthEnd { get; internal set; } = new();

        [JsonIgnore]
        internal Keys ClickLabelKeyBinding
        {
            get => ClickLabelKey is null ? Keys.None : ClickLabelKey.Value.Key;
        }

        [JsonIgnore]
        internal Keys ToggleItemsHotkeyBinding
        {
            get => ToggleItemsHotkey is null ? Keys.None : ToggleItemsHotkey.Value.Key;
        }

        [JsonIgnore]
        internal Keys LazyModeDisableKeyBinding
        {
            get => LazyModeDisableKey is null ? Keys.None : LazyModeDisableKey.Value.Key;
        }

        [JsonIgnore]
        internal Keys DelveFlareHotkeyBinding
        {
            get => DelveFlareHotkey is null ? Keys.None : DelveFlareHotkey.Value.Key;
        }

        public ClickItSettings()
        {
            InitializeDefaultWeights();
            ClickItSettingsMigrationService.Apply(this);
            SettingsUiBootstrapper.InitializeScreenNodes(this);
        }

    }
}
