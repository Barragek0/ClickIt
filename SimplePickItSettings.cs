using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Attributes;
using System.Windows.Forms;

namespace SimplePickIt
{
    public class SimplePickItSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("Click Label Hotkey")]
        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);

        [Menu("Time between Clicks in Milliseconds")]
        public RangeNode<int> WaitTimeInMs { get; set; } = new RangeNode<int>(75, 40, 200);
        public RangeNode<int> CacheIntervall { get; set; } = new RangeNode<int>(50, 0, 200);
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(50, 0, 200);
        public ToggleNode MouseProximityMode { get; set; } = new ToggleNode(false);
        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
        public ToggleNode ClickChests { get; set; } = new ToggleNode(true);
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(true);
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);
        public ToggleNode IgnoreUniques { get; set; } = new ToggleNode(false);
        public ToggleNode BlockOnOpenLeftPanel { get; internal set; } = new ToggleNode(true);
    }
}
