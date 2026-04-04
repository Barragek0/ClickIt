namespace ClickIt.UI.Settings.Panels
{
    internal sealed class AltarSettingsPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public void DrawAltarsPanel(bool embedded = false)
        {
            bool weightTablesExpanded = false;

            DrawExarchSection();
            DrawEaterSection();
            weightTablesExpanded = DrawAltarWeightingSection();

            DrawAlertSoundSection();

            if (embedded)
            {
                _settings.UiState.MechanicsAltarWeightTablesExpanded = weightTablesExpanded;
            }
        }

        public void DrawAltarModWeights()
        {
            DrawUpsideModsSection();
            DrawDownsideModsSection();
        }

        private void DrawExarchSection()
        {
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Show Searing Exarch Overlay##Exarch",
                _settings.HighlightExarchAltars,
                "Highlights the recommended option for you to choose for searing exarch altars, based on a decision tree created from your settings below.");
        }

        private void DrawEaterSection()
        {
            SettingsUiRenderHelpers.DrawToggleNodeControl(
                "Show Eater of Worlds Overlay##Eater",
                _settings.HighlightEaterAltars,
                "Highlights the recommended option for you to choose for eater of worlds altars, based on a decision tree created from your settings below.");
        }

        private bool DrawAltarWeightingSection()
        {
            if (!ImGui.TreeNode("Altar Weights"))
                return false;

            bool upsideExpanded = DrawUpsideModsSection();
            bool downsideExpanded = DrawDownsideModsSection();
            DrawAltarWeightOverridesSection();

            ImGui.TreePop();

            return upsideExpanded || downsideExpanded;
        }

        private void DrawAltarWeightOverridesSection()
        {
            if (!ImGui.TreeNode("Altar Weight Overrides"))
                return;

            SettingsUiRenderHelpers.DrawToggleAndRangeNodeControls(
                "Valuable Upside",
                _settings.ValuableUpside,
                "When enabled, automatically chooses the altar option with modifiers that have weights above the threshold, even if the overall weight calculation would suggest otherwise.",
                "Valuable Upside Threshold",
                _settings.ValuableUpsideThreshold,
                1,
                100,
                "Minimum weight threshold for upside modifiers to trigger the high value override. Modifiers with weights at or above this value will cause the plugin to choose that altar option.",
                rangeWidthOverride: 300f);
            SettingsUiRenderHelpers.DrawToggleAndRangeNodeControls(
                "Unvaluable Upside",
                _settings.UnvaluableUpside,
                "When enabled, automatically chooses the opposite altar option when modifiers have weights at or below the threshold, avoiding potentially undesirable choices.",
                "Unvaluable Upside Threshold",
                _settings.UnvaluableUpsideThreshold,
                1,
                100,
                "Weight threshold that triggers the low value override. When any modifier has a weight at or below this value, the plugin will choose the opposite altar option.",
                rangeWidthOverride: 300f);
            SettingsUiRenderHelpers.DrawToggleAndRangeNodeControls(
                "Dangerous Downside",
                _settings.DangerousDownside,
                "When enabled, automatically avoids altar options with dangerous downside modifiers that have weights above the threshold.",
                "Dangerous Downside Threshold",
                _settings.DangerousDownsideThreshold,
                1,
                100,
                "Maximum weight threshold for downside modifiers to trigger the dangerous override. Modifiers with weights at or above this value will cause the plugin to choose the opposite altar option.",
                rangeWidthOverride: 300f);
            SettingsUiRenderHelpers.DrawToggleAndRangeNodeControls(
                "Minimum Weight",
                _settings.MinWeightThresholdEnabled,
                "When enabled, the plugin will enforce a minimum final weight for altar options. If an option's final weight is below this value the plugin will avoid picking it (and will choose the opposite option if available).",
                "Minimum Weight Threshold",
                _settings.MinWeightThreshold,
                1,
                100,
                "Minimum final weight (1 - 100) an option must have to be considered valid. If both options are below this value, neither will be auto-chosen.",
                rangeWidthOverride: 300f);

            ImGui.TreePop();
        }

        private void DrawAlertSoundSection()
        {
            if (!ImGui.TreeNode("Alert Sound"))
                return;

            SettingsUiRenderHelpers.DrawToggleNodeControl("Auto-download Default Alert Sound", _settings.AutoDownloadAlertSound, "When enabled the plugin will attempt to download a default 'alert.wav' from the project's GitHub repository into your plugin config folder if the file is missing.");

            SettingsUiRenderHelpers.DrawButtonNodeControl("Open Config Directory", _settings.OpenConfigDirectory, "Open the plugin config directory where you should put 'alert.wav'");
            SettingsUiRenderHelpers.DrawButtonNodeControl("Reload Alert Sound", _settings.ReloadAlertSound, "Reloads the 'alert.wav' sound file from the config directory");

            SettingsUiRenderHelpers.DrawRangeNodeControl("Alert Volume", _settings.AlertSoundVolume, 0, 100, "Volume to play alert sound at (0-100)");

            ImGui.TreePop();
        }

        private bool DrawUpsideModsSection()
            => SettingsUiRenderHelpers.DrawAltarModSection(
                _settings,
                SettingsUiRenderHelpers.GetUpsideAltarModSectionDescriptor(),
                ref _settings.UiState.UpsideSearchFilter,
                AltarModsConstants.UpsideMods,
                static (type, _) => SettingsUiRenderHelpers.GetAltarUpsideSectionStyle(type));

        private bool DrawDownsideModsSection()
            => SettingsUiRenderHelpers.DrawAltarModSection(
                _settings,
                SettingsUiRenderHelpers.GetDownsideAltarModSectionDescriptor(),
                ref _settings.UiState.DownsideSearchFilter,
                AltarModsConstants.DownsideMods,
                static (_, defaultWeight) => SettingsUiRenderHelpers.GetAltarDownsideSectionStyle(defaultWeight));

    }
}