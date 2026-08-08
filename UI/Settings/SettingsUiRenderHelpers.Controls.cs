namespace ClickIt.UI.Settings
{
    internal static partial class SettingsUiRenderHelpers
    {
        internal static bool TryInvokeHotkeyPicker(object? hotkeyNode, string label)
        {
            if (hotkeyNode == null)
                return false;

            try
            {
                hotkeyNode.GetType().GetMethod("DrawPickerButton", BindingFlags.Instance | BindingFlags.Public)?.Invoke(hotkeyNode, [label]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void DrawHotkeyNodeControl(object? hotkeyNode, string label, string tooltip)
        {
            _ = TryInvokeHotkeyPicker(hotkeyNode, label);
            if (!string.IsNullOrWhiteSpace(tooltip))
                DrawInlineTooltip(tooltip);
        }

        internal static void DrawToggleNodeControl(string label, ToggleNode node, string tooltip)
        {
            bool value = node.Value;
            if (ImGui.Checkbox(label, ref value))
                node.Value = value;


            DrawInlineTooltip(tooltip);
        }

        internal static void DrawButtonNodeControl(string label, ButtonNode? node, string tooltip)
        {
            if (ImGui.Button(label))
                TriggerButtonNode(node);


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
                ImGui.SetNextItemWidth(widthOverride ?? 400f);


            if (ImGui.SliderInt(label, ref value, min, max))
                node.Value = value;


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
                ImGui.SetTooltip(tooltip);

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

        // Resolves the colour of the word run starting at `index` in `words`. Returns null for the
        // base colour; `consumed` is the number of words the colour spans (1 unless a multi-word
        // phrase like "Chilling Tower" matched) and must be set even when returning null.
        internal delegate Vector4? ColoredTextResolver(string[] words, int index, out int consumed);

        // Multi-colour text laid out by explicit '\n' only — no width-based wrapping, so the text
        // is never re-wrapped or clipped differently from the authored line breaks. Words are drawn
        // with the raw DrawList at explicitly computed positions, so ImGui's
        // cursor/SameLine/TextWrapPos bookkeeping can never re-wrap or clip a word differently from
        // the measurement.
        internal static void DrawColoredText(string text, Vector4 baseColor, ColoredTextResolver? colorForWords, float indent = 0f)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (indent > 0f)
                ImGui.Indent(indent);

            float lineHeight = ImGui.GetTextLineHeight();
            float spaceWidth = ImGui.CalcTextSize(" ").X;

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            ImFontPtr font = ImGui.GetFont();
            float fontSize = ImGui.GetFontSize();
            float startY = ImGui.GetCursorPosY();

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                    continue; // blank line — the next line's explicit Y already skips its height
                float x = ImGui.GetCursorScreenPos().X;
                float y = ImGui.GetCursorScreenPos().Y + (i * lineHeight);
                string[] words = lines[i].Split(' ');
                int index = 0;
                while (index < words.Length)
                {
                    if (words[index].Length == 0)
                    {
                        index++;
                        continue;
                    }

                    int consumed = 1;
                    Vector4 color = colorForWords != null
                        ? colorForWords(words, index, out consumed) ?? baseColor
                        : baseColor;
                    consumed = SystemMath.Clamp(consumed, 1, words.Length - index);
                    for (int k = 0; k < consumed; k++)
                    {
                        string word = words[index + k];
                        if (word.Length == 0)
                            continue;
                        x = DrawWord(drawList, font, fontSize, x, y, color, word);
                        x += spaceWidth;
                    }
                    index += consumed;
                }
            }

            ImGui.SetCursorPosY(startY + (lines.Length * lineHeight));

            if (indent > 0f)
                ImGui.Unindent(indent);
        }

        private static float DrawWord(ImDrawListPtr drawList, ImFontPtr font, float fontSize, float x, float y, Vector4 color, string word)
        {
            drawList.AddText(font, fontSize, new NumVector2(x, y), ImGui.GetColorU32(color), word);
            return x + ImGui.CalcTextSize(word).X;
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