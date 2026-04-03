namespace ClickIt.UI.Debug.Introspection
{
    internal sealed class RuntimeObjectTraversalEngine
    {
        private readonly Stack<RuntimeObjectTraversalPendingNode> _stack = new();
        private readonly HashSet<object> _visited = new(ReferenceEqualityComparer.Instance);
        private readonly Stopwatch _elapsedStopwatch = Stopwatch.StartNew();

        public RuntimeObjectTraversalOptions Options { get; }
        public bool EnforceElapsedBudget { get; }
        public int TotalProcessedNodes { get; private set; }
        public bool IsFinished { get; private set; }

        public RuntimeObjectTraversalEngine(object root, RuntimeObjectTraversalOptions options, bool enforceElapsedBudget)
        {
            Options = options;
            EnforceElapsedBudget = enforceElapsedBudget;
            _stack.Push(new RuntimeObjectTraversalPendingNode("Root", root, 0));
        }

        public IReadOnlyList<RuntimeObjectTraversalEvent> ProcessNext()
        {
            if (IsFinished)
                return [];

            if (EnforceElapsedBudget && _elapsedStopwatch.ElapsedMilliseconds >= Options.MaxElapsedMs)
            {
                IsFinished = true;
                return [new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.TraversalStoppedTimeBudget, string.Empty, Count: Options.MaxElapsedMs)];
            }

            if (TotalProcessedNodes >= Options.MaxTotalNodes)
            {
                IsFinished = true;
                return [new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.TraversalStoppedNodeBudget, string.Empty, Count: Options.MaxTotalNodes)];
            }

            if (_stack.Count == 0)
            {
                IsFinished = true;
                return [new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.TraversalCompleted, string.Empty, Count: TotalProcessedNodes)];
            }

            RuntimeObjectTraversalPendingNode pending = _stack.Pop();
            TotalProcessedNodes++;

            var events = new List<RuntimeObjectTraversalEvent>();
            try
            {
                object? value = pending.Value;
                if (value == null)
                {
                    events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.NodeNull, pending.Path));
                    return events;
                }

                Type type = value.GetType();
                if (RuntimeObjectTraversalSupport.IsSimpleType(type))
                {
                    events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.NodeSimple, pending.Path, RuntimeType: type, Value: value));
                    return events;
                }

                if (!type.IsValueType && !_visited.Add(value))
                {
                    events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.NodeCycle, pending.Path, RuntimeType: type));
                    return events;
                }

                if (value is IEnumerable enumerable && value is not string)
                {
                    events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.EnumerableType, pending.Path, RuntimeType: type));

                    var stagedEntries = new List<RuntimeObjectTraversalPendingNode>();
                    int previewCount = 0;
                    bool hasMore = false;
                    foreach (object? entry in enumerable)
                    {
                        if (previewCount < Options.MaxCollectionItems)
                        {
                            stagedEntries.Add(new RuntimeObjectTraversalPendingNode($"{pending.Path}[{previewCount}]", entry, pending.Depth + 1));
                            previewCount++;
                            continue;
                        }

                        hasMore = true;
                        break;
                    }

                    events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.EnumerablePreviewCount, pending.Path, Count: previewCount));
                    if (hasMore)
                        events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.EnumerableTruncated, pending.Path));

                    for (int i = stagedEntries.Count - 1; i >= 0; i--)
                        _stack.Push(stagedEntries[i]);

                    return events;
                }

                events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.NodeType, pending.Path, RuntimeType: type));
                if (pending.Depth >= Options.MaxDepth)
                {
                    events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.NodeMaxDepth, pending.Path));
                    return events;
                }

                IReadOnlyList<MemberInfo> members = RuntimeObjectTraversalSupport.GetReadableMembers(type, Options.PriorityMembers, Options.IncludeNonPublicMembers);
                if (members.Count == 0)
                {
                    events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.NodeNoReadableMembers, pending.Path));
                    return events;
                }

                int memberCount = SystemMath.Min(members.Count, Options.MaxMembersPerObject);
                if (members.Count > Options.MaxMembersPerObject)
                    events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.MemberOutputTruncated, pending.Path, Count: members.Count - Options.MaxMembersPerObject));

                var stagedMembers = new List<RuntimeObjectTraversalPendingNode>();
                for (int i = 0; i < memberCount; i++)
                {
                    MemberInfo member = members[i];
                    string memberPath = $"{pending.Path}.{member.Name}";

                    if (!RuntimeObjectTraversalSupport.TryGetMemberValue(value, member, out object? memberValue))
                    {
                        events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.MemberUnavailable, memberPath));
                        continue;
                    }

                    if (memberValue == null)
                    {
                        events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.MemberNull, memberPath));
                        continue;
                    }

                    if (RuntimeObjectTraversalSupport.IsSimpleType(memberValue.GetType()))
                    {
                        events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.MemberSimple, memberPath, Value: memberValue));
                        continue;
                    }

                    if (TotalProcessedNodes + _stack.Count + stagedMembers.Count >= Options.MaxTotalNodes)
                    {
                        events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.NodeBudgetReachedWhileSchedulingMembers, pending.Path));
                        break;
                    }

                    stagedMembers.Add(new RuntimeObjectTraversalPendingNode(memberPath, memberValue, pending.Depth + 1));
                }

                for (int i = stagedMembers.Count - 1; i >= 0; i--)
                    _stack.Push(stagedMembers[i]);
            }
            catch (Exception ex)
            {
                events.Add(new RuntimeObjectTraversalEvent(RuntimeObjectTraversalEventKind.NodeProcessingError, pending.Path, Message: $"{ex.GetType().Name}: {ex.Message}"));
            }

            return events;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }

    internal readonly record struct RuntimeObjectTraversalPendingNode(string Path, object? Value, int Depth);

    internal readonly record struct RuntimeObjectTraversalOptions(
        string Title,
        int MaxDepth,
        int MaxCollectionItems,
        IReadOnlyList<string> PriorityMembers,
        int MaxMembersPerObject,
        bool IncludeNonPublicMembers,
        int MaxValueChars,
        int MaxTotalNodes,
        int MaxElapsedMs);

    internal static class RuntimeObjectTraversalSupport
    {
        private readonly record struct MemberCacheKey(Type Type, bool IncludeNonPublicMembers, string PriorityKey);
        private static readonly Dictionary<MemberCacheKey, MemberInfo[]> ReadableMembersCache = [];
        private static readonly object CacheLock = new();

        public static IReadOnlyList<MemberInfo> GetReadableMembers(Type type, IReadOnlyList<string> priorityMembers, bool includeNonPublicMembers)
        {
            string priorityKey = priorityMembers.Count == 0
                ? string.Empty
                : string.Join("|", priorityMembers);
            var cacheKey = new MemberCacheKey(type, includeNonPublicMembers, priorityKey);

            lock (CacheLock)
            {
                if (ReadableMembersCache.TryGetValue(cacheKey, out MemberInfo[]? cachedMembers))
                    return cachedMembers;
            }

            var members = new List<MemberInfo>();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            if (includeNonPublicMembers)
                flags |= BindingFlags.NonPublic;

            PropertyInfo[] properties = type.GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                MethodInfo? getter = property.GetGetMethod(nonPublic: includeNonPublicMembers);
                if (getter == null)
                    continue;

                members.Add(property);
            }

            FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
                members.Add(fields[i]);

            if (members.Count > 1)
            {
                var priorityIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < priorityMembers.Count; i++)
                {
                    if (!priorityIndex.ContainsKey(priorityMembers[i]))
                        priorityIndex[priorityMembers[i]] = i;
                }

                members.Sort((a, b) =>
                {
                    int aPriority = priorityIndex.TryGetValue(a.Name, out int ai) ? ai : int.MaxValue;
                    int bPriority = priorityIndex.TryGetValue(b.Name, out int bi) ? bi : int.MaxValue;

                    int byPriority = aPriority.CompareTo(bPriority);
                    if (byPriority != 0)
                        return byPriority;

                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
            }

            MemberInfo[] sortedMembers = [.. members];
            lock (CacheLock)
            {
                ReadableMembersCache[cacheKey] = sortedMembers;
            }

            return sortedMembers;
        }

        public static bool TryGetMemberValue(object source, MemberInfo member, out object? value)
        {
            value = null;
            if (source == null || member == null)
                return false;

            try
            {
                switch (member)
                {
                    case PropertyInfo property:
                        value = property.GetValue(source);
                        return true;
                    case FieldInfo field:
                        value = field.GetValue(source);
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsSimpleType(Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsPrimitive || underlying.IsEnum)
                return true;

            return underlying == typeof(string)
                || underlying == typeof(decimal)
                || underlying == typeof(DateTime)
                || underlying == typeof(DateTimeOffset)
                || underlying == typeof(TimeSpan)
                || underlying == typeof(Guid);
        }
    }
}