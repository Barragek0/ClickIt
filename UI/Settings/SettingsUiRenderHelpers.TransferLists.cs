namespace ClickIt.UI.Settings
{
    internal static partial class SettingsUiRenderHelpers
    {
        internal static void DrawDualTransferTable(
            string tableId,
            string leftHeader,
            string rightHeader,
            Vector4 leftBackground,
            Vector4 rightBackground,
            Action drawLeft,
            Action drawRight,
            float leftColumnWeight = 0.5f,
            float rightColumnWeight = 0.5f)
        {
            if (!ImGui.BeginTable(tableId, 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                return;

            try
            {
                bool hideLeftColumn = leftColumnWeight <= 0f;
                bool hideRightColumn = rightColumnWeight <= 0f;

                SetupTwoColumnFilterTableHeader(leftHeader, rightHeader, leftBackground, rightBackground, leftColumnWeight, rightColumnWeight, hideLeftColumn, hideRightColumn);
                ImGui.TableNextRow();
                if (ImGui.TableSetColumnIndex(0))
                    drawLeft();


                if (ImGui.TableSetColumnIndex(1))
                    drawRight();

            }
            finally
            {
                ImGui.EndTable();
            }
        }

        internal static void DrawSingleTransferTable(
            string tableId,
            string header,
            Vector4 background,
            Action drawContent)
        {
            if (!ImGui.BeginTable(tableId, 1, ImGuiTableFlags.Borders))
                return;

            try
            {
                ImGui.TableSetupColumn(header, ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableNextRow(ImGuiTableRowFlags.None);

                if (ImGui.TableSetColumnIndex(0))
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(background));
                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), header);
                }

                ImGui.TableNextRow();
                if (ImGui.TableSetColumnIndex(0))
                    drawContent();

            }
            finally
            {
                ImGui.EndTable();
            }
        }

        internal static bool DrawTransferListRow(string listId, string key, string displayText, bool moveToPrimaryList, Vector4 textColor)
        {
            float rowWidth = CalculateTransferRowWidth();
            const float arrowWidth = 28f;

            if (moveToPrimaryList)
            {
                bool leftArrowClicked = ImGui.Button($"<-##Move_{listId}_{key}", new NumVector2(arrowWidth, 0));
                ImGui.SameLine();
                DrawTransferListSelectable(listId, key, displayText, rowWidth, textColor);
                return leftArrowClicked;
            }

            DrawTransferListSelectable(listId, key, displayText, rowWidth, textColor);
            ImGui.SameLine();
            return ImGui.Button($"->##Move_{listId}_{key}", new NumVector2(arrowWidth, 0));
        }

        internal static ExpandableTransferRowState DrawExpandableTransferListRow(
            string moveButtonId,
            string label,
            bool isExpanded,
            bool moveToPrimaryList,
            Vector4 textColor)
        {
            float rowWidth = CalculateTransferRowWidth();
            const float arrowWidth = 28f;

            if (moveToPrimaryList)
            {
                bool leftArrowClicked = ImGui.Button($"<-##{moveButtonId}", new NumVector2(arrowWidth, 0));
                bool leftArrowHovered = ImGui.IsItemHovered();
                ImGui.SameLine();
                bool rowClicked = DrawExpandableTransferSelectable(label, isExpanded, rowWidth, textColor);
                bool rowHovered = ImGui.IsItemHovered();
                return new ExpandableTransferRowState(rowClicked, leftArrowClicked, rowHovered, leftArrowHovered);
            }

            bool clicked = DrawExpandableTransferSelectable(label, isExpanded, rowWidth, textColor);
            bool hovered = ImGui.IsItemHovered();
            ImGui.SameLine();
            bool rightArrowClicked = ImGui.Button($"->##{moveButtonId}", new NumVector2(arrowWidth, 0));
            bool rightArrowHovered = ImGui.IsItemHovered();
            return new ExpandableTransferRowState(clicked, rightArrowClicked, hovered, rightArrowHovered);
        }

        private static void DrawTransferListSelectable(string listId, string key, string displayText, float rowWidth, Vector4 textColor)
        {
            _ = DrawSelectableText($"{displayText}##{listId}_{key}", false, ImGuiSelectableFlags.None, textColor, new NumVector2(rowWidth, 0));
        }

        private static bool DrawExpandableTransferSelectable(string label, bool isExpanded, float rowWidth, Vector4 textColor)
            => DrawSelectableText(label, isExpanded, ImGuiSelectableFlags.AllowDoubleClick, textColor, new NumVector2(rowWidth, 0));

        private static void SetupTwoColumnFilterTableHeader(
            string leftHeader,
            string rightHeader,
            Vector4 leftBackground,
            Vector4 rightBackground,
            float leftColumnWeight,
            float rightColumnWeight,
            bool hideLeftColumn,
            bool hideRightColumn)
        {
            float normalizedLeftWeight = SystemMath.Max(0.15f, leftColumnWeight);
            float normalizedRightWeight = SystemMath.Max(0.15f, rightColumnWeight);

            ImGui.TableSetupColumn(leftHeader, ImGuiTableColumnFlags.WidthStretch, normalizedLeftWeight);
            ImGui.TableSetupColumn(rightHeader, ImGuiTableColumnFlags.WidthStretch, normalizedRightWeight);
            ImGui.TableSetColumnEnabled(0, !hideLeftColumn);
            ImGui.TableSetColumnEnabled(1, !hideRightColumn);

            ImGui.TableNextRow(ImGuiTableRowFlags.None);

            if (ImGui.TableSetColumnIndex(0))
            {
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(leftBackground));
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), leftHeader);
            }

            if (ImGui.TableSetColumnIndex(1))
            {
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(rightBackground));
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), rightHeader);
            }
        }
    }
}