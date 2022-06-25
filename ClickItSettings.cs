﻿using ExileCore.Shared.Interfaces;
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

        [Menu("Hotkey","Held hotkey to start clicking", 1, 1000)]
        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);

        [Menu("Wait Time Between Clicks","Time between clicks in milliseconds", 2, 1000)]
        public RangeNode<int> WaitTimeInMs { get; set; } = new RangeNode<int>(75, 40, 200);
        [Menu("", 2000)]
        public EmptyNode EmptyCaching { get; set; } = new EmptyNode();
        [Menu("Caching", "Enables caching, improving CPU load while picking up items.\n\nReload the plugin below if you change this setting!", 1, 2000)]
        public ToggleNode CachingEnable { get; set; } = new ToggleNode(true);
        [Menu("Reload","Press this when you toggle between caching and no caching", 2, 2000)]
        public ButtonNode ReloadPluginButton { get; set; } = new ButtonNode();
        [Menu("Cache Refresh Interval","How often the cache refreshes. Higher value will typically mean less CPU load on your system, but more chance to missclick labels.", 3, 2000)]
        public RangeNode<int> CacheInterval { get; set; } = new RangeNode<int>(30, 0, 200);

        [Menu("", 3000)]
        public EmptyNode EmptyClicking { get; set; } = new EmptyNode();
        [Menu("Search Radius","Radius the plugin will search in for interactable objects. Above 75 is not recommended typically as the plugin may missclick on the interface.", 1, 3000)]
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(75, 0, 200);
        [Menu("Items", "Click items", 2, 3000)]
        public ToggleNode ClickItems { get; set; } = new ToggleNode(true);
        [Menu("Ignore Unique Items", "Ignore unique items, aside from metamorph organs", 3, 3000)]
        public ToggleNode IgnoreUniques { get; set; } = new ToggleNode(false);
        [Menu("Essences", "Click essences", 4, 3000)]
        public ToggleNode ClickEssences { get; set; } = new ToggleNode(true);
        [Menu("Corrupt Essences", "Corrupt essences automatically (misery, envy, dread, scorn).\n\nAlso corrupts certain essences that result in a profit when corrupted to deafening.", 5, 3000)]
        public ToggleNode CorruptEssences { get; set; } = new ToggleNode(true);
        //[Menu("Not yet implemented", 2, 3000)]
        //public ToggleNode MouseProximityMode { get; set; } = new ToggleNode(false);
        [Menu("Basic Chests", "Click normal (non-league related) chests", 6, 3000)]
        public ToggleNode ClickBasicChests { get; set; } = new ToggleNode(false);
        [Menu("League Mechanic 'Chests'", "Click league mechanic related 'chests' (blight pustules, legion war hoards / chests, sentinel caches, etc)", 7, 3000)]
        public ToggleNode ClickLeagueChests { get; set; } = new ToggleNode(true);
        [Menu("Shrines", "Click shrines", 8, 3000)]
        public ToggleNode ClickShrines { get; set; } = new ToggleNode(true);
        [Menu("Area Transitions", "Click area transitions", 9, 3000)]
        public ToggleNode ClickAreaTransitions { get; set; } = new ToggleNode(false);
        [Menu("Open Inventory Hotkey", "Hotkey to open your inventory", 10, 3000)]
        public HotkeyNode OpenInventoryKey { get; set; } = new HotkeyNode(Keys.I);

        [Menu("Inventory Open Delay", "Milliseconds to wait before searching for remnants, after opening the inventory.\nMust be above 200ms as the game takes a short time to open and load the window, disabling clicks during that period.\n\nThis process may take longer if you're running the game on older hardware or on a HDD, which is the reason for the slider.\n\nIf your inventory is being opened and closed more than once per corruption, this value is too low.", 11, 3000)]
        public RangeNode<int> InventoryOpenDelayInMs { get; set; } = new RangeNode<int>(400, 200, 800);
        [Menu("Block on Open UI", "Disables clicking when certain panels are open to avoid misclicks", 12, 3000)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);

    }
}
