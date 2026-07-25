namespace ClickIt.UI.Settings.Panels
{
    internal sealed class ControlsPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public void Draw()
        {
            DrawControlsSection();
            DrawPathfindingSection();
            DrawLazyModeSection();
        }

        private void DrawControlsSection()
        {
            SettingsUiRenderHelpers.DrawHotkeyNodeControl(_settings.ClickLabelKey, "Click Hotkey##ControlsClickHotkey", "Held hotkey to start clicking");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Click Hotkey Toggle Mode##ControlsClickHotkeyToggleMode", _settings.ClickHotkeyToggleMode, "When enabled, pressing the Click Hotkey toggles clicking on/off.\nWhen disabled, clicking only occurs while holding the Click Hotkey (or via Lazy Mode).");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Manual Cursor Target Mode##ControlsManualCursorTargetMode", _settings.ClickOnManualUiHoverOnly, "When enabled, ClickIt repeatedly checks what your cursor is currently over, and only clicks when that on-cursor target is a valid ClickIt mechanic.\n\nSimple version: point your mouse at what you want picked up/clicked, and ClickIt will click that target without moving your cursor.\n\nThis feature is only for non-lazy mode. If Lazy Mode is enabled, this feature is ignored.\n\nHolding your Click Hotkey still overrides this feature exactly like normal, and while the hotkey is active this manual-cursor click mode is paused.");

            DrawSliderWidthSection(_settings.ControlsSliderWidthStart, _settings.ControlsSliderWidthEnd, () =>
            {
                SettingsUiRenderHelpers.DrawRangeNodeControl("Search Radius##ControlsSearchRadius", _settings.ClickDistance, 0, 300, "Radius the plugin will search in for interactable objects. A value of 100 is recommended for 1080p, though, you may need to increase this on higher resolutions.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Click Frequency Target (ms)##ControlsClickFrequencyTarget", _settings.ClickFrequencyTarget, 80, 250, "Target milliseconds between clicks for non-altar/shrine actions. Higher = less frequent clicks.\n\nThe plugin will try to maintain this target as best it can, but heavy CPU load or many visible labels may increase delays.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Chest Height Offset##ControlsChestHeightOffset", _settings.ChestHeightOffset, -100, 100, "If you're experiencing a lot of missclicking for chests specifically (clicking too high or low),\nchange this value. If you're clicking too high, lower the value, if you're clicking too low, raise the value");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Toggle Item View Interval (ms)##ControlsToggleItemsInterval", _settings.ToggleItemsIntervalMs, 500, 10000, "How often Toggle Item View is allowed to trigger.\n1000 ms = 1 second.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Disable Clicking after Toggle Items (ms)##ControlsToggleItemsPostClickBlock", _settings.ToggleItemsPostToggleClickBlockMs, 0, 250, "Temporarily blocks further clicks after Toggle Item View triggers.\n\nIncrease this if clicks right after toggling are clicking incorrect labels.");
            });

            SettingsUiRenderHelpers.DrawToggleNodeControl("Block when Left or Right Panel open##ControlsBlockOnOpenPanels", _settings.BlockOnOpenLeftRightPanel, "Prevent clicks when the inventory or character screen are open");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Verify Cursor is within Game Window before Clicking##ControlsVerifyCursorInWindow", _settings.VerifyCursorInGameWindowBeforeClick, "When enabled, the plugin will verify the OS cursor is inside the Path of Exile window before performing any automated clicks. If the cursor is outside the window, the click will be skipped.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Left-handed##ControlsLeftHanded", _settings.LeftHanded, "Changes the primary mouse button the plugin uses from left to right.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Toggle Item View##ControlsToggleItems", _settings.ToggleItems, "This will occasionally double tap your Toggle Items Hotkey to correct the position of ground items / labels.");
            SettingsUiRenderHelpers.DrawHotkeyNodeControl(_settings.ToggleItemsHotkey, "Toggle Items Hotkey##ControlsToggleItemsHotkey", "Hotkey to toggle the display of ground items / labels.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("UIHover Verification (non-lazy)##ControlsVerifyUiHoverWhenNotLazy", _settings.VerifyUIHoverWhenNotLazy, "When enabled, the plugin verifies UIHover before clicking while not in Lazy Mode.\n\nThis extra verification step can make clicking slower and less frequent, however, enabling this helps prevent accidentally picking up blacklisted items.\n\nI'd recommend keeping this disabled unless you frequently encounter issues with blacklisted items being picked up.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Avoid Overlapping Labels when Clicking##ControlsAvoidOverlappingLabels", _settings.AvoidOverlappingLabelClickPoints, "When enabled, the plugin attempts to click a visible, non-overlapped part of the target label instead of always clicking center. Helps when one label partially covers another.");
            SettingsUiRenderHelpers.DrawRangeNodeControl("Blocked UI Refresh Interval (ms)##ControlsBlockedUiRefreshInterval", _settings.BlockedUiRefreshIntervalMs, 50, 5000, "How often ClickIt refreshes blocked UI rectangles such as buffs, debuffs, quest tracker panels, chat, map, altar, ritual, and similar overlays.\n\nLower values react faster to UI changes but use more CPU. Higher values reduce CPU usage but increase the chance that a newly appeared UI element lags behind for a short time.");
        }

        private void DrawPathfindingSection()
        {
            if (!ImGui.TreeNodeEx("Pathfinding##ControlsPathfinding", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            SettingsUiRenderHelpers.DrawToggleNodeControl("Walk toward Offscreen Labels##ControlsPathfindingWalkTowardOffscreen", _settings.WalkTowardOffscreenLabels, "When enabled and no clickable labels are on screen, attempt to walk toward the nearest offscreen interactable target using terrain pathfinding data.\n\nI would be careful enabling this feature as its somewhat likely GGG could flag you as a bot.\n\nWhile that hasn't happen to me while testing the feature, I wouldn't be surprised if it did happen during prolonged use.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Prioritize On-Screen Clickable Mechanics##ControlsPathfindingPrioritizeOnscreen", _settings.PrioritizeOnscreenClickableMechanicsOverPathfinding, "When enabled, offscreen pathfinding is skipped whenever there is at least one clickable on-screen mechanic candidate (for example: altars, shrines, settlers ore, or lost shipment).");

            DrawSliderWidthSection(_settings.PathfindingSliderWidthStart, _settings.PathfindingSliderWidthEnd, () =>
            {
                SettingsUiRenderHelpers.DrawRangeNodeControl("Offscreen Pathfinding Search Budget##ControlsPathfindingSearchBudget", _settings.OffscreenPathfindingSearchBudget, 1000, 50000, "Controls pathfinding search complexity for offscreen walking. Higher values search deeper but increase CPU usage.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Offscreen Path Line Timeout (ms)##ControlsPathfindingLineTimeout", _settings.OffscreenPathfindingLineTimeoutMs, 250, 10000, "Maximum age of the red pathfinding line. If pathfinding has not run within this timeout, the line is automatically cleared.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Movement Skill Minimum Path Subsection Length##ControlsPathfindingMovementSkillMinLength", _settings.OffscreenMovementSkillMinPathSubsectionLength, 1, 100, "Minimum remaining path node count required before a movement skill cast is attempted. Lower values cast more often; higher values are more conservative.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Shield Charge Post-Cast Delay (ms)##ControlsPathfindingShieldChargeDelay", _settings.OffscreenShieldChargePostCastClickDelayMs, 0, 1000, "Delay before normal clicking resumes after Shield Charge is used for offscreen pathing. Lower values cast/recover faster; higher values are safer for slower attack speed setups.");
            });

            SettingsUiRenderHelpers.DrawToggleNodeControl("Use Movement Skills for Offscreen Pathfinding##ControlsPathfindingUseMovementSkills", _settings.UseMovementSkillsForOffscreenPathfinding, "When enabled, the plugin will attempt to use an equipped movement skill keybind while pathing to offscreen targets. Supports common travel/blink gems when they are off cooldown and have a keyboard keybind.");

            ImGui.TreePop();
        }

        private void DrawLazyModeSection()
        {
            if (!ImGui.TreeNodeEx("Lazy Mode##ControlsLazyMode", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            SettingsUiRenderHelpers.DrawToggleNodeControl("Lazy Mode - Important Info in Tooltip ->##ControlsLazyModeEnabled", _settings.LazyMode, "Will automatically click most things for you, without you needing to hold the key.\n\nThere are inherent limitations to this feature that cannot be fixed:\n\n-> If you are holding down a skill, for instance, Cyclone, you cannot interact with most things in the game.\n   If you use a skill that requires you to hold a key, you must set it to left or right click and enable\n   the 'Disable Lazy Mode while Left Click Held' or 'Disable Lazy Mode while Right Click Held' setting below for lazy mode to function correctly.\n\n-> The plugin cannot detect when a chest becomes unlocked,\n   This is a limitation with ExileAPI and not the plugin and for this reason, lazy mode is not allowed\n   to click chests that were locked when spawned. When a locked-on-spawn chest is on-screen,\n   lazy mode will be temporarily disabled, until the blacklisted item is off of the screen, which will\n   allow you to manually press the hotkey to click these items specifically if you want to.\n\n-> This will take control away from you at crucial moments, potentially causing you to die.\n\nHolding the click items hotkey you have set in Controls will override lazy mode blocking.");

            DrawSliderWidthSection(_settings.LazyModeSliderWidthStart, _settings.LazyModeSliderWidthEnd, () =>
            {
                SettingsUiRenderHelpers.DrawRangeNodeControl("Click Limiting (ms)##ControlsLazyModeClickLimiting", _settings.LazyModeClickLimiting, 80, 1000, "When lazy mode is enabled, this sets the minimum delay (in milliseconds)\nthat must pass between consecutive clicks performed by the plugin.\nThis limiter applies to all automated clicks (shrines, altars, strongboxes, etc.)\nonly while lazy mode is active. Increase this value to reduce click spam and\nprevent the plugin from taking control away from you.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Restore Cursor Delay (ms)##ControlsLazyModeRestoreCursorDelay", _settings.LazyModeRestoreCursorDelayMs, 0, 40, "Delay before restoring cursor position after a lazy-mode click when cursor restore is enabled.\n\nWhen set below 20, this may cause the plugin to have to click an item multiple times to pick it up.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Item Hover Sleep (ms)##ControlsLazyModeUiHoverSleep", _settings.LazyModeUIHoverSleep, 20, 40, "Sleep duration before UIHover verification in lazy mode.\nIncrease if you notice the mouse moving and not successfully clicking on things when it should.\n\nA value of 20 is recommended.");
                SettingsUiRenderHelpers.DrawRangeNodeControl("Lever Reclick Delay (ms)##ControlsLazyModeLeverReclickDelay", _settings.LazyModeLeverReclickDelay, 10000, 30000, "When lazy mode is enabled, prevents repeatedly clicking the same lever too quickly.\nIncrease this value if a lever is being clicked repeatedly.");
            });

            SettingsUiRenderHelpers.DrawHotkeyNodeControl(_settings.LazyModeDisableKey, "Disable Hotkey##ControlsLazyModeDisableHotkey", "When lazy mode is enabled and active, holding this key will temporarily disable lazy mode clicking.\nThis allows you to pause automated clicking without disabling lazy mode entirely.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Disable Hotkey Toggle Mode##ControlsLazyModeDisableHotkeyToggleMode", _settings.LazyModeDisableKeyToggleMode, "When enabled, pressing the Disable Hotkey toggles lazy mode clicking on/off until you press it again.\nWhen disabled, the hotkey works as hold-to-disable.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Restore Cursor Position after Each Click##ControlsLazyModeRestoreCursor", _settings.RestoreCursorInLazyMode, "When enabled, restores cursor to original position after clicking in lazy mode.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Disable Lazy Mode while Left Click Held##ControlsLazyModeDisableLeftClickHeld", _settings.DisableLazyModeLeftClickHeld, "When enabled, holding left mouse button will disable lazy mode auto-clicking.");
            SettingsUiRenderHelpers.DrawToggleNodeControl("Disable Lazy Mode while Right Click Held##ControlsLazyModeDisableRightClickHeld", _settings.DisableLazyModeRightClickHeld, "When enabled, holding right mouse button will disable lazy mode auto-clicking.");

            ImGui.Spacing();
            ImGui.TextDisabled("Nearby Monster Blockers");
            SettingsUiRenderHelpers.DrawInlineTooltip("Prevents lazy mode clicking when nearby monster density reaches your configured thresholds.");
            _settings.LazyModeNearbyMonsterRulesPanel.DrawDelegate?.Invoke();

            ImGui.TreePop();
        }

        private static void DrawSliderWidthSection(CustomNode start, CustomNode end, Action drawContent)
        {
            start.DrawDelegate?.Invoke();
            drawContent();
            end.DrawDelegate?.Invoke();
        }
    }
}