using ExileCore.Shared.Nodes;
using ImGuiNET;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private static void DrawToggleNodeControl(string label, ToggleNode node, string tooltip)
        {
            bool value = node.Value;
            if (ImGui.Checkbox(label, ref value))
            {
                node.Value = value;
            }
            DrawInlineTooltip(tooltip);
        }

        private static void DrawRangeNodeControl(string label, RangeNode<int> node, int min, int max, string tooltip)
        {
            int value = node.Value;
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
            {
                return;
            }

            try
            {
                var buttonType = buttonNode.GetType();
                var candidateMethods = new[] { "Press", "Click", "Invoke", "Trigger" };
                foreach (var methodName in candidateMethods)
                {
                    var method = buttonType.GetMethod(methodName);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        method.Invoke(buttonNode, null);
                        return;
                    }
                }

                var onPressedProperty = buttonType.GetProperty("OnPressed");
                if (onPressedProperty?.GetValue(buttonNode) is Delegate propertyDelegate)
                {
                    propertyDelegate.DynamicInvoke();
                    return;
                }

                var onPressedField = buttonType.GetField("OnPressed");
                if (onPressedField?.GetValue(buttonNode) is Delegate fieldDelegate)
                {
                    fieldDelegate.DynamicInvoke();
                }
            }
            catch
            {
                // Best effort fallback: button invocation API may vary by ExileCore build.
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