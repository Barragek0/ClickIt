namespace ClickIt
{
    public partial class ClickItSettings
    {
        [IgnoreMenu]
        public EmptyNode EmptyTesting { get; set; } = new EmptyNode();

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode DebugTestingPanel { get; internal set; } = new();

        [IgnoreMenu]
        public ToggleNode DebugWindowVisible { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public TextNode DebugWindowSplitterWidth { get; set; } = new TextNode("");

        [IgnoreMenu]
        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode RenderDebug { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ButtonNode CopyAdditionalDebugInfoButton { get; set; } = new ButtonNode();

        [IgnoreMenu]
        public ToggleNode DebugShowClick { get; set; } = new ToggleNode(true);


        [IgnoreMenu]
        public ToggleNode DebugShowStatus { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowGameState { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowWindowDebug { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowPerformance { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode RenderPerformanceInGame { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode OnlyShowPerformanceInGameWhileInMap { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowClickFrequencyTarget { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowAltarDetection { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowAltarService { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowLabels { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowInventoryPickup { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode DebugShowHoveredItemMetadata { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowPathfinding { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode DebugShowUltimatum { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode DebugShowClicking { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode DebugShowRuntimeDebugLogOverlay { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode DebugShowRecentErrors { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowHarvest { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode DebugShowBlight { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode BlightDebugShowLaneLabels { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode DebugShowUnclickableScreenRegions { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        [ConditionalDisplay(nameof(ShowLegacySettingsTreeNodes))]
        public RangeNode<int> DebugFreezeSuccessfulInteractionMs { get; set; } = new RangeNode<int>(10000, 0, 20000);

        [IgnoreMenu]
        public ToggleNode LogMessages { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ButtonNode ReportBugButton { get; set; } = new ButtonNode();
    }
}