namespace ClickIt.UI.Settings.Panels
{
    internal sealed class DebugTestingPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public void Draw()
        {
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Debug Mode",
                _settings.DebugMode,
                "Enables debug mode to help with troubleshooting issues.");

            if (_settings.DebugMode.Value)
            {
                ImGui.Indent();

                bool visible = _settings.DebugWindowVisible.Value;
                if (ImGui.Checkbox("Show Debug Overlay Window", ref visible))
                    _settings.DebugWindowVisible.Value = visible;
                ImGui.Spacing();

                bool showUnclickableScreenRegions = _settings.DebugShowUnclickableScreenRegions.Value;
                if (ImGui.Checkbox("Display Unclickable Screen Regions", ref showUnclickableScreenRegions))
                    _settings.DebugShowUnclickableScreenRegions.Value = showUnclickableScreenRegions;
                ImGui.Spacing();

                SettingsUiRenderHelpers.DrawRangeNodeControl(
                    "Freeze Successful Interaction Debug (ms)",
                    _settings.DebugFreezeSuccessfulInteractionMs,
                    0,
                    20000,
                    "When greater than 0, ClickIt holds the current debug telemetry snapshot for this many milliseconds after a successful automated click or offscreen traversal.");
                ImGui.Spacing();

                SettingsUiRenderHelpers.DrawToggleNodeControl(
                    "Log messages",
                    _settings.LogMessages,
                    "This will flood your log and screen with debug text.");

                ImGui.Unindent();
            }

            SettingsUiRenderHelpers.DrawButtonNodeControl("Report Bug", _settings.ReportBugButton, "If you run into a bug that hasn't already been reported, please report it here.");
        }
    }
}