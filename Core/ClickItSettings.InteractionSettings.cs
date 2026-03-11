using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        // ----- General -----
        [Menu("General", 1000)]
        public EmptyNode Click { get; set; } = new EmptyNode();

        [Menu("Click Hotkey", "Held hotkey to start clicking", 1, 1000)]
        [Obsolete("Can be safely ignored for now.")]
        public HotkeyNode ClickLabelKey { get; set; } = new HotkeyNode(Keys.F1);

        [Menu("Search Radius", "Radius the plugin will search in for interactable objects. A value of 100 is recommended for 1080p, though, you may need to increase this on higher resolutions.", 2, 1000)]
        public RangeNode<int> ClickDistance { get; set; } = new RangeNode<int>(100, 0, 300);

        [Menu("Click Frequency Target (ms)", "Target milliseconds between clicks for non-altar/shrine actions. Higher = less frequent clicks.\n\nThe plugin will try to maintain this target as best it can, but heavy CPU load or many visible labels may increase delays.", 3, 1000)]
        public RangeNode<int> ClickFrequencyTarget { get; set; } = new RangeNode<int>(80, 80, 250);

        [Menu("Chest Height Offset", "If you're experiencing a lot of missclicking for chests specifically (clicking too high or low),\nchange this value. If you're clicking too high, lower the value, if you're clicking too low, raise the value", 4, 1000)]
        public RangeNode<int> ChestHeightOffset { get; set; } = new RangeNode<int>(0, -100, 100);

        // ----- Controls -----
        [Menu("Controls", 1100)]
        public EmptyNode InputAndSafetyCategory { get; set; } = new EmptyNode();

        [Menu("Block when Left or Right Panel open", "Prevent clicks when the inventory or character screen are open", 1, 1100)]
        public ToggleNode BlockOnOpenLeftRightPanel { get; internal set; } = new ToggleNode(true);

        [Menu("Verify cursor is within game window before clicking", "When enabled the plugin will verify the OS cursor is inside the Path of Exile window before performing any automated clicks. If the cursor is outside the window the click will be skipped.", 2, 1100)]
        public ToggleNode VerifyCursorInGameWindowBeforeClick { get; set; } = new ToggleNode(true);

        [Menu("Left-handed", "Changes the primary mouse button the plugin uses from left to right.", 3, 1100)]
        public ToggleNode LeftHanded { get; set; } = new ToggleNode(false);

        [Menu("Toggle Item View", "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels", 4, 1100)]
        public ToggleNode ToggleItems { get; set; } = new ToggleNode(true);

        [Menu("UIHover Verification (non-lazy)", "When enabled, the plugin verifies UIHover before clicking while NOT in Lazy Mode.\n\nThis extra verification step can make clicking slower and less frequent, however, enabling this helps prevent accidentally picking up blacklisted items.\n\nI'd recommend keeping this disabled unless you frequently encounter issues with blacklisted items being picked up.", 5, 1100)]
        public ToggleNode VerifyUIHoverWhenNotLazy { get; set; } = new ToggleNode(false);

        [Menu("Toggle Items Hotkey", "Hotkey to toggle the display of ground items / labels", 6, 1100)]
        public HotkeyNode ToggleItemsHotkey { get; set; } = new HotkeyNode(Keys.Z);

        // ----- Lazy Mode -----
        [Menu("Lazy Mode", 1200)]
        public EmptyNode LazyModeCategory { get; set; } = new EmptyNode();

        [Menu("Lazy Mode - IMPORTANT INFO IN TOOLTIP ->", "Will automatically click most things for you, without you needing to hold the key.\n\nThere are inherent limitations to this feature that cannot be fixed:\n\n-> If you are holding down a skill, for instance, Cyclone, you cannot interact with most things in the game.\n   If you use a skill that requires you to hold a key, you must set it to left or right click and enable\n   the 'disable lazy mode while x click held' setting below for lazy mode to function correctly.\n\n-> The plugin cannot detect when a chest becomes unlocked, or if a settlers tree has been activated.\n   This is a limitation with exileapi and not the plugin and for this reason, Lazy Mode is not allowed\n   to click chests that were locked when spawned or the settlers tree. When one of these is on-screen,\n   Lazy Mode will be temporarily disabled, until the blacklisted item is off of the screen, which will\n   allow you to manually press the hotkey to click these items specifically if you want to.\n\n-> This will take control away from you at crucial moments, potentially causing you to die.\n\nHolding the click items hotkey you have set in Controls will override lazy mode blocking.", 1, 1200)]
        public ToggleNode LazyMode { get; set; } = new ToggleNode(false);

        [Menu("Click Limiting (ms)", "When Lazy Mode is enabled, this sets the minimum delay (in milliseconds)\nthat must pass between consecutive clicks performed by the plugin.\nThis limiter applies to all automated clicks (shrines, altars, strongboxes, etc.)\nonly while Lazy Mode is active. Increase this value to reduce click spam and\nprevent the plugin from taking control away from you.", 2, 1200)]
        public RangeNode<int> LazyModeClickLimiting { get; set; } = new RangeNode<int>(80, 80, 1000);

        [Menu("Disable Hotkey", "When Lazy Mode is enabled and active, holding this key will temporarily disable lazy mode clicking.\nThis allows you to pause automated clicking without disabling lazy mode entirely.", 3, 1200)]
        public HotkeyNode LazyModeDisableKey { get; set; } = new HotkeyNode(Keys.F2);

        [Menu("Restore cursor position after each click", "When enabled, restores cursor to original position after clicking in lazy mode.", 4, 1200)]
        public ToggleNode RestoreCursorInLazyMode { get; set; } = new ToggleNode(true);

        [Menu("Item Hover Sleep (ms)", "Sleep duration before UIHover verification in lazy mode.\nIncrease if you notice the mouse moving and not successfully clicking on things when it should.\n\nA value of 20 is recommended.", 5, 1200)]
        public RangeNode<int> LazyModeUIHoverSleep { get; set; } = new RangeNode<int>(20, 20, 40);

        [Menu("Disable lazy mode while left click held", "When enabled, holding left mouse button will disable lazy mode auto-clicking.", 6, 1200)]
        public ToggleNode DisableLazyModeLeftClickHeld { get; set; } = new ToggleNode(true);

        [Menu("Disable lazy mode while right click held", "When enabled, holding right mouse button will disable lazy mode auto-clicking.", 7, 1200)]
        public ToggleNode DisableLazyModeRightClickHeld { get; set; } = new ToggleNode(true);

        [Menu("Lever Reclick Delay (ms)", "When Lazy Mode is enabled, prevents repeatedly clicking the same lever too quickly.\nIncrease this value if a lever is being clicked repeatedly.", 8, 1200)]
        public RangeNode<int> LazyModeLeverReclickDelay { get; set; } = new RangeNode<int>(10000, 10000, 30000);
    }
}