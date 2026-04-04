namespace ClickIt
{
    public partial class ClickItSettings
    {
        [IgnoreMenu]
        public EmptyNode EmptyTesting { get; set; } = new EmptyNode();

        [IgnoreMenu]
        [JsonIgnore]
        public CustomNode DebugTestingPanel { get; internal set; } = new();

        [JsonIgnore]
        public bool ShowRawDebugNodesInSettings => false;

        [IgnoreMenu]
        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ToggleNode RenderDebug { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ButtonNode CopyAdditionalDebugInfoButton { get; set; } = new ButtonNode();

        [JsonIgnore]
        public bool MemoryDumpInProgress { get => TransientState.MemoryDumpInProgress; set => TransientState.MemoryDumpInProgress = value; }

        [JsonIgnore]
        public int MemoryDumpProgressPercent { get => TransientState.MemoryDumpProgressPercent; set => TransientState.MemoryDumpProgressPercent = value; }

        [JsonIgnore]
        public bool MemoryDumpLastRunSucceeded { get => TransientState.MemoryDumpLastRunSucceeded; set => TransientState.MemoryDumpLastRunSucceeded = value; }

        [JsonIgnore]
        public string MemoryDumpStatusText { get => TransientState.MemoryDumpStatusText; set => TransientState.MemoryDumpStatusText = value; }

        [JsonIgnore]
        public string MemoryDumpOutputPath { get => TransientState.MemoryDumpOutputPath; set => TransientState.MemoryDumpOutputPath = value; }

        [IgnoreMenu]
        public ToggleNode DebugShowStatus { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowGameState { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode DebugShowPerformance { get; set; } = new ToggleNode(true);

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
        public ToggleNode DebugShowFrames { get; set; } = new ToggleNode(true);

        [IgnoreMenu]
        public ToggleNode AutoCopyInventoryWarningDebug { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        public RangeNode<int> DebugFreezeSuccessfulInteractionMs { get; set; } = new RangeNode<int>(10000, 0, 20000);

        [IgnoreMenu]
        public ToggleNode LogMessages { get; set; } = new ToggleNode(false);

        [IgnoreMenu]
        public ButtonNode ReportBugButton { get; set; } = new ButtonNode();
    }
}

namespace ClickIt.Core.Settings
{
    public sealed class ClickItDebugSettingsSubmenu(ClickItSettings owner)
    {
        private bool ShowRawDebugNodesInSettings => owner.ShowRawDebugNodesInSettings;

        [Menu(" ", 1)]
        [JsonIgnore]
        public CustomNode DebugTestingPanel
        {
            get => owner.DebugTestingPanel;
            set => owner.DebugTestingPanel = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Debug Mode", "Enables debug mode to help with troubleshooting issues.", 1)]
        public ToggleNode DebugMode
        {
            get => owner.DebugMode;
            set => owner.DebugMode = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Additional Debug Information", "Provides more debug text related to rendering the overlay.", 2)]
        public ToggleNode RenderDebug
        {
            get => owner.RenderDebug;
            set => owner.RenderDebug = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Copy Additional Debug Information", "Copies the current Additional Debug Information text to clipboard.", 5)]
        public ButtonNode CopyAdditionalDebugInfoButton
        {
            get => owner.CopyAdditionalDebugInfoButton;
            set => owner.CopyAdditionalDebugInfoButton = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Status", "Show/hide the status debug section", 1, 2)]
        public ToggleNode DebugShowStatus
        {
            get => owner.DebugShowStatus;
            set => owner.DebugShowStatus = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Game State", "Show/hide the Game State debug section", 2, 2)]
        public ToggleNode DebugShowGameState
        {
            get => owner.DebugShowGameState;
            set => owner.DebugShowGameState = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Performance", "Show/hide the performance debug section", 3, 2)]
        public ToggleNode DebugShowPerformance
        {
            get => owner.DebugShowPerformance;
            set => owner.DebugShowPerformance = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Click Frequency Target", "Show/hide the Click Frequency Target debug section", 4, 2)]
        public ToggleNode DebugShowClickFrequencyTarget
        {
            get => owner.DebugShowClickFrequencyTarget;
            set => owner.DebugShowClickFrequencyTarget = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Altar Detection", "Show/hide the Altar Detection debug section", 5, 2)]
        public ToggleNode DebugShowAltarDetection
        {
            get => owner.DebugShowAltarDetection;
            set => owner.DebugShowAltarDetection = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Altar Service", "Show/hide the Altar Service debug section", 6, 2)]
        public ToggleNode DebugShowAltarService
        {
            get => owner.DebugShowAltarService;
            set => owner.DebugShowAltarService = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Labels", "Show/hide the labels debug section", 7, 2)]
        public ToggleNode DebugShowLabels
        {
            get => owner.DebugShowLabels;
            set => owner.DebugShowLabels = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Inventory Pickup", "Show/hide inventory pickup/fullness debug section", 8, 2)]
        public ToggleNode DebugShowInventoryPickup
        {
            get => owner.DebugShowInventoryPickup;
            set => owner.DebugShowInventoryPickup = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Hovered Item Metadata", "Show/hide the hovered item metadata debug section", 9, 2)]
        public ToggleNode DebugShowHoveredItemMetadata
        {
            get => owner.DebugShowHoveredItemMetadata;
            set => owner.DebugShowHoveredItemMetadata = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Pathfinding", "Show/hide offscreen pathfinding debug section", 10, 2)]
        public ToggleNode DebugShowPathfinding
        {
            get => owner.DebugShowPathfinding;
            set => owner.DebugShowPathfinding = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Ultimatum", "Show/hide ultimatum automation debug section", 11, 2)]
        public ToggleNode DebugShowUltimatum
        {
            get => owner.DebugShowUltimatum;
            set => owner.DebugShowUltimatum = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Clicking", "Show/hide clicking debug section", 12, 2)]
        public ToggleNode DebugShowClicking
        {
            get => owner.DebugShowClicking;
            set => owner.DebugShowClicking = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Debug Log Overlay", "Show/hide overlay section that displays DebugLog messages as a recent-stage style trail.", 13, 2)]
        public ToggleNode DebugShowRuntimeDebugLogOverlay
        {
            get => owner.DebugShowRuntimeDebugLogOverlay;
            set => owner.DebugShowRuntimeDebugLogOverlay = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Recent Errors", "Show/hide the Recent Errors debug section", 14, 2)]
        public ToggleNode DebugShowRecentErrors
        {
            get => owner.DebugShowRecentErrors;
            set => owner.DebugShowRecentErrors = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Debug Frames", "Show/hide the debug screen area frames", 15, 2)]
        public ToggleNode DebugShowFrames
        {
            get => owner.DebugShowFrames;
            set => owner.DebugShowFrames = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Auto Copy Inventory Warning Debug", "Automatically copies inventory warning debug details when the 'Your inventory is full' overlay is triggered. Copy attempts are throttled to once per second.", 4)]
        public ToggleNode AutoCopyInventoryWarningDebug
        {
            get => owner.AutoCopyInventoryWarningDebug;
            set => owner.AutoCopyInventoryWarningDebug = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        public RangeNode<int> DebugFreezeSuccessfulInteractionMs
        {
            get => owner.DebugFreezeSuccessfulInteractionMs;
            set => owner.DebugFreezeSuccessfulInteractionMs = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Log messages", "This will flood your log and screen with debug text.", 5)]
        public ToggleNode LogMessages
        {
            get => owner.LogMessages;
            set => owner.LogMessages = value;
        }

        [ConditionalDisplay(nameof(ShowRawDebugNodesInSettings))]
        [Menu("Report Bug", "If you run into a bug that hasn't already been reported, please report it here.", 6)]
        public ButtonNode ReportBugButton
        {
            get => owner.ReportBugButton;
            set => owner.ReportBugButton = value;
        }
    }
}