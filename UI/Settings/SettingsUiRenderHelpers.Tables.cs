namespace ClickIt.UI.Settings
{
    internal static partial class SettingsUiRenderHelpers
    {
        internal static bool BeginSingleColumnPriorityTable(string tableId, string headerText, float tableWidth)
        {
            if (!ImGui.BeginTable(tableId, 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return false;

            ImGui.TableSetupColumn(headerText, ImGuiTableColumnFlags.WidthFixed, tableWidth);
            DrawPriorityTableHeaderCell(headerText);
            return true;
        }

        internal static bool DrawCenteredCheckboxTableCell(string label, bool currentValue, out bool updatedValue)
        {
            updatedValue = currentValue;
            _ = ImGui.TableNextColumn();
            NumVector2 available = ImGui.GetContentRegionAvail();
            const float checkboxSize = 18f;
            float currentX = ImGui.GetCursorPosX();
            float offset = (available.X - checkboxSize) * 0.5f;
            if (offset > 0)
                ImGui.SetCursorPosX(currentX + offset);

            return ImGui.Checkbox(label, ref updatedValue);
        }

        internal static bool DrawSliderIntTableCell(string label, int currentValue, int min, int max, float width, out int updatedValue)
        {
            updatedValue = currentValue;
            _ = ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(width);
            return ImGui.SliderInt(label, ref updatedValue, min, max);
        }

        internal static void DrawTableTextCell(string text, Vector4? textColor = null)
        {
            _ = ImGui.TableNextColumn();
            if (textColor.HasValue)
            {
                ImGui.TextColored(textColor.Value, text);
                return;
            }

            ImGui.Text(text);
        }

        internal static void SetupFixedWidthTableColumns(
            (string HeaderText, float Width) first,
            (string HeaderText, float Width) second,
            (string HeaderText, float Width) third,
            (string HeaderText, float Width)? fourth = null)
        {
            ImGui.TableSetupColumn(first.HeaderText, ImGuiTableColumnFlags.WidthFixed, first.Width);
            ImGui.TableSetupColumn(second.HeaderText, ImGuiTableColumnFlags.WidthFixed, second.Width);
            ImGui.TableSetupColumn(third.HeaderText, ImGuiTableColumnFlags.WidthFixed, third.Width);
            if (fourth.HasValue)
            {
                ImGui.TableSetupColumn(fourth.Value.HeaderText, ImGuiTableColumnFlags.WidthFixed, fourth.Value.Width);
            }

            ImGui.TableHeadersRow();
        }

        internal static void DrawTableSectionHeaderRow(string headerText, Vector4 headerColor, Vector4? textColor = null)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(headerColor));
            ImGui.Text(string.Empty);
            ImGui.TableNextColumn();

            if (textColor.HasValue)
            {
                ImGui.TextColored(textColor.Value, headerText);
                return;
            }

            ImGui.Text(headerText);
        }

        private static void DrawPriorityTableHeaderCell(string headerText)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.6f, 0.3f)));
            ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), headerText);
        }
    }
}