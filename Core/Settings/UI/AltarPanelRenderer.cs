using ImGuiNET;
using System;
using System.Numerics;
using ClickIt.Definitions;

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
            DrawToggleNodeControl(
                "Show Searing Exarch Overlay##Exarch",
                HighlightExarchAltars,
                "Highlights the recommended option for you to choose for searing exarch altars, based on a decision tree created from your settings below.");
        }

        private void DrawEaterSection()
        {
            DrawToggleNodeControl(
                "Show Eater of Worlds Overlay##Eater",
                HighlightEaterAltars,
                "Highlights the recommended option for you to choose for eater of worlds altars, based on a decision tree created from your settings below.");
        }

        private void DrawAltarWeightingSection()
        {
            if (!ImGui.TreeNode("Altar Weights"))
                return;

            DrawAltarModWeights();
            DrawAltarWeightOverridesSection();

            ImGui.TreePop();
        }

        private void DrawAltarWeightOverridesSection()
        {
            if (!ImGui.TreeNode("Altar Weight Overrides"))
                return;

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
                "Unvaluable Upside Threshold",
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
                "Minimum Weight",
                MinWeightThresholdEnabled,
                "When enabled, the plugin will enforce a minimum final weight for altar options. If an option's final weight is below this value the plugin will avoid picking it (and will choose the opposite option if available).");

            DrawRangeNodeControl(
                "Minimum Weight Threshold",
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

        private void DrawAltarModWeights()
        {
            DrawUpsideModsSection();
            DrawDownsideModsSection();
        }

        private void DrawUpsideModsSection()
        {
            bool isOpen = ImGui.TreeNode("Altar Weight Upsides");
            DrawInlineTooltip("Set weights for upside modifiers. Higher values are more desirable and can influence recommended altar choices.");
            if (!isOpen)
                return;

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale (Higher = More Valuable):");
            DrawWeightScale(bestAtHigh: true);
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar("##UpsideSearch", "Clear##UpsideClear", ref UiState.UpsideSearchFilter);
            ImGui.Spacing();
            DrawUpsideModsTable();
            ImGui.TreePop();
        }

        private void DrawDownsideModsSection()
        {
            bool isOpen = ImGui.TreeNode("Altar Weight Downsides");
            DrawInlineTooltip("Set weights for downside modifiers. Higher values are more dangerous and can influence recommended altar choices.");
            if (!isOpen)
                return;

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale (Higher = More Dangerous):");
            DrawWeightScale(bestAtHigh: false);
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar("##DownsideSearch", "Clear##DownsideClear", ref UiState.DownsideSearchFilter);
            ImGui.Spacing();
            DrawDownsideModsTable();
            ImGui.TreePop();
        }

        private void DrawUpsideModsTable()
        {
            if (!ImGui.BeginTable("UpsideModsConfig", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;

            SetupModTableColumns(isUpside: true);
            string currentSection = string.Empty;
            foreach ((string id, string name, string type, int _) in AltarModsConstants.UpsideMods)
            {
                if (!MatchesSearchFilter(name, type, UiState.UpsideSearchFilter))
                    continue;

                string sectionHeader = GetUpsideSectionHeader(type);
                DrawSectionHeaderIfNeeded(ref currentSection, sectionHeader, type);
                DrawUpsideModRow(id, name, type);
            }

            ImGui.EndTable();
        }

        private void DrawDownsideModsTable()
        {
            if (!ImGui.BeginTable("DownsideModsConfig", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;

            SetupModTableColumns(isUpside: false);
            string lastProcessedSection = string.Empty;
            foreach ((string id, string name, string type, int defaultWeight) in AltarModsConstants.DownsideMods)
            {
                if (!MatchesSearchFilter(name, type, UiState.DownsideSearchFilter))
                    continue;

                string sectionHeader = GetDownsideSectionHeader(defaultWeight);
                DrawDownsideSectionHeaderIfNeeded(ref lastProcessedSection, sectionHeader);
                DrawDownsideModRow(id, name, type, sectionHeader);
            }

            ImGui.EndTable();
        }

        private static void SetupModTableColumns(bool isUpside = false)
        {
            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 125);
            float modWidth = isUpside ? 760 : 830;
            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthFixed, modWidth);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 50);
            if (isUpside)
            {
                ImGui.TableSetupColumn("Alert", ImGuiTableColumnFlags.WidthFixed, 55);
            }

            ImGui.TableHeadersRow();
        }

        private static bool MatchesSearchFilter(string name, string type, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            string term = filter.Trim();
            return name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || type.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetUpsideSectionHeader(string type)
        {
            return type switch
            {
                AltarTypeMinion => "Minion Drops",
                AltarTypeBoss => "Boss Drops",
                AltarTypePlayer => "Player Bonuses",
                _ => string.Empty
            };
        }

        private static string GetDownsideSectionHeader(int defaultWeight)
        {
            return defaultWeight switch
            {
                100 => "Build Bricking Modifiers",
                >= 70 => "Very Dangerous Modifiers",
                >= 40 => "Dangerous Modifiers",
                >= 2 => "Ok Modifiers",
                _ => "Free Modifiers"
            };
        }

        private static void DrawSectionHeaderIfNeeded(ref string currentSection, string sectionHeader, string type)
        {
            if (string.IsNullOrEmpty(sectionHeader) || sectionHeader == currentSection)
                return;

            currentSection = sectionHeader;
            DrawUpsideSectionHeader(sectionHeader, type);
        }

        private static void DrawUpsideSectionHeader(string sectionHeader, string type)
        {
            DrawSectionHeaderRow(sectionHeader, GetUpsideSectionHeaderColor(type));
        }

        private static void DrawDownsideSectionHeaderIfNeeded(ref string lastProcessedSection, string sectionHeader)
        {
            if (string.IsNullOrEmpty(sectionHeader) || sectionHeader == lastProcessedSection)
                return;

            lastProcessedSection = sectionHeader;
            DrawDownsideSectionHeader(sectionHeader);
        }

        private static void DrawDownsideSectionHeader(string sectionHeader)
        {
            DrawSectionHeaderRow(sectionHeader, GetDownsideSectionHeaderColor(sectionHeader), new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
        }

        private static void DrawSectionHeaderRow(string sectionHeader, Vector4 headerColor, Vector4? textColor = null)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(headerColor));
            ImGui.Text(string.Empty);
            ImGui.TableNextColumn();

            if (textColor.HasValue)
            {
                ImGui.TextColored(textColor.Value, sectionHeader);
                return;
            }

            ImGui.Text(sectionHeader);
        }

        private static Vector4 GetUpsideSectionHeaderColor(string type)
        {
            return type switch
            {
                AltarTypeMinion => new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                AltarTypeBoss => new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                AltarTypePlayer => new Vector4(0.2f, 0.2f, 0.6f, 0.3f),
                _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f)
            };
        }

        private static Vector4 GetDownsideSectionHeaderColor(string sectionHeader)
        {
            return sectionHeader switch
            {
                "Build Bricking Modifiers" => new Vector4(1.0f, 0.0f, 0.0f, 0.6f),
                "Very Dangerous Modifiers" => new Vector4(0.9f, 0.1f, 0.1f, 0.5f),
                "Dangerous Modifiers" => new Vector4(1.0f, 0.5f, 0.0f, 0.4f),
                "Ok Modifiers" => new Vector4(1.0f, 1.0f, 0.0f, 0.3f),
                "Free Modifiers" => new Vector4(0.0f, 0.7f, 0.0f, 0.3f),
                _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f)
            };
        }

        private void DrawUpsideModRow(string id, string name, string type)
        {
            ImGui.PushID($"upside_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            DrawModWeightSliderCell(id, type);
            DrawModNameAndTypeCells(name, type, 760, GetUpsideModTextColor(type));

            if (ModAlerts != null)
            {
                _ = ImGui.TableNextColumn();
                var avail = ImGui.GetContentRegionAvail();
                const float checkboxSize = 18f;
                float currentX = ImGui.GetCursorPosX();
                float offset = (avail.X - checkboxSize) * 0.5f;
                if (offset > 0)
                {
                    ImGui.SetCursorPosX(currentX + offset);
                }

                bool currentAlert = GetModAlert(id, type);
                if (ImGui.Checkbox("##alert", ref currentAlert))
                {
                    ModAlerts[BuildCompositeKey(type, id)] = currentAlert;
                }
            }

            ImGui.PopID();
        }

        private void DrawDownsideModRow(string id, string name, string type, string sectionHeader)
        {
            ImGui.PushID($"downside_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            DrawModWeightSliderCell(id, type);
            DrawModNameAndTypeCells(name, type, 830, GetDownsideModTextColor(sectionHeader));
            ImGui.PopID();
        }

        private void DrawModWeightSliderCell(string id, string type)
        {
            _ = ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(125);
            int currentValue = GetModTier(id, type);
            if (ImGui.SliderInt("##weight", ref currentValue, 1, 100))
            {
                ModTiers[BuildCompositeKey(type, id)] = currentValue;
            }
        }

        private static void DrawModNameAndTypeCells(string name, string type, float modColumnWidth, Vector4 textColor)
        {
            ImGui.SetNextItemWidth(modColumnWidth);
            _ = ImGui.TableNextColumn();
            ImGui.TextColored(textColor, name);
            _ = ImGui.TableNextColumn();
            ImGui.Text(type);
        }

        private static Vector4 GetUpsideModTextColor(string type)
        {
            return type switch
            {
                AltarTypeMinion => new Vector4(0.4f, 0.8f, 0.4f, 1.0f),
                AltarTypeBoss => new Vector4(0.8f, 0.4f, 0.4f, 1.0f),
                AltarTypePlayer => new Vector4(0.4f, 0.7f, 0.9f, 1.0f),
                _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
            };
        }

        private static Vector4 GetDownsideModTextColor(string sectionHeader)
        {
            return sectionHeader switch
            {
                "Build Bricking Modifiers" => new Vector4(1.0f, 0.2f, 0.2f, 1.0f),
                "Very Dangerous Modifiers" => new Vector4(1.0f, 0.4f, 0.4f, 1.0f),
                "Dangerous Modifiers" => new Vector4(1.0f, 0.7f, 0.3f, 1.0f),
                "Ok Modifiers" => new Vector4(1.0f, 1.0f, 0.5f, 1.0f),
                "Free Modifiers" => new Vector4(0.5f, 1.0f, 0.5f, 1.0f),
                _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
            };
        }

        private static void DrawWeightScale(bool bestAtHigh = true, float width = 400f, float height = 20f)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 p = ImGui.GetCursorScreenPos();
            Vector4 colGood = new(0.2f, 1.0f, 0.2f, 1.0f);
            Vector4 colBad = new(1.0f, 0.2f, 0.2f, 1.0f);
            uint colLeft = ImGui.GetColorU32(bestAtHigh ? colBad : colGood);
            uint colRight = ImGui.GetColorU32(bestAtHigh ? colGood : colBad);
            Vector2 rectMin = p;
            Vector2 rectMax = new(p.X + width, p.Y + height);
            drawList.AddRectFilledMultiColor(rectMin, rectMax, colLeft, colRight, colRight, colLeft);
            uint borderCol = ImGui.GetColorU32(ImGuiCol.Border);
            drawList.AddRect(rectMin, rectMax, borderCol);
            const int steps = 4;
            float stepPx = width / steps;
            float tickTop = rectMax.Y;
            float tickBottom = rectMax.Y + 6f;
            float labelY = rectMax.Y + 8f;
            for (int i = 0; i <= steps; i++)
            {
                float x = rectMin.X + (i * stepPx);
                drawList.AddLine(new Vector2(x, tickTop), new Vector2(x, tickBottom), ImGui.GetColorU32(ImGuiCol.Text), 1.0f);
                string label = (i == 0 ? 1 : i * 25).ToString();
                Vector2 textSize = ImGui.CalcTextSize(label);
                Vector2 textPos = new(x - (textSize.X * 0.5f), labelY);
                drawList.AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), label);
            }

            string leftLegend = bestAtHigh ? "Worst" : "Best";
            string rightLegend = bestAtHigh ? "Best" : "Worst";
            Vector2 leftLegendSize = ImGui.CalcTextSize(leftLegend);
            Vector2 rightLegendSize = ImGui.CalcTextSize(rightLegend);
            const float margin = 2f;
            Vector2 leftPos = new(rectMin.X + margin, labelY + leftLegendSize.Y + 4f);
            Vector2 rightPos = new(rectMax.X - rightLegendSize.X - margin, labelY + rightLegendSize.Y + 4f);
            drawList.AddText(leftPos, ImGui.GetColorU32(ImGuiCol.Text), leftLegend);
            drawList.AddText(rightPos, ImGui.GetColorU32(ImGuiCol.Text), rightLegend);
            ImGui.Dummy(new Vector2(width, height + 28f + leftLegendSize.Y));
        }
    }
}
