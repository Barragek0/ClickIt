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

namespace ClickIt.Core.Settings
{
    public sealed class ClickItControlsSettingsSubmenu
    {
        private readonly ClickItSettings _owner;

        public ClickItControlsSettingsSubmenu(ClickItSettings owner)
        {
            _owner = owner;
            Pathfinding = new ClickItPathfindingSettingsSubmenu(owner);
            LazyMode = new ClickItLazyModeSettingsSubmenu(owner);
        }

        [Menu("Click Hotkey", "Held hotkey to start clicking", 1)]
        public HotkeyNodeV2 ClickLabelKey
        {
            get => _owner.ClickLabelKey;
            set => _owner.ClickLabelKey = value;
        }

        [Menu("Click Hotkey Toggle Mode", "When enabled, pressing the Click Hotkey toggles clicking on/off.\nWhen disabled, clicking only occurs while holding the Click Hotkey (or via Lazy Mode).", 2)]
        public ToggleNode ClickHotkeyToggleMode
        {
            get => _owner.ClickHotkeyToggleMode;
            set => _owner.ClickHotkeyToggleMode = value;
        }

        [Menu("Manual Cursor Target Mode", "When enabled, ClickIt repeatedly checks what your cursor is currently over, and only clicks when that on-cursor target is a valid ClickIt mechanic.\n\nSimple version: point your mouse at what you want picked up/clicked, and ClickIt will click that target without moving your cursor.\n\nThis feature is only for non-lazy mode. If Lazy Mode is enabled, this feature is ignored.\n\nHolding your Click Hotkey still overrides this feature exactly like normal, and while the hotkey is active this manual-cursor click mode is paused.", 3)]
        public ToggleNode ClickOnManualUiHoverOnly
        {
            get => _owner.ClickOnManualUiHoverOnly;
            set => _owner.ClickOnManualUiHoverOnly = value;
        }

        [Menu("", 10001)]
        [JsonIgnore]
        public CustomNode ControlsSliderWidthStart
        {
            get => _owner.ControlsSliderWidthStart;
            set => _owner.ControlsSliderWidthStart = value;
        }

        [Menu("Search Radius", "Radius the plugin will search in for interactable objects. A value of 100 is recommended for 1080p, though, you may need to increase this on higher resolutions.", 4)]
        public RangeNode<int> ClickDistance
        {
            get => _owner.ClickDistance;
            set => _owner.ClickDistance = value;
        }

        [Menu("Click Frequency Target (ms)", "Target milliseconds between clicks for non-altar/shrine actions. Higher = less frequent clicks.\n\nThe plugin will try to maintain this target as best it can, but heavy CPU load or many visible labels may increase delays.", 5)]
        public RangeNode<int> ClickFrequencyTarget
        {
            get => _owner.ClickFrequencyTarget;
            set => _owner.ClickFrequencyTarget = value;
        }

        [Menu("Chest Height Offset", "If you're experiencing a lot of missclicking for chests specifically (clicking too high or low),\nchange this value. If you're clicking too high, lower the value, if you're clicking too low, raise the value", 6)]
        public RangeNode<int> ChestHeightOffset
        {
            get => _owner.ChestHeightOffset;
            set => _owner.ChestHeightOffset = value;
        }

        [Menu("Block when Left or Right Panel open", "Prevent clicks when the inventory or character screen are open", 7)]
        public ToggleNode BlockOnOpenLeftRightPanel
        {
            get => _owner.BlockOnOpenLeftRightPanel;
            set => _owner.BlockOnOpenLeftRightPanel = value;
        }

        [Menu("Verify Cursor is within Game Window before Clicking", "When enabled, the plugin will verify the OS cursor is inside the Path of Exile window before performing any automated clicks. If the cursor is outside the window, the click will be skipped.", 8)]
        public ToggleNode VerifyCursorInGameWindowBeforeClick
        {
            get => _owner.VerifyCursorInGameWindowBeforeClick;
            set => _owner.VerifyCursorInGameWindowBeforeClick = value;
        }

        [Menu("Left-handed", "Changes the primary mouse button the plugin uses from left to right.", 9)]
        public ToggleNode LeftHanded
        {
            get => _owner.LeftHanded;
            set => _owner.LeftHanded = value;
        }

        [Menu("Toggle Item View", "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels.", 10)]
        public ToggleNode ToggleItems
        {
            get => _owner.ToggleItems;
            set => _owner.ToggleItems = value;
        }

        [Menu("Toggle Items Hotkey", "Hotkey to toggle the display of ground items / labels.", 11)]
        public HotkeyNodeV2 ToggleItemsHotkey
        {
            get => _owner.ToggleItemsHotkey;
            set => _owner.ToggleItemsHotkey = value;
        }

        [Menu("Toggle Item View Interval (ms)", "How often Toggle Item View is allowed to trigger.\n1000 ms = 1 second.", 12)]
        public RangeNode<int> ToggleItemsIntervalMs
        {
            get => _owner.ToggleItemsIntervalMs;
            set => _owner.ToggleItemsIntervalMs = value;
        }

        [Menu("Disable Clicking after Toggle Items (ms)", "Temporarily blocks further clicks after Toggle Item View triggers.\n\nIncrease this if clicks right after toggling are clicking incorrect labels.", 13)]
        public RangeNode<int> ToggleItemsPostToggleClickBlockMs
        {
            get => _owner.ToggleItemsPostToggleClickBlockMs;
            set => _owner.ToggleItemsPostToggleClickBlockMs = value;
        }

        [Menu("", 10002)]
        [JsonIgnore]
        public CustomNode ControlsSliderWidthEnd
        {
            get => _owner.ControlsSliderWidthEnd;
            set => _owner.ControlsSliderWidthEnd = value;
        }

        [Menu("UIHover Verification (non-lazy)", "When enabled, the plugin verifies UIHover before clicking while not in Lazy Mode.\n\nThis extra verification step can make clicking slower and less frequent, however, enabling this helps prevent accidentally picking up blacklisted items.\n\nI'd recommend keeping this disabled unless you frequently encounter issues with blacklisted items being picked up.", 14)]
        public ToggleNode VerifyUIHoverWhenNotLazy
        {
            get => _owner.VerifyUIHoverWhenNotLazy;
            set => _owner.VerifyUIHoverWhenNotLazy = value;
        }

        [Menu("Avoid Overlapping Labels when Clicking", "When enabled, the plugin attempts to click a visible, non-overlapped part of the target label instead of always clicking center. Helps when one label partially covers another.", 15)]
        public ToggleNode AvoidOverlappingLabelClickPoints
        {
            get => _owner.AvoidOverlappingLabelClickPoints;
            set => _owner.AvoidOverlappingLabelClickPoints = value;
        }

        [Submenu]
        [Menu("Pathfinding", 1114)]
        public ClickItPathfindingSettingsSubmenu Pathfinding { get; }

        [Submenu]
        [Menu("Lazy Mode", 1115)]
        public ClickItLazyModeSettingsSubmenu LazyMode { get; }
    }

    public sealed class ClickItPathfindingSettingsSubmenu(ClickItSettings owner)
    {
        [Menu("Walk toward Offscreen Labels", "When enabled and no clickable labels are on screen, attempt to walk toward the nearest offscreen interactable target using terrain pathfinding data.\n\nI would be careful enabling this feature as its somewhat likely GGG could flag you as a bot.\n\nWhile that hasn't happen to me while testing the feature, I wouldn't be surprised if it did happen during prolonged use.", 1)]
        public ToggleNode WalkTowardOffscreenLabels
        {
            get => owner.WalkTowardOffscreenLabels;
            set => owner.WalkTowardOffscreenLabels = value;
        }

        [Menu("Prioritize On-Screen Clickable Mechanics", "When enabled, offscreen pathfinding is skipped whenever there is at least one clickable on-screen mechanic candidate (for example: altars, shrines, settlers ore, or lost shipment).", 2)]
        public ToggleNode PrioritizeOnscreenClickableMechanicsOverPathfinding
        {
            get => owner.PrioritizeOnscreenClickableMechanicsOverPathfinding;
            set => owner.PrioritizeOnscreenClickableMechanicsOverPathfinding = value;
        }

        [Menu("", 10003)]
        [JsonIgnore]
        public CustomNode PathfindingSliderWidthStart
        {
            get => owner.PathfindingSliderWidthStart;
            set => owner.PathfindingSliderWidthStart = value;
        }

        [Menu("Offscreen Pathfinding Search Budget", "Controls pathfinding search complexity for offscreen walking. Higher values search deeper but increase CPU usage.", 3)]
        public RangeNode<int> OffscreenPathfindingSearchBudget
        {
            get => owner.OffscreenPathfindingSearchBudget;
            set => owner.OffscreenPathfindingSearchBudget = value;
        }

        [Menu("Offscreen Path Line Timeout (ms)", "Maximum age of the red pathfinding line. If pathfinding has not run within this timeout, the line is automatically cleared.", 4)]
        public RangeNode<int> OffscreenPathfindingLineTimeoutMs
        {
            get => owner.OffscreenPathfindingLineTimeoutMs;
            set => owner.OffscreenPathfindingLineTimeoutMs = value;
        }

        [Menu("Use Movement Skills for Offscreen Pathfinding", "When enabled, the plugin will attempt to use an equipped movement skill keybind while pathing to offscreen targets. Supports common travel/blink gems when they are off cooldown and have a keyboard keybind.", 5)]
        public ToggleNode UseMovementSkillsForOffscreenPathfinding
        {
            get => owner.UseMovementSkillsForOffscreenPathfinding;
            set => owner.UseMovementSkillsForOffscreenPathfinding = value;
        }

        [Menu("Movement Skill Minimum Path Subsection Length", "Minimum remaining path node count required before a movement skill cast is attempted. Lower values cast more often; higher values are more conservative.", 6)]
        public RangeNode<int> OffscreenMovementSkillMinPathSubsectionLength
        {
            get => owner.OffscreenMovementSkillMinPathSubsectionLength;
            set => owner.OffscreenMovementSkillMinPathSubsectionLength = value;
        }

        [Menu("Shield Charge Post-Cast Delay (ms)", "Delay before normal clicking resumes after Shield Charge is used for offscreen pathing. Lower values cast/recover faster; higher values are safer for slower attack speed setups.", 7)]
        public RangeNode<int> OffscreenShieldChargePostCastClickDelayMs
        {
            get => owner.OffscreenShieldChargePostCastClickDelayMs;
            set => owner.OffscreenShieldChargePostCastClickDelayMs = value;
        }

        [Menu("", 10004)]
        [JsonIgnore]
        public CustomNode PathfindingSliderWidthEnd
        {
            get => owner.PathfindingSliderWidthEnd;
            set => owner.PathfindingSliderWidthEnd = value;
        }
    }

    public sealed class ClickItLazyModeSettingsSubmenu(ClickItSettings owner)
    {
        [Menu("Lazy Mode - Important Info in Tooltip ->", "Will automatically click most things for you, without you needing to hold the key.\n\nThere are inherent limitations to this feature that cannot be fixed:\n\n-> If you are holding down a skill, for instance, Cyclone, you cannot interact with most things in the game.\n   If you use a skill that requires you to hold a key, you must set it to left or right click and enable\n   the 'Disable Lazy Mode while Left Click Held' or 'Disable Lazy Mode while Right Click Held' setting below for lazy mode to function correctly.\n\n-> The plugin cannot detect when a chest becomes unlocked,\n   This is a limitation with ExileAPI and not the plugin and for this reason, lazy mode is not allowed\n   to click chests that were locked when spawned. When a locked-on-spawn chest is on-screen,\n   lazy mode will be temporarily disabled, until the blacklisted item is off of the screen, which will\n   allow you to manually press the hotkey to click these items specifically if you want to.\n\n-> This will take control away from you at crucial moments, potentially causing you to die.\n\nHolding the click items hotkey you have set in Controls will override lazy mode blocking.", 1)]
        public ToggleNode LazyMode
        {
            get => owner.LazyMode;
            set => owner.LazyMode = value;
        }

        [Menu("", 10005)]
        [JsonIgnore]
        public CustomNode LazyModeSliderWidthStart
        {
            get => owner.LazyModeSliderWidthStart;
            set => owner.LazyModeSliderWidthStart = value;
        }

        [Menu("Click Limiting (ms)", "When lazy mode is enabled, this sets the minimum delay (in milliseconds)\nthat must pass between consecutive clicks performed by the plugin.\nThis limiter applies to all automated clicks (shrines, altars, strongboxes, etc.)\nonly while lazy mode is active. Increase this value to reduce click spam and\nprevent the plugin from taking control away from you.", 2)]
        public RangeNode<int> LazyModeClickLimiting
        {
            get => owner.LazyModeClickLimiting;
            set => owner.LazyModeClickLimiting = value;
        }

        [Menu("Disable Hotkey", "When lazy mode is enabled and active, holding this key will temporarily disable lazy mode clicking.\nThis allows you to pause automated clicking without disabling lazy mode entirely.", 3)]
        public HotkeyNodeV2 LazyModeDisableKey
        {
            get => owner.LazyModeDisableKey;
            set => owner.LazyModeDisableKey = value;
        }

        [Menu("Disable Hotkey Toggle Mode", "When enabled, pressing the Disable Hotkey toggles lazy mode clicking on/off until you press it again.\nWhen disabled, the hotkey works as hold-to-disable.", 4)]
        public ToggleNode LazyModeDisableKeyToggleMode
        {
            get => owner.LazyModeDisableKeyToggleMode;
            set => owner.LazyModeDisableKeyToggleMode = value;
        }

        [Menu("Restore Cursor Position after Each Click", "When enabled, restores cursor to original position after clicking in lazy mode.", 5)]
        public ToggleNode RestoreCursorInLazyMode
        {
            get => owner.RestoreCursorInLazyMode;
            set => owner.RestoreCursorInLazyMode = value;
        }

        [Menu("Restore Cursor Delay (ms)", "Delay before restoring cursor position after a lazy-mode click when cursor restore is enabled.\n\nWhen set below 20, this may cause the plugin to have to click an item multiple times to pick it up.", 6)]
        public RangeNode<int> LazyModeRestoreCursorDelayMs
        {
            get => owner.LazyModeRestoreCursorDelayMs;
            set => owner.LazyModeRestoreCursorDelayMs = value;
        }

        [Menu("Item Hover Sleep (ms)", "Sleep duration before UIHover verification in lazy mode.\nIncrease if you notice the mouse moving and not successfully clicking on things when it should.\n\nA value of 20 is recommended.", 7)]
        public RangeNode<int> LazyModeUIHoverSleep
        {
            get => owner.LazyModeUIHoverSleep;
            set => owner.LazyModeUIHoverSleep = value;
        }

        [Menu("Disable Lazy Mode while Left Click Held", "When enabled, holding left mouse button will disable lazy mode auto-clicking.", 8)]
        public ToggleNode DisableLazyModeLeftClickHeld
        {
            get => owner.DisableLazyModeLeftClickHeld;
            set => owner.DisableLazyModeLeftClickHeld = value;
        }

        [Menu("Disable Lazy Mode while Right Click Held", "When enabled, holding right mouse button will disable lazy mode auto-clicking.", 9)]
        public ToggleNode DisableLazyModeRightClickHeld
        {
            get => owner.DisableLazyModeRightClickHeld;
            set => owner.DisableLazyModeRightClickHeld = value;
        }

        [Menu("Lever Reclick Delay (ms)", "When lazy mode is enabled, prevents repeatedly clicking the same lever too quickly.\nIncrease this value if a lever is being clicked repeatedly.", 10)]
        public RangeNode<int> LazyModeLeverReclickDelay
        {
            get => owner.LazyModeLeverReclickDelay;
            set => owner.LazyModeLeverReclickDelay = value;
        }

        [Menu("Nearby Monster Blockers", "Prevents lazy mode clicking when nearby monster density reaches your configured thresholds.", 11)]
        [JsonIgnore]
        public CustomNode LazyModeNearbyMonsterRulesPanel
        {
            get => owner.LazyModeNearbyMonsterRulesPanel;
            set => owner.LazyModeNearbyMonsterRulesPanel = value;
        }

        [Menu("", 10006)]
        [JsonIgnore]
        public CustomNode LazyModeSliderWidthEnd
        {
            get => owner.LazyModeSliderWidthEnd;
            set => owner.LazyModeSliderWidthEnd = value;
        }
    }
}