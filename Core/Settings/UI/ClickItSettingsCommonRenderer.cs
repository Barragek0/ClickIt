using ExileCore.Shared.Nodes;
using ImGuiNET;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private static void DrawDualTransferTable(
            string tableId,
            string leftHeader,
            string rightHeader,
            Vector4 leftBackground,
            Vector4 rightBackground,
            Action drawLeft,
            Action drawRight)
        {
            if (!ImGui.BeginTable(tableId, 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                return;

            try
            {
                SetupTwoColumnFilterTableHeader(
                    leftHeader,
                    rightHeader,
                    leftBackground,
                    rightBackground);

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                drawLeft();

                ImGui.TableSetColumnIndex(1);
                drawRight();
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        private static bool DrawTransferListRow(string listId, string key, string displayText, bool moveToPrimaryList, Vector4 textColor)
        {
            float rowWidth = CalculateItemTypeRowWidth();
            const float arrowWidth = 28f;

            if (moveToPrimaryList)
            {
                bool leftArrowClicked = ImGui.Button($"<-##Move_{listId}_{key}", new Vector2(arrowWidth, 0));
                ImGui.SameLine();
                DrawTransferListSelectable(listId, key, displayText, rowWidth, textColor);
                return leftArrowClicked;
            }

            DrawTransferListSelectable(listId, key, displayText, rowWidth, textColor);
            ImGui.SameLine();
            bool rightArrowClicked = ImGui.Button($"->##Move_{listId}_{key}", new Vector2(arrowWidth, 0));
            return rightArrowClicked;
        }

        private static void DrawTransferListSelectable(string listId, string key, string displayText, float rowWidth, Vector4 textColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.Selectable($"{displayText}##{listId}_{key}", false, ImGuiSelectableFlags.None, new Vector2(rowWidth, 0));
            ImGui.PopStyleColor();
        }

        private static bool BeginSingleColumnPriorityTable(string tableId, string headerText, float tableWidth)
        {
            if (!ImGui.BeginTable(tableId, 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return false;

            ImGui.TableSetupColumn(headerText, ImGuiTableColumnFlags.WidthFixed, tableWidth);
            DrawPriorityTableHeaderCell(headerText);
            return true;
        }

        private static void DrawPriorityTableHeaderCell(string headerText)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.6f, 0.3f)));
            ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), headerText);
        }

        private static void DrawToggleNodeControl(string label, ToggleNode node, string tooltip)
        {
            bool value = node.Value;
            if (ImGui.Checkbox(label, ref value))
            {
                node.Value = value;
            }

            DrawInlineTooltip(tooltip);
        }

        private static void PushStandardSliderWidth()
        {
            ImGui.PushItemWidth(400f);
        }

        private static void PopStandardSliderWidth()
        {
            ImGui.PopItemWidth();
        }

        private static void DrawRangeNodeControl(string label, RangeNode<int> node, int min, int max, string tooltip, bool useStandardWidth = true)
        {
            int value = node.Value;
            if (useStandardWidth)
            {
                ImGui.SetNextItemWidth(400f);
            }

            if (ImGui.SliderInt(label, ref value, min, max))
            {
                node.Value = value;
            }

            DrawInlineTooltip(tooltip);
        }

        private static void DrawInlineTooltip(string tooltip)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
        }

        private static void TriggerButtonNode(ButtonNode buttonNode)
        {
            if (buttonNode == null)
                return;

            try
            {
                buttonNode.OnPressed?.Invoke();
            }
            catch
            {
                // Best effort invocation.
            }
        }

        private static void DrawSearchBar(string searchId, string clearId, ref string searchFilter)
        {
            ImGui.SetNextItemWidth(300);
            ImGui.InputTextWithHint(searchId, "Search", ref searchFilter, 256);
            ImGui.SameLine();
            if (ImGui.Button(clearId))
            {
                searchFilter = string.Empty;
            }
        }

        private static bool DrawResetDefaultsButton(string buttonId)
        {
            ImGui.SameLine();
            return ImGui.Button(buttonId);
        }

        private static void DrawNoEntriesPlaceholder(bool hasEntries)
        {
            if (!hasEntries)
            {
                ImGui.TextDisabled("No entries");
            }
        }

        private static void SetupTwoColumnFilterTableHeader(string leftHeader, string rightHeader, Vector4 leftBackground, Vector4 rightBackground)
        {
            ImGui.TableSetupColumn(leftHeader, ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn(rightHeader, ImGuiTableColumnFlags.WidthStretch, 0.5f);

            ImGui.TableNextRow(ImGuiTableRowFlags.None);

            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(leftBackground));
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), leftHeader);

            ImGui.TableSetColumnIndex(1);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(rightBackground));
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), rightHeader);
        }
    }
}