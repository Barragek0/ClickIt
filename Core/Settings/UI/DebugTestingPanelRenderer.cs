using ExileCore.Shared.Nodes;
using ImGuiNET;
using System;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private readonly struct DebugSectionToggleDescriptor(string label, ToggleNode node, string tooltip)
        {
            public string Label { get; } = label;
            public ToggleNode Node { get; } = node;
            public string Tooltip { get; } = tooltip;
        }

        private void DrawDebugTestingPanel()
        {
            DrawToggleNodeControl(
                "Debug Mode",
                DebugMode,
                "Enables debug mode to help with troubleshooting issues.");

            DrawToggleNodeControl(
                "Additional Debug Information",
                RenderDebug,
                "Provides more debug text related to rendering the overlay.");

            if (ImGui.Button("Copy Additional Debug Information"))
            {
                TriggerButtonNode(CopyAdditionalDebugInfoButton);
            }
            DrawInlineTooltip("Copies the current Additional Debug Information text to clipboard.");

            if (MemoryDumpInProgress)
            {
                float progress = Math.Clamp(MemoryDumpProgressPercent, 0, 100) / 100f;
                ImGui.ProgressBar(progress, new Vector2(-1f, 0f), $"Memory Dump: {MemoryDumpProgressPercent}%");
                if (!string.IsNullOrWhiteSpace(MemoryDumpStatusText))
                    ImGui.TextWrapped(MemoryDumpStatusText);
            }
            else if (MemoryDumpLastRunSucceeded && !string.IsNullOrWhiteSpace(MemoryDumpOutputPath))
            {
                ImGui.TextColored(new Vector4(0.25f, 0.85f, 0.35f, 1.0f), "memoryData.dat written to:");
                ImGui.TextWrapped(MemoryDumpOutputPath);
            }
            else if (!string.IsNullOrWhiteSpace(MemoryDumpStatusText))
            {
                ImGui.TextColored(new Vector4(0.95f, 0.45f, 0.35f, 1.0f), MemoryDumpStatusText);
            }

            if (RenderDebug.Value)
            {
                ImGui.Indent();
                DrawRangeNodeControl(
                    "Freeze Successful Interaction Debug (ms)",
                    DebugFreezeSuccessfulInteractionMs,
                    0,
                    20000,
                    "When greater than 0, ClickIt holds the current Additional Debug Information telemetry snapshot for this many milliseconds after a successful automated click or offscreen traversal.\n\nThis prevents later debug updates from immediately overwriting the evidence you want to inspect,\n\nand also gives you time to copy the Additional Debug Information to clipboard after a successful interaction, before it gets overwritten.");
                DrawDebugSectionToggles();
                ImGui.Unindent();
            }

            DrawToggleNodeControl(
                "Auto Copy Inventory Warning Debug",
                AutoCopyInventoryWarningDebug,
                "Automatically copies inventory warning debug details when the 'Your inventory is full' overlay is triggered. Copy attempts are throttled to once per second.");

            DrawToggleNodeControl(
                "Log messages",
                LogMessages,
                "This will flood your log and screen with debug text.");

            if (ImGui.Button("Report Bug"))
            {
                TriggerButtonNode(ReportBugButton);
            }
            DrawInlineTooltip("If you run into a bug that hasn't already been reported, please report it here.");
        }

        private void DrawDebugSectionToggles()
        {
            var toggles = new[]
            {
                new DebugSectionToggleDescriptor("Status", DebugShowStatus, "Show/hide the status debug section"),
                new DebugSectionToggleDescriptor("Game State", DebugShowGameState, "Show/hide the Game State debug section"),
                new DebugSectionToggleDescriptor("Performance", DebugShowPerformance, "Show/hide the performance debug section"),
                new DebugSectionToggleDescriptor("Click Frequency Target", DebugShowClickFrequencyTarget, "Show/hide the Click Frequency Target debug section"),
                new DebugSectionToggleDescriptor("Altar Detection", DebugShowAltarDetection, "Show/hide the Altar Detection debug section"),
                new DebugSectionToggleDescriptor("Altar Service", DebugShowAltarService, "Show/hide the Altar Service debug section"),
                new DebugSectionToggleDescriptor("Labels", DebugShowLabels, "Show/hide the labels debug section"),
                new DebugSectionToggleDescriptor("Inventory Pickup", DebugShowInventoryPickup, "Show/hide inventory pickup/fullness debug section"),
                new DebugSectionToggleDescriptor("Hovered Item Metadata", DebugShowHoveredItemMetadata, "Show/hide the hovered item metadata debug section"),
                new DebugSectionToggleDescriptor("Pathfinding", DebugShowPathfinding, "Show/hide offscreen pathfinding debug section"),
                new DebugSectionToggleDescriptor("Ultimatum", DebugShowUltimatum, "Show/hide ultimatum automation debug section"),
                new DebugSectionToggleDescriptor("Clicking", DebugShowClicking, "Show/hide clicking debug section"),
                new DebugSectionToggleDescriptor("Debug Log Overlay", DebugShowRuntimeDebugLogOverlay, "Show/hide overlay section that displays DebugLog messages as a recent-stage style trail"),
                new DebugSectionToggleDescriptor("Recent Errors", DebugShowRecentErrors, "Show/hide the Recent Errors debug section"),
                new DebugSectionToggleDescriptor("Debug Frames", DebugShowFrames, "Show/hide the debug screen area frames")
            };

            foreach (DebugSectionToggleDescriptor toggle in toggles)
            {
                DrawToggleNodeControl(toggle.Label, toggle.Node, toggle.Tooltip);
            }
        }
    }
}