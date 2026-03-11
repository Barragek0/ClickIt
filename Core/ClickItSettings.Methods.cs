using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using System.Numerics;
using ClickIt.Definitions;

namespace ClickIt
{
    public partial class ClickItSettings : ISettings
    {
        private void DrawPanelSafe(string panelName, Action drawAction)
        {
            try
            {
                drawAction();
            }
            catch (Exception ex)
            {
                _lastSettingsUiError = $"{panelName}: {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ClickItSettings UI Error] {_lastSettingsUiError}{Environment.NewLine}{ex}");

                ImGui.Separator();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Settings UI error caught");
                ImGui.TextWrapped(_lastSettingsUiError);

                if (ImGui.Button($"Throw Last UI Error##{panelName}"))
                {
                    throw new InvalidOperationException(_lastSettingsUiError, ex);
                }
            }
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

            if (RenderDebug.Value)
            {
                ImGui.Indent();
                DrawToggleNodeControl("Status", DebugShowStatus, "Show/hide the Status debug section");
                DrawToggleNodeControl("Game State", DebugShowGameState, "Show/hide the Game State debug section");
                DrawToggleNodeControl("Performance", DebugShowPerformance, "Show/hide the Performance debug section");
                DrawToggleNodeControl("Click Frequency Target", DebugShowClickFrequencyTarget, "Show/hide the Click Frequency Target debug section");
                DrawToggleNodeControl("Altar Detection", DebugShowAltarDetection, "Show/hide the Altar Detection debug section");
                DrawToggleNodeControl("Altar Service", DebugShowAltarService, "Show/hide the Altar Service debug section");
                DrawToggleNodeControl("Labels", DebugShowLabels, "Show/hide the Labels debug section");
                DrawToggleNodeControl("Hovered Item Metadata", DebugShowHoveredItemMetadata, "Show/hide the hovered item metadata debug section");
                DrawToggleNodeControl("Recent Errors", DebugShowRecentErrors, "Show/hide the Recent Errors debug section");
                DrawToggleNodeControl("Debug Frames", DebugShowFrames, "Show/hide the debug screen area frames");
                ImGui.Unindent();
            }

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
    }
}
