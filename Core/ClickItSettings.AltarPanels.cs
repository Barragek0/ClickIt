using ImGuiNET;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private void DrawAltarsPanel()
        {
            DrawExarchSection();
            DrawEaterSection();
            DrawAltarWeightingSection();
            DrawAlertSoundSection();
        }

        private void DrawExarchSection()
        {
            if (!ImGui.TreeNode("Searing Exarch"))
                return;

            DrawToggleNodeControl(
                "Click recommended option##Exarch",
                ClickExarchAltars,
                "Clicks searing exarch altars for you based on a decision tree created from your settings.\n\nIf both options are as good as each other (according to your weights), this won't click for you.");

            DrawToggleNodeControl(
                "Highlight recommended option##Exarch",
                HighlightExarchAltars,
                "Highlights the recommended option for you to choose for searing exarch altars, based on a decision tree created from your settings below.");

            ImGui.TreePop();
        }

        private void DrawEaterSection()
        {
            if (!ImGui.TreeNode("Eater of Worlds"))
                return;

            DrawToggleNodeControl(
                "Click recommended option##Eater",
                ClickEaterAltars,
                "Clicks eater of worlds altars for you based on a decision tree created from your settings.\n\nIf both options are as good as each other (according to your weights), this won't click for you.");

            DrawToggleNodeControl(
                "Highlight recommended option##Eater",
                HighlightEaterAltars,
                "Highlights the recommended option for you to choose for eater of worlds altars, based on a decision tree created from your settings below.");

            ImGui.TreePop();
        }

        private void DrawAltarWeightingSection()
        {
            if (!ImGui.TreeNode("Altar Weighting"))
                return;

            DrawAltarModWeights();

            DrawToggleNodeControl(
                "Valuable Upside",
                ValuableUpside,
                "When enabled, automatically chooses the altar option with modifiers that have weights above the threshold, even if the overall weight calculation would suggest otherwise.");

            DrawRangeNodeControl(
                "Valuable Upside Threshold",
                ValuableUpsideThreshold,
                1,
                100,
                "Minimum weight threshold for upside modifiers to trigger the high value override. Modifiers with weights at or above this value will cause the plugin to choose that altar option.");

            DrawToggleNodeControl(
                "Unvaluable Upside",
                UnvaluableUpside,
                "When enabled, automatically chooses the opposite altar option when modifiers have weights at or below the threshold, avoiding potentially undesirable choices.");

            DrawRangeNodeControl(
                "Unvaluable Threshold",
                UnvaluableUpsideThreshold,
                1,
                100,
                "Weight threshold that triggers the low value override. When any modifier has a weight at or below this value, the plugin will choose the opposite altar option.");

            DrawToggleNodeControl(
                "Dangerous Downside",
                DangerousDownside,
                "When enabled, automatically avoids altar options with dangerous downside modifiers that have weights above the threshold.");

            DrawRangeNodeControl(
                "Dangerous Downside Threshold",
                DangerousDownsideThreshold,
                1,
                100,
                "Maximum weight threshold for downside modifiers to trigger the dangerous override. Modifiers with weights at or above this value will cause the plugin to choose the opposite altar option.");

            DrawToggleNodeControl(
                "Minimum Weight Threshold",
                MinWeightThresholdEnabled,
                "When enabled, the plugin will enforce a minimum final weight for altar options. If an option's final weight is below this value the plugin will avoid picking it (and will choose the opposite option if available).");

            DrawRangeNodeControl(
                "Minimum Weight Value",
                MinWeightThreshold,
                1,
                100,
                "Minimum final weight (1 - 100) an option must have to be considered valid. If both options are below this value, neither will be auto-chosen.");

            ImGui.TreePop();
        }

        private void DrawAlertSoundSection()
        {
            if (!ImGui.TreeNode("Alert Sound"))
                return;

            DrawToggleNodeControl(
                "Auto-download Default Alert Sound",
                AutoDownloadAlertSound,
                "When enabled the plugin will attempt to download a default 'alert.wav' from the project's GitHub repository into your plugin config folder if the file is missing.");

            if (ImGui.Button("Open Config Directory"))
            {
                TriggerButtonNode(OpenConfigDirectory);
            }
            DrawInlineTooltip("Open the plugin config directory where you should put 'alert.wav'");

            if (ImGui.Button("Reload Alert Sound"))
            {
                TriggerButtonNode(ReloadAlertSound);
            }
            DrawInlineTooltip("Reloads the 'alert.wav' sound file from the config directory");

            DrawRangeNodeControl(
                "Alert Volume",
                AlertSoundVolume,
                0,
                100,
                "Volume to play alert sound at (0-100)");

            ImGui.TreePop();
        }
    }
}