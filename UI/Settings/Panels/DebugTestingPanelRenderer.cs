namespace ClickIt.UI.Settings.Panels
{
    internal sealed class DebugTestingPanelRenderer(ClickItSettings settings)
    {
        private static readonly string[] DumpTargetNames = Enum.GetNames<GameStateDumpTarget>();

        private readonly ClickItSettings _settings = settings;
        private int _selectedDumpTarget;

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

                ImGui.Spacing();

                // Game-state dump tool (gated by GameStateDumpCoordinator.Enabled in code).
                GameStateDumpCoordinator? dump = GameStateDumpCoordinator.Current;
                if (GameStateDumpCoordinator.Enabled && dump != null)
                {
                    GameStateDumpSnapshot dumpState = dump.GetProgress();
                    ImGui.TextUnformatted("Game State Dump");
                    ImGui.SetNextItemWidth(150f);
                    ImGui.Combo("Area", ref _selectedDumpTarget, DumpTargetNames, DumpTargetNames.Length);
                    ImGui.SameLine();
                    ImGui.BeginDisabled(dumpState.InProgress);
                    if (ImGui.Button("Dump"))
                        dump.QueueDump((GameStateDumpTarget)_selectedDumpTarget);
                    ImGui.SameLine();
                    if (ImGui.Button("Dump all"))
                        dump.QueueDumpAll();
                    ImGui.EndDisabled();
                    if (dumpState.InProgress)
                    {
                        ImGui.ProgressBar(dumpState.ProgressPercent / 100f, new NumVector2(300f, 0f));
                        ImGui.SameLine();
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.65f, 0.10f, 0.10f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.80f, 0.15f, 0.15f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.55f, 0.08f, 0.08f, 1f));
                        if (ImGui.Button("Cancel"))
                            dump.CancelDump();
                        ImGui.PopStyleColor(3);
                    }
                    if (dumpState.StatusText.Length > 0)
                        ImGui.TextUnformatted(dumpState.StatusText);
                    foreach (string error in dumpState.Errors)
                        ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), error);
                    if (dumpState.Steps.Count > 0)
                    {
                        ImGui.TextUnformatted("Recent Steps");
                        ImGui.SameLine();
                        if (ImGui.Button("Copy"))
                            _ = ClipboardText.TryCopy(string.Join(Environment.NewLine, dumpState.Steps));
                        if (ImGui.BeginTable("DumpSteps", 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                        {
                            ImGui.TableSetupColumn("Step", ImGuiTableColumnFlags.WidthFixed, 450f);
                            for (int i = 0; i < dumpState.Steps.Count; i++)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.TextUnformatted(dumpState.Steps[i]);
                            }
                            ImGui.EndTable();
                        }
                    }
                    ImGui.Spacing();
                }

                ImGui.Unindent();
            }

            SettingsUiRenderHelpers.DrawButtonNodeControl("Report Bug", _settings.ReportBugButton, "If you run into a bug that hasn't already been reported, please report it here.");
        }
    }
}