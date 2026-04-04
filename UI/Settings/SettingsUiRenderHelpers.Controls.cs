namespace ClickIt.UI.Settings
{
    internal static partial class SettingsUiRenderHelpers
    {
        internal static void DrawToggleNodeControl(string label, ToggleNode node, string tooltip)
        {
            bool value = node.Value;
            if (ImGui.Checkbox(label, ref value))
            {
                node.Value = value;
            }

            DrawInlineTooltip(tooltip);
        }

        internal static void DrawButtonNodeControl(string label, ButtonNode? node, string tooltip)
        {
            if (ImGui.Button(label))
            {
                TriggerButtonNode(node);
            }

            DrawInlineTooltip(tooltip);
        }

        internal static void PushStandardSliderWidth()
        {
            ImGui.PushItemWidth(400f);
        }

        internal static void PopStandardSliderWidth()
        {
            ImGui.PopItemWidth();
        }

        internal static void DrawRangeNodeControl(string label, RangeNode<int> node, int min, int max, string tooltip, bool useStandardWidth = true)
        {
            DrawRangeNodeControl(label, node, min, max, tooltip, useStandardWidth, null);
        }

        internal static void DrawRangeNodeControl(string label, RangeNode<int> node, int min, int max, string tooltip, bool useStandardWidth, float? widthOverride)
        {
            int value = node.Value;
            if (useStandardWidth)
            {
                ImGui.SetNextItemWidth(widthOverride ?? 400f);
            }

            if (ImGui.SliderInt(label, ref value, min, max))
            {
                node.Value = value;
            }

            DrawInlineTooltip(tooltip);
        }

        internal static void DrawToggleAndRangeNodeControls(
            string toggleLabel,
            ToggleNode toggleNode,
            string toggleTooltip,
            string rangeLabel,
            RangeNode<int> rangeNode,
            int min,
            int max,
            string rangeTooltip,
            bool useStandardWidth = true,
            float? rangeWidthOverride = null)
        {
            DrawToggleNodeControl(toggleLabel, toggleNode, toggleTooltip);
            DrawRangeNodeControl(rangeLabel, rangeNode, min, max, rangeTooltip, useStandardWidth, rangeWidthOverride);
        }

        internal static void DrawInlineTooltip(string tooltip)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
        }

        internal static void TriggerButtonNode(ButtonNode? buttonNode)
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

        internal static bool DrawSelectableText(string label, bool selected, ImGuiSelectableFlags flags, Vector4 textColor, NumVector2 size)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            bool clicked = ImGui.Selectable(label, selected, flags, size);
            ImGui.PopStyleColor();
            return clicked;
        }

        internal static void DrawWrappedText(string text, Vector4 textColor, float indent = 0f)
        {
            if (indent > 0f)
                ImGui.Indent(indent);

            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextWrapped(text);
            ImGui.PopStyleColor();

            if (indent > 0f)
                ImGui.Unindent(indent);
        }

        internal static void DrawInstructionText(string text)
        {
            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), text);
        }

        internal static bool DrawCheckbox(string label, ref bool value, Vector4 textColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            bool changed = ImGui.Checkbox(label, ref value);
            ImGui.PopStyleColor();
            return changed;
        }
    }
}