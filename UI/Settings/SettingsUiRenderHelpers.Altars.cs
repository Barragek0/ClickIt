namespace ClickIt.UI.Settings
{
    internal static partial class SettingsUiRenderHelpers
    {
        internal static AltarModSectionStyle GetAltarUpsideSectionStyle(string type)
            => type switch
            {
                ClickItSettings.AltarTypeMinion => new("Minion Drops", new Vector4(0.2f, 0.6f, 0.2f, 0.3f), null, new Vector4(0.4f, 0.8f, 0.4f, 1.0f)),
                ClickItSettings.AltarTypeBoss => new("Boss Drops", new Vector4(0.6f, 0.2f, 0.2f, 0.3f), null, new Vector4(0.8f, 0.4f, 0.4f, 1.0f)),
                ClickItSettings.AltarTypePlayer => new("Player Bonuses", new Vector4(0.2f, 0.2f, 0.6f, 0.3f), null, new Vector4(0.4f, 0.7f, 0.9f, 1.0f)),
                _ => new(string.Empty, new Vector4(0.4f, 0.4f, 0.4f, 0.3f), null, new Vector4(1f, 1f, 1f, 1f))
            };

        internal static AltarModSectionStyle GetAltarDownsideSectionStyle(int defaultWeight)
            => defaultWeight switch
            {
                100 => new("Build Bricking Modifiers", new Vector4(1.0f, 0.0f, 0.0f, 0.6f), new Vector4(1f, 1f, 1f, 1f), new Vector4(1.0f, 0.2f, 0.2f, 1.0f)),
                >= 70 => new("Very Dangerous Modifiers", new Vector4(0.9f, 0.1f, 0.1f, 0.5f), new Vector4(1f, 1f, 1f, 1f), new Vector4(1.0f, 0.4f, 0.4f, 1.0f)),
                >= 40 => new("Dangerous Modifiers", new Vector4(1.0f, 0.5f, 0.0f, 0.4f), new Vector4(1f, 1f, 1f, 1f), new Vector4(1.0f, 0.7f, 0.3f, 1.0f)),
                >= 2 => new("Ok Modifiers", new Vector4(1.0f, 1.0f, 0.0f, 0.3f), new Vector4(1f, 1f, 1f, 1f), new Vector4(1.0f, 1.0f, 0.5f, 1.0f)),
                _ => new("Free Modifiers", new Vector4(0.0f, 0.7f, 0.0f, 0.3f), new Vector4(1f, 1f, 1f, 1f), new Vector4(0.5f, 1.0f, 0.5f, 1.0f))
            };

        internal static AltarModSectionDescriptor GetUpsideAltarModSectionDescriptor()
            => new(
                "Altar Weight Upsides",
                "Set weights for upside modifiers. Higher values are more desirable and can influence recommended altar choices.",
                "Weight Scale (Higher = More Valuable):",
                BestAtHigh: true,
                "##UpsideSearch",
                "Clear##UpsideClear",
                "UpsideModsConfig",
                ShowAlertColumn: true);

        internal static AltarModSectionDescriptor GetDownsideAltarModSectionDescriptor()
            => new(
                "Altar Weight Downsides",
                "Set weights for downside modifiers. Higher values are more dangerous and can influence recommended altar choices.",
                "Weight Scale (Higher = More Dangerous):",
                BestAtHigh: false,
                "##DownsideSearch",
                "Clear##DownsideClear",
                "DownsideModsConfig",
                ShowAlertColumn: false);

        internal static bool DrawAltarModSection(
            ClickItSettings settings,
            AltarModSectionDescriptor descriptor,
            ref string searchFilter,
            IReadOnlyList<(string id, string name, string type, int defaultWeight)> mods,
            Func<string, int, AltarModSectionStyle> getSectionStyle)
        {
            bool isOpen = ImGui.TreeNode(descriptor.TreeLabel);
            DrawInlineTooltip(descriptor.Tooltip);
            if (!isOpen)
                return false;

            float availableWidth = SystemMath.Max(220f, ImGui.GetContentRegionAvail().X);

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped(descriptor.ScaleHeading);
            DrawWeightScale(descriptor.BestAtHigh, SystemMath.Min(400f, availableWidth));
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar(descriptor.SearchId, descriptor.ClearId, ref searchFilter);
            ImGui.Spacing();
            DrawAltarModTable(settings, descriptor, searchFilter, mods, getSectionStyle);
            ImGui.TreePop();

            return true;
        }

        internal static void DrawWeightScale(bool bestAtHigh = true, float width = 400f, float height = 20f)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            NumVector2 p = ImGui.GetCursorScreenPos();
            Vector4 colGood = new(0.2f, 1.0f, 0.2f, 1.0f);
            Vector4 colBad = new(1.0f, 0.2f, 0.2f, 1.0f);
            uint colLeft = ImGui.GetColorU32(bestAtHigh ? colBad : colGood);
            uint colRight = ImGui.GetColorU32(bestAtHigh ? colGood : colBad);
            NumVector2 rectMin = p;
            NumVector2 rectMax = new(p.X + width, p.Y + height);
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
                drawList.AddLine(new NumVector2(x, tickTop), new NumVector2(x, tickBottom), ImGui.GetColorU32(ImGuiCol.Text), 1.0f);
                string label = (i == 0 ? 1 : i * 25).ToString();
                NumVector2 textSize = ImGui.CalcTextSize(label);
                NumVector2 textPos = new(x - (textSize.X * 0.5f), labelY);
                drawList.AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), label);
            }

            string leftLegend = bestAtHigh ? "Worst" : "Best";
            string rightLegend = bestAtHigh ? "Best" : "Worst";
            NumVector2 leftLegendSize = ImGui.CalcTextSize(leftLegend);
            NumVector2 rightLegendSize = ImGui.CalcTextSize(rightLegend);
            const float margin = 2f;
            NumVector2 leftPos = new(rectMin.X + margin, labelY + leftLegendSize.Y + 4f);
            NumVector2 rightPos = new(rectMax.X - rightLegendSize.X - margin, labelY + rightLegendSize.Y + 4f);
            drawList.AddText(leftPos, ImGui.GetColorU32(ImGuiCol.Text), leftLegend);
            drawList.AddText(rightPos, ImGui.GetColorU32(ImGuiCol.Text), rightLegend);
            ImGui.Dummy(new NumVector2(width, height + 28f + leftLegendSize.Y));
        }

        internal static void DrawAltarModTable(
            ClickItSettings settings,
            AltarModSectionDescriptor descriptor,
            string searchFilter,
            IReadOnlyList<(string id, string name, string type, int defaultWeight)> mods,
            Func<string, int, AltarModSectionStyle> getSectionStyle)
        {
            int columnCount = descriptor.ShowAlertColumn ? 4 : 3;
            if (!ImGui.BeginTable(descriptor.TableId, columnCount, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;

            SetupAltarModTableColumns(descriptor.ShowAlertColumn);
            string currentSection = string.Empty;
            foreach ((string id, string name, string type, int defaultWeight) in mods)
            {
                if (!MatchesSearch(searchFilter, name, type))
                    continue;

                AltarModSectionStyle sectionStyle = getSectionStyle(type, defaultWeight);
                DrawAltarSectionHeaderIfNeeded(ref currentSection, sectionStyle);
                DrawAltarModRow(settings, id, name, type, sectionStyle.RowTextColor, descriptor.ShowAlertColumn);
            }

            ImGui.EndTable();
        }

        private static void SetupAltarModTableColumns(bool showAlertColumn)
        {
            float availableWidth = SystemMath.Max(320f, ImGui.GetContentRegionAvail().X);
            float reservedWidth = showAlertColumn ? 285f : 225f;
            float modColumnWeight = SystemMath.Max(1f, availableWidth - reservedWidth);

            if (showAlertColumn)
            {
                ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 125f);
                ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthStretch, modColumnWeight);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 90f);
                ImGui.TableSetupColumn("Alert", ImGuiTableColumnFlags.WidthFixed, 70f);
                ImGui.TableHeadersRow();
                return;
            }

            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 125f);
            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthStretch, modColumnWeight);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableHeadersRow();
        }

        private static void DrawAltarSectionHeaderIfNeeded(ref string currentSection, AltarModSectionStyle style)
        {
            if (string.IsNullOrEmpty(style.HeaderText) || style.HeaderText == currentSection)
                return;

            currentSection = style.HeaderText;
            DrawTableSectionHeaderRow(style.HeaderText, style.HeaderColor, style.HeaderTextColor);
        }

        private static void DrawAltarModRow(ClickItSettings settings, string id, string name, string type, Vector4 textColor, bool showAlertColumn)
        {
            ImGui.PushID($"mod_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            DrawAltarModWeightSliderCell(settings, id, type);
            DrawTableTextCell(name, textColor);
            DrawTableTextCell(type);

            if (showAlertColumn)
                DrawAltarModAlertCell(settings, id, type);

            ImGui.PopID();
        }

        private static void DrawAltarModAlertCell(ClickItSettings settings, string id, string type)
        {
            if (settings.ModAlerts == null)
                return;

            bool currentAlert = settings.GetModAlert(id, type);
            if (DrawCenteredCheckboxTableCell("##alert", currentAlert, out bool updatedAlert))
            {
                settings.ModAlerts[ClickItSettings.BuildCompositeKey(type, id)] = updatedAlert;
            }
        }

        private static void DrawAltarModWeightSliderCell(ClickItSettings settings, string id, string type)
        {
            int currentValue = settings.GetModTier(id, type);
            if (DrawSliderIntTableCell("##weight", currentValue, 1, 100, 125, out int updatedValue))
            {
                settings.ModTiers[ClickItSettings.BuildCompositeKey(type, id)] = updatedValue;
            }
        }
    }
}