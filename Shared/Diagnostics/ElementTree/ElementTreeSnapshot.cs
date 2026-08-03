namespace ClickIt.Shared.Diagnostics.ElementTree;

// A single value read from the captured object graph. PreviousValue is set when
// the value differs from the previous capture of the same target.
internal readonly record struct PropertySnapshot(
    string Name,
    string Value,
    string? PreviousValue,
    bool IsError);

// One node of the captured object graph: the root object, every named object
// property, collection item, element child and entity component. KeyPath
// uniquely identifies a node so values can be diffed across captures.
internal readonly record struct ElementNodeSnapshot(
    string KeyPath,
    string Name,
    string TypeName,
    long Address,
    IReadOnlyList<PropertySnapshot> Properties,
    IReadOnlyList<ElementNodeSnapshot> Nodes);

// A single capture of an inspected object graph. The first capture of a target
// is a baseline (rendered in full); later captures only render changed values.
internal readonly record struct ElementInspectionCapture(
    DateTime Timestamp,
    long EntityAddress,
    string? EntityPath,
    string? RenderName,
    string Reason,
    string? Detail,
    bool IsBaseline,
    IReadOnlyList<ElementNodeSnapshot> Nodes);
