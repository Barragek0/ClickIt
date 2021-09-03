using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Attributes;
using System.Windows.Forms;

namespace SimplePickIt
{
    public class SimplePickItSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("PickUp Hotkey")]
        public HotkeyNode PickUpKey { get; set; } = new HotkeyNode(Keys.F1);

        [Menu("Time between Clicks in Milliseconds")]
        public RangeNode<int> WaitTimeInMs { get; set; } = new RangeNode<int>(75, 40, 200);
        public RangeNode<int> CacheIntervall { get; set; } = new RangeNode<int>(50, 0, 200);
        public RangeNode<int> PickUpDistance { get; set; } = new RangeNode<int>(50, 0, 200);
        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
    }
}
