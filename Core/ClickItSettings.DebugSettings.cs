using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;
using Newtonsoft.Json;

namespace ClickIt
{
    public partial class ClickItSettings
    {
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
    }
}