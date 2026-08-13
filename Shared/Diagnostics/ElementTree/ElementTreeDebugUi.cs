using NumVec4 = System.Numerics.Vector4;

namespace ClickIt.Shared.Diagnostics.ElementTree;

// Self-contained debug UI for an ElementTreeInspector: the first capture of a target renders the full tree, later captures only render changed values (old value in red, new value in green). Also appends the same view to the copy-all dump.
internal static class ElementTreeDebugUi
{
    private static readonly NumVec4 CHeader = Vec4(Color.Orange);
    private static readonly NumVec4 CInfo = Vec4(Color.Cyan);
    private static readonly NumVec4 CMuted = Vec4(Color.LightGray);
    private static readonly NumVec4 CDim = Vec4(Color.DarkGray);
    private static readonly NumVec4 CWhite = Vec4(Color.White);
    private static readonly NumVec4 CError = Vec4(Color.Red);
    private static readonly NumVec4 CGreen = Vec4(Color.LightGreen);

    internal static void DrawSection(ElementTreeInspector inspector, string title, ref int selectedIndex)
    {
        IReadOnlyList<ElementInspectionCapture> history = inspector.GetHistory();
        ImGui.Spacing();
        if (history.Count == 0)
        {
            ImGui.TextColored(CMuted, "No element captures yet.");
            return;
        }

        if (!ImGui.CollapsingHeader($"{title} ({history.Count} captures)"))
            return;

        if (selectedIndex >= history.Count)
            selectedIndex = history.Count - 1;

        for (int i = 0; i < history.Count; i++)
        {
            ElementInspectionCapture h = history[i];
            if (ImGui.Selectable($"{h.Timestamp:HH:mm:ss.fff} {h.Reason}##elementtree{i}", i == selectedIndex))
                selectedIndex = i;
        }

        ImGui.Separator();
        ElementInspectionCapture selected = history[selectedIndex];
        ImGui.TextColored(CInfo, $"{selected.Timestamp:HH:mm:ss.fff} - {selected.Reason}");
        if (selected.Detail != null)
        {
            ImGui.SameLine();
            ImGui.TextColored(CDim, selected.Detail);
        }
        ImGui.TextColored(CDim, $"addr=0x{selected.EntityAddress:X}");
        if (selected.EntityPath != null)
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(CDim, $"path={selected.EntityPath}");
            ImGui.PopTextWrapPos();
        }
        if (selected.RenderName != null)
            ImGui.TextColored(CDim, $"name={selected.RenderName}");

        ImGui.Spacing();
        ImGui.TextColored(CHeader, "Element Tree:");
        RenderTree(selected);
    }

    internal static void AppendToDump(StringBuilder sb, ElementTreeInspector inspector)
    {
        ElementInspectionCapture? capture = inspector.Latest;
        if (capture == null)
            return;

        ElementInspectionCapture latest = capture.Value;
        sb.AppendLine($"  {latest.Timestamp:HH:mm:ss.fff} {latest.Reason} addr=0x{latest.EntityAddress:X} path={latest.EntityPath ?? "?"} name={latest.RenderName ?? "?"} detail={latest.Detail ?? string.Empty}");
        foreach (ElementNodeSnapshot node in latest.Nodes)
            AppendNodeToDump(sb, node, 1, latest.IsBaseline);
    }

    private static void RenderTree(ElementInspectionCapture capture)
    {
        if (capture.Nodes.Count == 0)
            return;
        if (!capture.IsBaseline && !HasChanges(capture.Nodes[0]))
        {
            ImGui.TextColored(CDim, "  no changes");
            return;
        }
        foreach (ElementNodeSnapshot node in capture.Nodes)
            RenderNode(node, capture.IsBaseline);
    }

    private static void RenderNode(ElementNodeSnapshot node, bool baseline)
    {
        if (!baseline && !HasChanges(node))
            return;

        if (ImGui.TreeNodeEx($"{NodeLabel(node)}##elementtreenode{node.KeyPath}", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool renderedAny = false;
            foreach (PropertySnapshot property in node.Properties)
            {
                if (baseline || property.PreviousValue != null)
                {
                    RenderProperty(property);
                    renderedAny = true;
                }
            }
            if (!baseline && !renderedAny && node.Nodes.Count == 0)
                ImGui.TextColored(CDim, "  no changes");

            foreach (ElementNodeSnapshot child in node.Nodes)
                RenderNode(child, baseline);
            ImGui.TreePop();
        }
    }

    private static void RenderProperty(PropertySnapshot property)
    {
        if (property.PreviousValue != null)
        {
            ImGui.TextColored(CWhite, $"  {property.Name}: ");
            ImGui.SameLine(0, 0);
            ImGui.PushStyleColor(ImGuiCol.Text, CError);
            ImGui.Text(property.PreviousValue);
            ImGui.PopStyleColor();
            ImGui.SameLine(0, 4);
            ImGui.Text("->");
            ImGui.SameLine(0, 4);
            ImGui.PushStyleColor(ImGuiCol.Text, CGreen);
            ImGui.Text(property.Value);
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.TextColored(property.IsError ? CError : CWhite, $"  {property.Name}: {property.Value}");
        }
    }

    private static bool HasChanges(ElementNodeSnapshot node)
    {
        foreach (PropertySnapshot property in node.Properties)
        {
            if (property.PreviousValue != null)
                return true;
        }
        foreach (ElementNodeSnapshot child in node.Nodes)
        {
            if (HasChanges(child))
                return true;
        }
        return false;
    }

    private static string NodeLabel(ElementNodeSnapshot node)
    {
        if (node.KeyPath == "root")
            return $"[root] {node.TypeName}";
        if (node.TypeName.Length == 0)
            return $"{node.Name} ({Count(node)})";
        string suffix = node.Address != 0 ? $" [{node.Address:X}]" : string.Empty;
        return node.Name == node.TypeName
            ? $"{node.Name}{suffix}"
            : $"{node.Name}: {node.TypeName}{suffix}";
    }

    private static string Count(ElementNodeSnapshot node)
    {
        foreach (PropertySnapshot property in node.Properties)
        {
            if (property.Name == "Count")
                return property.Value;
        }
        return string.Empty;
    }

    private static void AppendNodeToDump(StringBuilder sb, ElementNodeSnapshot node, int indent, bool baseline)
    {
        if (!baseline && !HasChanges(node))
            return;

        string padding = new(' ', indent * 2);
        sb.AppendLine($"{padding}{NodeLabel(node)}");
        foreach (PropertySnapshot property in node.Properties)
        {
            if (baseline || property.PreviousValue != null)
                sb.AppendLine($"{padding}  {FormatProperty(property)}");
        }
        foreach (ElementNodeSnapshot child in node.Nodes)
            AppendNodeToDump(sb, child, indent + 1, baseline);
    }

    private static string FormatProperty(PropertySnapshot property)
        => property.PreviousValue != null
            ? $"{property.Name}: {property.PreviousValue} -> {property.Value}"
            : $"{property.Name}: {property.Value}";

    private static NumVec4 Vec4(Color c)
        => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
}
