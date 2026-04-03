using System.Text;

namespace ClickIt.UI.Debug.Introspection
{
    internal static class RuntimeObjectIntrospectionEventFormatter
    {
        public static void AppendTraversalEvents(StringBuilder sb, IReadOnlyList<RuntimeObjectTraversalEvent> events, int maxValueChars)
        {
            for (int i = 0; i < events.Count; i++)
                sb.AppendLine(FormatTraversalEvent(events[i], maxValueChars));
        }

        public static string FormatTraversalEvent(RuntimeObjectTraversalEvent evt, int maxValueChars)
        {
            return evt.Kind switch
            {
                RuntimeObjectTraversalEventKind.NodeNull => $"{evt.Path}: null",
                RuntimeObjectTraversalEventKind.NodeSimple => $"{evt.Path}: {evt.RuntimeType?.Name} = {RuntimeObjectIntrospection.FormatValue(evt.Value, maxValueChars)}",
                RuntimeObjectTraversalEventKind.NodeType => $"{evt.Path}: {evt.RuntimeType?.FullName}",
                RuntimeObjectTraversalEventKind.NodeCycle => $"{evt.Path}: {evt.RuntimeType?.FullName} (cycle)",
                RuntimeObjectTraversalEventKind.NodeMaxDepth => $"{evt.Path}: max depth reached",
                RuntimeObjectTraversalEventKind.NodeNoReadableMembers => $"{evt.Path}: no readable public members",
                RuntimeObjectTraversalEventKind.EnumerableType => $"{evt.Path}: {evt.RuntimeType?.FullName} (enumerable)",
                RuntimeObjectTraversalEventKind.EnumerablePreviewCount => $"{evt.Path}: previewCount={evt.Count}",
                RuntimeObjectTraversalEventKind.EnumerableTruncated => $"{evt.Path}: collection output truncated (more entries omitted)",
                RuntimeObjectTraversalEventKind.MemberUnavailable => $"{evt.Path}: <unavailable>",
                RuntimeObjectTraversalEventKind.MemberNull => $"{evt.Path}: null",
                RuntimeObjectTraversalEventKind.MemberSimple => $"{evt.Path}: {RuntimeObjectIntrospection.FormatValue(evt.Value, maxValueChars)}",
                RuntimeObjectTraversalEventKind.MemberOutputTruncated => $"{evt.Path}: member output truncated ({evt.Count} omitted)",
                RuntimeObjectTraversalEventKind.NodeBudgetReachedWhileSchedulingMembers => $"{evt.Path}: node budget reached while scheduling members",
                RuntimeObjectTraversalEventKind.TraversalStoppedTimeBudget => $"Traversal stopped: elapsed-time budget reached ({evt.Count}ms).",
                RuntimeObjectTraversalEventKind.TraversalStoppedNodeBudget => $"Traversal stopped: node budget reached ({evt.Count}).",
                RuntimeObjectTraversalEventKind.TraversalCompleted => $"Traversal completed: processed {evt.Count} nodes.",
                RuntimeObjectTraversalEventKind.NodeProcessingError => $"{evt.Path}: <node processing error> {evt.Message}",
                _ => $"{evt.Path}: <unknown traversal event>"
            };
        }
    }
}