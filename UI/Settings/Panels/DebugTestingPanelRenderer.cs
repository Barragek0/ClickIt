namespace ClickIt.UI.Settings.Panels
{
    internal sealed class DebugTestingPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        private readonly struct DebugSectionToggleDescriptor(string label, ToggleNode node, string tooltip)
        {
            public string Label { get; } = label;
            public ToggleNode Node { get; } = node;
            public string Tooltip { get; } = tooltip;
        }

        public void Draw()
        {
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Debug Mode",
                _settings.DebugMode,
                "Enables debug mode to help with troubleshooting issues.");

            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Additional Debug Information",
                _settings.RenderDebug,
                "Provides more debug text related to rendering the overlay.");

            if (ImGui.Button("Copy Additional Debug Information"))
            {
                SettingsUiRenderHelpers.TriggerButtonNode(_settings.CopyAdditionalDebugInfoButton);
            }
            SettingsUiRenderHelpers.DrawInlineTooltip("Copies the current Additional Debug Information text to clipboard.");

            if (_settings.MemoryDumpInProgress)
            {
                float progress = Math.Clamp(_settings.MemoryDumpProgressPercent, 0, 100) / 100f;
                ImGui.ProgressBar(progress, new NumVector2(-1f, 0f), $"Memory Dump: {_settings.MemoryDumpProgressPercent}%");
                if (!string.IsNullOrWhiteSpace(_settings.MemoryDumpStatusText))
                    ImGui.TextWrapped(_settings.MemoryDumpStatusText);
            }
            else if (_settings.MemoryDumpLastRunSucceeded && !string.IsNullOrWhiteSpace(_settings.MemoryDumpOutputPath))
            {
                ImGui.TextColored(new Vector4(0.25f, 0.85f, 0.35f, 1.0f), "memoryData.dat written to:");
                ImGui.TextWrapped(_settings.MemoryDumpOutputPath);
            }
            else if (!string.IsNullOrWhiteSpace(_settings.MemoryDumpStatusText))
            {
                ImGui.TextColored(new Vector4(0.95f, 0.45f, 0.35f, 1.0f), _settings.MemoryDumpStatusText);
            }

            if (_settings.RenderDebug.Value)
            {
                ImGui.Indent();
                SettingsUiRenderHelpers.DrawRangeNodeControl(
                    "Freeze Successful Interaction Debug (ms)",
                    _settings.DebugFreezeSuccessfulInteractionMs,
                    0,
                    20000,
                    "When greater than 0, ClickIt holds the current Additional Debug Information telemetry snapshot for this many milliseconds after a successful automated click or offscreen traversal.\n\nThis prevents later debug updates from immediately overwriting the evidence you want to inspect,\n\nand also gives you time to copy the Additional Debug Information to clipboard after a successful interaction, before it gets overwritten.");
                DrawDebugSectionToggles();
                ImGui.Unindent();
            }

            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Auto Copy Inventory Warning Debug",
                _settings.AutoCopyInventoryWarningDebug,
                "Automatically copies inventory warning debug details when the 'Your inventory is full' overlay is triggered. Copy attempts are throttled to once per second.");

            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Log messages",
                _settings.LogMessages,
                "This will flood your log and screen with debug text.");

            if (ImGui.Button("Report Bug"))
            {
                SettingsUiRenderHelpers.TriggerButtonNode(_settings.ReportBugButton);
            }
            SettingsUiRenderHelpers.DrawInlineTooltip("If you run into a bug that hasn't already been reported, please report it here.");
        }

        private void DrawDebugSectionToggles()
        {
            var toggles = new[]
            {
                new DebugSectionToggleDescriptor("Status", _settings.DebugShowStatus, "Show/hide the status debug section"),
                new DebugSectionToggleDescriptor("Game State", _settings.DebugShowGameState, "Show/hide the Game State debug section"),
                new DebugSectionToggleDescriptor("Performance", _settings.DebugShowPerformance, "Show/hide the performance debug section"),
                new DebugSectionToggleDescriptor("Click Frequency Target", _settings.DebugShowClickFrequencyTarget, "Show/hide the Click Frequency Target debug section"),
                new DebugSectionToggleDescriptor("Altar Detection", _settings.DebugShowAltarDetection, "Show/hide the Altar Detection debug section"),
                new DebugSectionToggleDescriptor("Altar Service", _settings.DebugShowAltarService, "Show/hide the Altar Service debug section"),
                new DebugSectionToggleDescriptor("Labels", _settings.DebugShowLabels, "Show/hide the labels debug section"),
                new DebugSectionToggleDescriptor("Inventory Pickup", _settings.DebugShowInventoryPickup, "Show/hide inventory pickup/fullness debug section"),
                new DebugSectionToggleDescriptor("Hovered Item Metadata", _settings.DebugShowHoveredItemMetadata, "Show/hide the hovered item metadata debug section"),
                new DebugSectionToggleDescriptor("Pathfinding", _settings.DebugShowPathfinding, "Show/hide offscreen pathfinding debug section"),
                new DebugSectionToggleDescriptor("Ultimatum", _settings.DebugShowUltimatum, "Show/hide ultimatum automation debug section"),
                new DebugSectionToggleDescriptor("Clicking", _settings.DebugShowClicking, "Show/hide clicking debug section"),
                new DebugSectionToggleDescriptor("Debug Log Overlay", _settings.DebugShowRuntimeDebugLogOverlay, "Show/hide overlay section that displays DebugLog messages as a recent-stage style trail"),
                new DebugSectionToggleDescriptor("Recent Errors", _settings.DebugShowRecentErrors, "Show/hide the Recent Errors debug section"),
                new DebugSectionToggleDescriptor("Debug Frames", _settings.DebugShowFrames, "Show/hide the debug screen area frames")
            };

            foreach (DebugSectionToggleDescriptor toggle in toggles)
            {
                SettingsUiRenderHelpers.DrawToggleNodeControl(toggle.Label, toggle.Node, toggle.Tooltip);
            }
        }
    }
}