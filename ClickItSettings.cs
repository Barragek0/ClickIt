using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Attributes;
using System.Windows.Forms;

namespace ClickIt
{
    public class ClickItSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);
        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
        [Menu("", 1000)]
        public EmptyNode EmptyMain { get; set; } = new EmptyNode();

        [Menu("Hotkey","Click Label Hotkey", 1, 1000)]
        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);

        [Menu("WaitTime","Time between Clicks in Milliseconds", 2, 1000)]
        public RangeNode<int> WaitTimeInMs { get; set; } = new RangeNode<int>(75, 40, 200);
        [Menu("", 2000)]
        public EmptyNode EmptyCaching { get; set; } = new EmptyNode();
        [Menu("Caching", "Enables Caching of Labels. Reload Plugin if you change this Setting!", 1, 2000)]
        public ToggleNode CachingEnable { get; set; } = new ToggleNode(true);
        [Menu("Reload","Press this when you toggle between Caching and no Caching", 2, 2000)]
        public ButtonNode ReloadPluginButton { get; set; } = new ButtonNode();
        [Menu("CacheIntervall","Refresh interval of cached labels", 3, 2000)]
        public RangeNode<int> CacheInterval { get; set; } = new RangeNode<int>(50, 0, 200);
        [Menu("", 3000)]
        public EmptyNode EmptyClicking { get; set; } = new EmptyNode();
        [Menu("ClickDistance","How far away an item can be to be clicked", 1, 3000)]
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(50, 0, 200);
        //[Menu("Not yet implemented", 2, 3000)]
        //public ToggleNode MouseProximityMode { get; set; } = new ToggleNode(false);
        [Menu("Click Chest Labels", 3, 3000)]
        public ToggleNode ClickChests { get; set; } = new ToggleNode(true);
        [Menu("Area Transitions", "Click Area Transitions", 4, 3000)]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(true);
        [Menu("Items", "Click Items", 5, 3000)]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);
        [Menu("IgnoreUniques", "Ignore Unique Items, aside from Metamorph Organs", 6, 3000)]
        public ToggleNode IgnoreUniques { get; set; } = new ToggleNode(false);
        [Menu("Block on Open UI", "Disables Clicking when certain Panels are open to avoid misclicks", 7, 3000)]
        public ToggleNode BlockOnOpenLeftPanel { get; internal set; } = new ToggleNode(true);
        
    }
}
