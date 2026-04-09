namespace ClickIt.UI.Settings
{
    internal static partial class SettingsUiRenderHelpers
    {
        internal readonly record struct ExpandableTransferRowState(bool RowClicked, bool ArrowClicked, bool RowHovered, bool ArrowHovered);
        internal readonly record struct AltarModSectionStyle(string HeaderText, Vector4 HeaderColor, Vector4? HeaderTextColor, Vector4 RowTextColor);
        internal readonly record struct AltarModSectionDescriptor(
            string TreeLabel,
            string Tooltip,
            string ScaleHeading,
            bool BestAtHigh,
            string SearchId,
            string ClearId,
            string TableId,
            bool ShowAlertColumn);

        internal static float CalculateTransferRowWidth()
        {
            float availableWidth = SystemMath.Max(80f, ImGui.GetContentRegionAvail().X);
            const float arrowWidth = 28f;
            return SystemMath.Max(40f, availableWidth - arrowWidth - 6f);
        }

        internal static Vector4 GetUltimatumPriorityRowColor(int index, int totalCount)
            => UltimatumModifiersConstants.GetPriorityGradientColor(index, totalCount, 0.30f);

        internal static bool DrawArrowButton(ImGuiDir direction, string id, bool enabled)
        {
            if (!enabled)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.5f);
            }

            bool clicked = ImGui.ArrowButton(id, direction);

            if (!enabled)
            {
                ImGui.PopStyleVar();
                return false;
            }

            return clicked;
        }

        internal static void DrawSearchBar(string searchId, string clearId, ref string searchFilter)
        {
            ImGui.SetNextItemWidth(300);
            ImGui.InputTextWithHint(searchId, "Search", ref searchFilter, 256);
            ImGui.SameLine();
            if (ImGui.Button(clearId))
            {
                searchFilter = string.Empty;
            }
        }

        internal static bool DrawResetDefaultsButton(string buttonId)
        {
            ImGui.SameLine();
            return ImGui.Button(buttonId);
        }

        internal static bool MatchesSearch(string filter, params string?[] values)
            => MatchesSearch(filter, (IEnumerable<string?>)values);

        internal static bool MatchesSearch(string filter, IEnumerable<string?> values)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            string term = filter.Trim();
            foreach (string? value in values)
            {
                if (!string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static string BuildExpandedRowKey(string listId, string rowId)
            => $"{listId}:{rowId}";

        internal static string ToggleExpandedRowKey(string currentKey, string listId, string rowId)
        {
            string rowKey = BuildExpandedRowKey(listId, rowId);
            return string.Equals(currentKey, rowKey, StringComparison.Ordinal)
                ? string.Empty
                : rowKey;
        }

        internal static void DrawNoEntriesPlaceholder(bool hasEntries)
        {
            if (!hasEntries)
            {
                ImGui.TextDisabled("No entries");
            }
        }

    }
}