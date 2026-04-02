namespace ClickIt.Utils
{
    internal readonly record struct RuntimeObjectTraversalEvent(
        RuntimeObjectTraversalEventKind Kind,
        string Path,
        Type? RuntimeType = null,
        object? Value = null,
        int Count = 0,
        string? Message = null);

    internal enum RuntimeObjectTraversalEventKind
    {
        NodeNull,
        NodeSimple,
        NodeType,
        NodeCycle,
        NodeMaxDepth,
        NodeNoReadableMembers,
        EnumerableType,
        EnumerablePreviewCount,
        EnumerableTruncated,
        MemberUnavailable,
        MemberNull,
        MemberSimple,
        MemberOutputTruncated,
        NodeBudgetReachedWhileSchedulingMembers,
        TraversalStoppedTimeBudget,
        TraversalStoppedNodeBudget,
        TraversalCompleted,
        NodeProcessingError
    }
}