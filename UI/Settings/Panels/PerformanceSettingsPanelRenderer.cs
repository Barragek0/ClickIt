namespace ClickIt.UI.Settings.Panels
{
    internal sealed class PerformanceSettingsPanelRenderer(ClickItSettings settings)
    {
        private static readonly Vector4 BodyTextColor = new(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Vector4 HighlightTextColor = new(0.85f, 0.85f, 0.5f, 1f);
        private static readonly Vector4 ValueTextColor = new(0.4f, 0.9f, 0.5f, 1f);
        private static readonly Vector4 RedTextColor = new(1f, 0.4f, 0.4f, 1f);
        private static readonly Vector4 WarningTextColor = new(1f, 0.6f, 0.6f, 1f);

        private static readonly Vector4 GreenButtonColor = new(0.15f, 0.5f, 0.2f, 1f);
        private static readonly Vector4 GreenButtonHoveredColor = new(0.2f, 0.6f, 0.25f, 1f);
        private static readonly Vector4 GreenButtonActiveColor = new(0.1f, 0.4f, 0.15f, 1f);

        private static readonly Vector4 RedButtonColor = new(0.55f, 0.15f, 0.15f, 1f);
        private static readonly Vector4 RedButtonHoveredColor = new(0.65f, 0.2f, 0.2f, 1f);
        private static readonly Vector4 RedButtonActiveColor = new(0.45f, 0.1f, 0.1f, 1f);

        private const string SetupPopupTitle = "Performance Setup##ClickItPerformance";
        private const string ParseServerEntitiesName = "Parse Server Entities";
        private const string FpsExplanation = "Target FPS is 70 because ExileAPI runs a few frames below the value you set, so 70 keeps you at a steady 60 - values above 70 can cause ExileAPI to freeze temporarily in some situations when using this plugin.";
        private const string ParseServerExplanation = "Parse Server Entities reads the server's entity stream so ClickIt can see and target things the game has not loaded in yet - keep it ON.";
        private const string ProcessLassoWarning = "If you use Process Lasso and have the CPU -> 'ProBalance' feature enabled, you must exclude Loader.exe (ExileAPI) from it, otherwise, ExileAPI may sometimes hang when used with ClickIt.";

        private static Func<PerformanceSettingsPanelRenderer?>? s_current;
        // Set by the debug-box "Reset setup flag" button so the setup popup shows again after reload
        // even when every recommended setting is already correct (otherwise the empty-changes
        // auto-confirm silently swallows the reset). Cleared once the popup is confirmed.
        private static bool s_forceShowSetup;

        private readonly ClickItSettings _settings = settings;
        private string? _applyResult;
        private bool _showSkipConfirmation;

        internal static void SetCurrent(Func<PerformanceSettingsPanelRenderer?>? current)
            => s_current = current;

        internal static void ForceShowSetup()
            => s_forceShowSetup = true;

        // Main-render hook: draws the first-run setup popup from the active panel renderer, if any.
        internal static void DrawStartupSetupFlow()
            => s_current?.Invoke()?.DrawSetupFlow();

        // Settings-section entry point: the post-confirm performance guide.
        public void Draw()
        {
            List<RecommendedSettingChange>? changes = ExileCorePerformanceApplier.GetRecommendedChanges();
            if (changes is { Count: 0 })
            {
                SettingsUiRenderHelpers.DrawWrappedText(
                    "All of the recommended ExileAPI settings are already set.",
                    BodyTextColor);
                DrawProcessLassoWarning();
                return;
            }

            DrawRecommendedSettings(changes);

            ImGui.Spacing();
            if (DrawGreenButton("Apply Recommended Settings"))
                ApplyRecommended();
            DrawApplyResult();

            DrawProcessLassoWarning();
        }

        // Main-render entry point: shows the first-run setup popup on plugin start, not only when
        // the settings window is open.
        internal void DrawSetupFlow()
        {
            if (_settings.ShownPerformanceConfirmation.Value)
            {
                s_forceShowSetup = false;
                return;
            }

            List<RecommendedSettingChange>? changes = ExileCorePerformanceApplier.GetRecommendedChanges();
            if (changes is null)
                return; // game not ready yet; retry next frame

            if (changes.Count == 0 && !s_forceShowSetup)
            {
                _settings.ShownPerformanceConfirmation.Value = true;
                return;
            }

            // A debug-box reset only takes effect after ExileAPI reloads, not immediately.
            if (ExileCorePerformanceApplier.SuppressSetupUntilReload)
                return;

            DrawSetupPopup(changes);
        }

        private void DrawSetupPopup(List<RecommendedSettingChange>? changes)
        {
            CenterPopupOnScreen();
            ImGui.OpenPopup(SetupPopupTitle);

            // One modal only; the skip confirmation swaps in as the same window's content so no
            // second popup is stacked on top. X closes it as confirm; ESC leaves it open.
            bool open = true;
            if (ImGui.BeginPopupModal(SetupPopupTitle, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.SetWindowFocus(SetupPopupTitle);

                if (_showSkipConfirmation)
                    DrawSkipConfirmationContent();
                else
                    DrawSetupContent(changes);

                ImGui.EndPopup();
            }
            else if (!open && !ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                _settings.ShownPerformanceConfirmation.Value = true;
            }
        }

        private void DrawSetupContent(List<RecommendedSettingChange>? changes)
        {
            SettingsUiRenderHelpers.DrawWrappedText(
                "ClickIt works best when a few of your ExileAPI settings are changed. Review the list below, then apply the recommended settings automatically or set them yourself in the ExileAPI menu. You will only see this screen once.",
                BodyTextColor);
            ImGui.Spacing();

            DrawRecommendedSettings(changes);

            ImGui.Spacing();
            if (DrawGreenButton("Apply Recommended Settings"))
                ApplyRecommended();
            ImGui.SameLine();
            if (DrawRedButton("Confirm, don't show this again"))
                _showSkipConfirmation = true;
            DrawApplyResult();

            DrawProcessLassoWarning();
        }

        private void DrawSkipConfirmationContent()
        {
            SettingsUiRenderHelpers.DrawWrappedText(
                "Are you sure you don't want to apply the recommended settings in ExileAPI? You may face issues with the plugin, or ExileAPI itself if you choose not to apply them.",
                BodyTextColor);

            ImGui.Spacing();
            if (DrawRedButton("Skip"))
            {
                _settings.ShownPerformanceConfirmation.Value = true;
                _showSkipConfirmation = false;
            }
            ImGui.SameLine();
            if (DrawGreenButton("Cancel"))
                _showSkipConfirmation = false;
        }

        private static void CenterPopupOnScreen()
        {
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(
                new NumVector2(viewport.WorkPos.X + (viewport.WorkSize.X * 0.5f), viewport.WorkPos.Y + (viewport.WorkSize.Y * 0.5f)),
                ImGuiCond.Appearing,
                new NumVector2(0.5f, 0.5f));
        }

        private static void DrawRecommendedSettings(List<RecommendedSettingChange>? changes)
        {
            if (changes is not { Count: > 0 })
            {
                DrawFallbackRows();
                return;
            }

            ImGui.Indent();
            bool fpsShown = false;
            bool parseShown = false;
            foreach (RecommendedSettingChange change in changes)
            {
                DrawChangeRow(change);
                ImGui.Spacing();

                if (!fpsShown && IsFpsSetting(change.Name))
                {
                    fpsShown = true;
                    DrawFpsExplanation();
                }
                else if (!parseShown && change.Name == ParseServerEntitiesName)
                {
                    parseShown = true;
                    DrawParseServerExplanation();
                }
            }
            ImGui.Unindent();
        }

        private static void DrawFallbackRows()
        {
            ImGui.Indent();
            DrawSettingRow("Coroutine Multi Threading", "ON");
            DrawSettingRow("Parse Entities in Multi Thread", "ON");
            DrawSettingRow("Threads count", ExileCorePerformanceApplier.ResolveRecommendedThreadCount().ToString());
            DrawSettingRow("Target FPS", "70");
            DrawFpsExplanation();
            DrawSettingRow("Target Parallel Coroutine FPS", "70");
            DrawSettingRow("Entities Fps", "70");
            DrawSettingRow("Parse Server Entities", "ON");
            DrawParseServerExplanation();
            ImGui.Unindent();
        }

        private static void DrawChangeRow(RecommendedSettingChange change)
        {
            ImGui.TextUnformatted(change.Name);
            ImGui.SameLine();
            ImGui.TextColored(RedTextColor, change.CurrentText);
            ImGui.SameLine();
            ImGui.TextUnformatted("->");
            ImGui.SameLine();
            ImGui.TextColored(ValueTextColor, change.NewText);
        }

        private static bool IsFpsSetting(string name)
            => name is "Target FPS" or "Target Parallel Coroutine FPS" or "Entities Fps";

        private static void DrawFpsExplanation()
            => DrawExplanation(FpsExplanation);

        private static void DrawParseServerExplanation()
            => DrawExplanation(ParseServerExplanation);

        private static void DrawProcessLassoWarning()
        {
            ImGui.Spacing();
            SettingsUiRenderHelpers.DrawWrappedText(ProcessLassoWarning, WarningTextColor);
            ImGui.Spacing();
        }

        private static void DrawExplanation(string text)
        {
            ImGui.Spacing();
            SettingsUiRenderHelpers.DrawWrappedText(text, HighlightTextColor);
            ImGui.Spacing();
        }

        private static bool DrawGreenButton(string label)
            => DrawColoredButton(label, GreenButtonColor, GreenButtonHoveredColor, GreenButtonActiveColor);

        private static bool DrawRedButton(string label)
            => DrawColoredButton(label, RedButtonColor, RedButtonHoveredColor, RedButtonActiveColor);

        private static bool DrawColoredButton(string label, Vector4 color, Vector4 hoveredColor, Vector4 activeColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, color);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hoveredColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, activeColor);
            bool clicked = ImGui.Button(label);
            ImGui.PopStyleColor(3);
            return clicked;
        }

        private void ApplyRecommended()
        {
            _applyResult = ExileCorePerformanceApplier.TryApplyRecommended()
                ? ""
                : "Could not apply the settings automatically; please set them manually in the ExileAPI menu.";
        }

        private void DrawApplyResult()
        {
            if (string.IsNullOrEmpty(_applyResult))
                return;

            ImGui.TextColored(HighlightTextColor, _applyResult);
        }

        private static void DrawSettingRow(string name, string value)
        {
            ImGui.TextUnformatted(name);
            ImGui.SameLine();
            ImGui.TextColored(ValueTextColor, value);
        }
    }
}
