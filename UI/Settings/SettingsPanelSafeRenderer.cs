namespace ClickIt.UI.Settings
{
    internal readonly record struct SettingsPanelSafeRenderHooks(
        Action Separator,
        Action<Vector4, string> TextColored,
        Action<string> TextWrapped,
        Func<string, bool> Button)
    {
        internal static readonly SettingsPanelSafeRenderHooks ImGui = new(
            static () => ImGui.Separator(),
            static (color, text) => ImGui.TextColored(color, text),
            static text => ImGui.TextWrapped(text),
            static label => ImGui.Button(label));
    }

    internal sealed class SettingsPanelSafeRenderer
    {
        private readonly ClickItSettings _settings;
        private readonly SettingsPanelSafeRenderHooks _hooks;

        internal SettingsPanelSafeRenderer(ClickItSettings settings)
            : this(settings, SettingsPanelSafeRenderHooks.ImGui)
        {
        }

        internal SettingsPanelSafeRenderer(ClickItSettings settings, SettingsPanelSafeRenderHooks hooks)
        {
            _settings = settings;
            _hooks = hooks;
        }

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

                _hooks.Separator();
                _hooks.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Settings UI error caught");
                _hooks.TextWrapped(_settings.UiState.LastSettingsUiError);

                if (_hooks.Button($"Throw Last UI Error##{panelName}"))
                    throw new InvalidOperationException(_settings.UiState.LastSettingsUiError, ex);

            }
        }
    }
}