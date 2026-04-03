namespace ClickIt.UI.Settings
{
    internal sealed class SettingsPanelSafeRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        internal void DrawPanel(string panelName, Action drawAction)
        {
            try
            {
                drawAction();
            }
            catch (Exception ex)
            {
                _settings.UiState.LastSettingsUiError = $"{panelName}: {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ClickItSettings UI Error] {_settings.UiState.LastSettingsUiError}{Environment.NewLine}{ex}");

                ImGui.Separator();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Settings UI error caught");
                ImGui.TextWrapped(_settings.UiState.LastSettingsUiError);

                if (ImGui.Button($"Throw Last UI Error##{panelName}"))
                {
                    throw new InvalidOperationException(_settings.UiState.LastSettingsUiError, ex);
                }
            }
        }
    }
}