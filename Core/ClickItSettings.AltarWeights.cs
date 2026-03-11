using ClickIt.Definitions;
using ImGuiNET;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private void DrawAltarModWeights()
        {
            DrawUpsideModsSection();
            DrawDownsideModsSection();
        }

        private void DrawUpsideModsSection()
        {
            bool isOpen = ImGui.TreeNode("Altar Upside Weights");
            DrawInlineTooltip("Set weights for upside modifiers. Higher values are more desirable and can influence recommended altar choices.");
            if (!isOpen)
            {
                return;
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale (Higher = More Valuable):");
            DrawWeightScale(bestAtHigh: true);
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar("##UpsideSearch", "Clear##UpsideClear", ref upsideSearchFilter);
            ImGui.Spacing();
            DrawUpsideModsTable();
            ImGui.TreePop();
        }

        private void DrawDownsideModsSection()
        {
            bool isOpen = ImGui.TreeNode("Altar Downside Weights");
            DrawInlineTooltip("Set weights for downside modifiers. Higher values are more dangerous and can influence recommended altar choices.");
            if (!isOpen)
            {
                return;
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale (Higher = More Dangerous):");
            DrawWeightScale(bestAtHigh: false);
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar("##DownsideSearch", "Clear##DownsideClear", ref downsideSearchFilter);
            ImGui.Spacing();
            DrawDownsideModsTable();
            ImGui.TreePop();
        }

        private void DrawUpsideModsTable()
        {
            // Upside table includes an extra "Alert" checkbox column
            // Use NoHostExtendX + NoPadOuterX so the table keeps the fixed column widths
            // and doesn't stretch to the window width when the settings window is resized.
            if (!ImGui.BeginTable("UpsideModsConfig", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
            {
                return;
            }

            SetupModTableColumns(isUpside: true);
            string currentSection = string.Empty;
            foreach ((string id, string name, string type, int _) in AltarModsConstants.UpsideMods)
            {
                if (!MatchesSearchFilter(name, type, upsideSearchFilter))
                {
                    continue;
                }

                string sectionHeader = GetUpsideSectionHeader(type);
                DrawSectionHeaderIfNeeded(ref currentSection, sectionHeader, type);
                DrawUpsideModRow(id, name, type);
            }

            ImGui.EndTable();
        }

        private void DrawDownsideModsTable()
        {
            if (!ImGui.BeginTable("DownsideModsConfig", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
            {
                return;
            }

            SetupModTableColumns(isUpside: false);
            string lastProcessedSection = string.Empty;
            foreach ((string id, string name, string type, int defaultWeight) in AltarModsConstants.DownsideMods)
            {
                if (!MatchesSearchFilter(name, type, downsideSearchFilter))
                {
                    continue;
                }

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
            return string.IsNullOrEmpty(filter) ||
                   name.ToLower().Contains(filter.ToLower()) ||
                   type.ToLower().Contains(filter.ToLower());
        }

        private static string GetUpsideSectionHeader(string type)
        {
            return type switch
            {
                AltarTypeMinion => "Minion Drops",
                AltarTypeBoss => "Boss Drops",
                AltarTypePlayer => "Player Bonuses",
                _ => string.Empty,
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
                _ => "Free Modifiers",
            };
        }

        private static void DrawSectionHeaderIfNeeded(ref string currentSection, string sectionHeader, string type)
        {
            if (string.IsNullOrEmpty(sectionHeader) || sectionHeader == currentSection)
            {
                return;
            }

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
            {
                return;
            }

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
                _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f),
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
                _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f),
            };
        }

        private void DrawUpsideModRow(string id, string name, string type)
        {
            ImGui.PushID($"upside_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            DrawModWeightSliderCell(id, type);
            DrawModNameAndTypeCells(name, type, 760, GetUpsideModTextColor(type));

            // Final column: Alert checkbox for upside mods.
            if (ModAlerts != null)
            {
                _ = ImGui.TableNextColumn();
                var avail = ImGui.GetContentRegionAvail();
                float checkboxSize = 18f;
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
                _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
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
                _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            };
        }

        internal void InitializeDefaultWeights()
        {
            // Initialize composite (type|id) defaults only. Do NOT migrate or remove legacy id-only keys.
            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.UpsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                if (ModTiers.ContainsKey(compositeKey))
                {
                    continue;
                }

                ModTiers[compositeKey] = defaultValue;
            }

            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.DownsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                if (ModTiers.ContainsKey(compositeKey))
                {
                    continue;
                }

                ModTiers[compositeKey] = defaultValue;
            }

            // Add per-upside mod alert defaults - most are off by default, but enable
            // a couple of very-high-value mods (Divine Orb drops) by default.
            foreach ((string id, _, string type, int _) in AltarModsConstants.UpsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                if (!ModAlerts.ContainsKey(compositeKey))
                {
                    if ((type == AltarTypeMinion && id == "#% chance to drop an additional Divine Orb") ||
                        (type == AltarTypeBoss && id == "Final Boss drops # additional Divine Orbs"))
                    {
                        ModAlerts[compositeKey] = true;
                    }
                    else
                    {
                        ModAlerts[compositeKey] = false;
                    }
                }
            }
        }

        private static string BuildCompositeKey(string type, string id)
        {
            return $"{type}|{id}";
        }

        public void EnsureAllModsHaveWeights()
        {
            InitializeDefaultWeights();
        }

        // Backward compatible single-argument lookup (tries id-only then returns 1).
        public int GetModTier(string modId)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return 1;
            }

            return ModTiers.TryGetValue(modId, out int value) ? value : 1;
        }

        // New getter that queries by both type and id. Does NOT fall back to id-only lookup
        // to ensure per-type weights are independent. Returns 1 if composite key not present.
        public int GetModTier(string modId, string type)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return 1;
            }

            string compositeKey = BuildCompositeKey(type, modId);
            return ModTiers.TryGetValue(compositeKey, out int value) ? value : 1;
        }

        public bool GetModAlert(string modId, string type)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return false;
            }

            string compositeKey = BuildCompositeKey(type, modId);
            if (ModAlerts.TryGetValue(compositeKey, out bool enabled))
            {
                return enabled;
            }

            // Fallback to id-only key if present.
            return ModAlerts.TryGetValue(modId, out enabled) && enabled;
        }

        public Dictionary<string, int> ModTiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // Per-upside mod alert flags (composite key = "type|id").
        public Dictionary<string, bool> ModAlerts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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

            int steps = 4;
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
            float margin = 2f;
            Vector2 leftPos = new(rectMin.X + margin, labelY + leftLegendSize.Y + 4f);
            Vector2 rightPos = new(rectMax.X - rightLegendSize.X - margin, labelY + rightLegendSize.Y + 4f);
            drawList.AddText(leftPos, ImGui.GetColorU32(ImGuiCol.Text), leftLegend);
            drawList.AddText(rightPos, ImGui.GetColorU32(ImGuiCol.Text), rightLegend);
            ImGui.Dummy(new Vector2(width, height + 28f + leftLegendSize.Y));
        }
    }
}