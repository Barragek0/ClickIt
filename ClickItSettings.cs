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

        [Menu("Hotkey","Hotkey", 1, 1000)]
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
        [Menu("Basic Chests", "Click normal (non-league related) chests", 3, 3000)]
        public ToggleNode ClickBasicChests { get; set; } = new ToggleNode(false);
        [Menu("League Mechanic 'Chests'", "Click league mechanic related 'chests' (blight pustules, legion war hoards / chests, etc)", 4, 3000)]
        public ToggleNode ClickLeagueChests { get; set; } = new ToggleNode(false);
        [Menu("Area Transitions", "Click Area Transitions", 5, 3000)]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(true);
        [Menu("Shrines", "Click Shrines", 6, 3000)]
        public ToggleNode ClickShrines { get; set; } = new ToggleNode(true);
        [Menu("Essences", "Click Essences", 7, 3000)]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);
        [Menu("Open Inventory Hotkey", "Hotkey to open your inventory", 8, 3000)]
        public HotkeyNode OpenInventoryKey { get; set; } = new HotkeyNode(Keys.I);
        [Menu("Corrupt Essences", "Corrupt essences automatically (misery, envy, dread, scorn)", 9, 3000)]
        public ToggleNode CorruptEssences { get; set; } = new ToggleNode(true);
        [Menu("Items", "Click Items", 10, 3000)]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);
        [Menu("IgnoreUniques", "Ignore Unique Items, aside from Metamorph Organs", 11, 3000)]
        public ToggleNode IgnoreUniques { get; set; } = new ToggleNode(false);
        [Menu("Block on Open UI", "Disables clicking when certain panels are open to avoid misclicks", 12, 3000)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);

    }
}
