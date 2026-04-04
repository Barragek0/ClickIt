namespace ClickIt
{
    public partial class ClickItSettings
    {
        [IgnoreMenu]
        public EmptyNode Click { get; set; } = new EmptyNode();

        [IgnoreMenu]
        public HotkeyNodeV2 ClickLabelKey { get; set; } = new HotkeyNodeV2(Keys.F1);

        [IgnoreMenu]
        public ToggleNode ClickHotkeyToggleMode { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode ClickOnManualUiHoverOnly { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode ControlsPanel { get; internal set; } = new();

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode ControlsSliderWidthStart { get; internal set; } = new();

        [IgnoreMenu]
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(100, 0, 300);

        [IgnoreMenu]
        public RangeNode<int> ClickFrequencyTarget { get; set; } = new RangeNode<int>(80, 80, 250);

        [IgnoreMenu]
        public RangeNode<int> ChestHeightOffset { get; set; } = new RangeNode<int>(0, -100, 100);

        [IgnoreMenu]
        public EmptyNode InputAndSafetyCategory { get; set; } = new EmptyNode();

        [IgnoreMenu]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode VerifyCursorInGameWindowBeforeClick { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode LeftHanded { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode ToggleItems { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public HotkeyNodeV2 ToggleItemsHotkey { get; set; } = new HotkeyNodeV2(Keys.Z);

        [IgnoreMenu]
        public RangeNode<int> ToggleItemsIntervalMs { get; set; } = new RangeNode<int>(1500, 500, 10000);

        [IgnoreMenu]
        public RangeNode<int> ToggleItemsPostToggleClickBlockMs { get; set; } = new RangeNode<int>(20, 0, 250);

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode ControlsSliderWidthEnd { get; internal set; } = new();

        [IgnoreMenu]
        public ToggleNode VerifyUIHoverWhenNotLazy { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode AvoidOverlappingLabelClickPoints { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public EmptyNode PathfindingCategory { get; set; } = new EmptyNode();

        [IgnoreMenu]
        public ToggleNode WalkTowardOffscreenLabels { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode PrioritizeOnscreenClickableMechanicsOverPathfinding { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode PathfindingSliderWidthStart { get; internal set; } = new();

        [IgnoreMenu]
        public RangeNode<int> OffscreenPathfindingSearchBudget { get; set; } = new RangeNode<int>(6000, 1000, 50000);

        [IgnoreMenu]
        public RangeNode<int> OffscreenPathfindingLineTimeoutMs { get; set; } = new RangeNode<int>(1500, 250, 10000);

        [IgnoreMenu]
        public ToggleNode UseMovementSkillsForOffscreenPathfinding { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public RangeNode<int> OffscreenMovementSkillMinPathSubsectionLength { get; set; } = new RangeNode<int>(8, 1, 100);

        [IgnoreMenu]
        public RangeNode<int> OffscreenShieldChargePostCastClickDelayMs { get; set; } = new RangeNode<int>(100, 0, 1000);

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode PathfindingSliderWidthEnd { get; internal set; } = new();

        [IgnoreMenu]
        public EmptyNode LazyModeCategory { get; set; } = new EmptyNode();

        [IgnoreMenu]
        public ToggleNode LazyMode { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode LazyModeSliderWidthStart { get; internal set; } = new();

        [IgnoreMenu]
        public RangeNode<int> LazyModeClickLimiting { get; set; } = new RangeNode<int>(80, 80, 1000);

        [IgnoreMenu]
        public HotkeyNodeV2 LazyModeDisableKey { get; set; } = new HotkeyNodeV2(Keys.F2);

        [IgnoreMenu]
        public ToggleNode LazyModeDisableKeyToggleMode { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode RestoreCursorInLazyMode { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public RangeNode<int> LazyModeRestoreCursorDelayMs { get; set; } = new RangeNode<int>(20, 0, 40);

        [IgnoreMenu]
        public RangeNode<int> LazyModeUIHoverSleep { get; set; } = new RangeNode<int>(20, 20, 40);

        [IgnoreMenu]
        public ToggleNode DisableLazyModeLeftClickHeld { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DisableLazyModeRightClickHeld { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public RangeNode<int> LazyModeLeverReclickDelay { get; set; } = new RangeNode<int>(10000, 10000, 30000);

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode LazyModeNearbyMonsterRulesPanel { get; internal set; } = new();

        [IgnoreMenu]
        public int LazyModeNormalMonsterBlockCount { get; set; } = 0;

        [IgnoreMenu]
        public int LazyModeNormalMonsterBlockDistance { get; set; } = 10;

        [IgnoreMenu]
        public int LazyModeMagicMonsterBlockCount { get; set; } = 3;

        [IgnoreMenu]
        public int LazyModeMagicMonsterBlockDistance { get; set; } = 10;

        [IgnoreMenu]
        public int LazyModeRareMonsterBlockCount { get; set; } = 1;

        [IgnoreMenu]
        public int LazyModeRareMonsterBlockDistance { get; set; } = 10;

        [IgnoreMenu]
        public int LazyModeUniqueMonsterBlockCount { get; set; } = 1;

        [IgnoreMenu]
        public int LazyModeUniqueMonsterBlockDistance { get; set; } = 10;

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode LazyModeSliderWidthEnd { get; internal set; } = new();
    }
}
