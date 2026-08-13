namespace ClickIt.Shared.Diagnostics.ElementTree;

// Captures an object graph (element tree + entity components) and diffs it against the previous capture: the first capture of a target is a baseline, later captures bake PreviousValue into every changed property.
internal sealed class ElementTreeInspector
{
    private const int MaxHistory = 24;

    private readonly Lock _gate = new();
    private readonly List<ElementInspectionCapture> _history = [];

    internal void Capture(object? rootElement, Entity? entity, string reason, string? detail = null)
    {
        IReadOnlyList<ElementNodeSnapshot> nodes = ElementTreeDumper.DumpTree(rootElement);
        (long address, string? path, string? renderName) = ElementTreeDumper.ReadEntityFacts(entity);

        ElementInspectionCapture? previous;
        lock (_gate)
            previous = _history.Count > 0 ? _history[^1] : null;

        bool baseline = true;
        if (previous is { } prev)
        {
            baseline = prev.EntityAddress != address;
            if (!baseline)
                nodes = ApplyDiff(prev.Nodes, nodes);
        }

        ElementInspectionCapture capture = new(
            Timestamp: DateTime.Now,
            EntityAddress: address,
            EntityPath: path,
            RenderName: renderName,
            Reason: reason,
            Detail: detail,
            IsBaseline: baseline,
            Nodes: nodes);

        lock (_gate)
        {
            _history.Add(capture);
            if (_history.Count > MaxHistory)
                _history.RemoveAt(0);
        }
    }

    internal ElementInspectionCapture? Latest
    {
        get
        {
            lock (_gate)
                return _history.Count > 0 ? _history[^1] : null;
        }
    }

    internal IReadOnlyList<ElementInspectionCapture> GetHistory()
    {
        lock (_gate)
            return [.. _history];
    }

    internal void Clear()
    {
        lock (_gate)
            _history.Clear();
    }

    private static List<ElementNodeSnapshot> ApplyDiff(
        IReadOnlyList<ElementNodeSnapshot> previousNodes,
        IReadOnlyList<ElementNodeSnapshot> currentNodes)
    {
        Dictionary<string, string> previousFlat = Flatten(previousNodes);
        List<ElementNodeSnapshot> result = new(currentNodes.Count);
        foreach (ElementNodeSnapshot node in currentNodes)
            result.Add(ApplyDiffNode(previousFlat, node));
        return result;
    }

    private static ElementNodeSnapshot ApplyDiffNode(Dictionary<string, string> previousFlat, ElementNodeSnapshot node)
    {
        List<PropertySnapshot> properties = new(node.Properties.Count);
        foreach (PropertySnapshot property in node.Properties)
        {
            string key = ElementTreeDumper.DiffKey(node.KeyPath, property.Name);
            string? previousValue = previousFlat.TryGetValue(key, out string? old)
                && !string.Equals(old, property.Value, StringComparison.Ordinal)
                    ? old
                    : null;
            properties.Add(property with { PreviousValue = previousValue });
        }

        List<ElementNodeSnapshot> nodes = new(node.Nodes.Count);
        foreach (ElementNodeSnapshot child in node.Nodes)
            nodes.Add(ApplyDiffNode(previousFlat, child));
        return node with { Properties = properties, Nodes = nodes };
    }

    private static Dictionary<string, string> Flatten(IReadOnlyList<ElementNodeSnapshot> nodes)
    {
        Dictionary<string, string> flat = new(StringComparer.Ordinal);
        foreach (ElementNodeSnapshot node in nodes)
            FlattenNode(node, flat);
        return flat;
    }

    private static void FlattenNode(ElementNodeSnapshot node, Dictionary<string, string> flat)
    {
        foreach (PropertySnapshot property in node.Properties)
            flat[ElementTreeDumper.DiffKey(node.KeyPath, property.Name)] = property.Value;
        foreach (ElementNodeSnapshot child in node.Nodes)
            FlattenNode(child, flat);
    }
}
