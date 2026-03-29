using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ClickIt.Utils
{
    internal enum IntrospectionProfile
    {
        Default,
        StructureFirst,
        Full
    }

    internal readonly record struct RuntimeObjectIntrospectionOptions(
        string Title,
        int MaxDepth,
        int MaxCollectionItems,
        IReadOnlyList<string>? PriorityMembers,
        int MaxMembersPerObject = 48,
        bool IncludeNonPublicMembers = false,
        int MaxValueChars = 120,
        int MaxTotalNodes = 25000,
        int MaxElapsedMs = 12000)
    {
        public static RuntimeObjectIntrospectionOptions Default => new(
            Title: "Runtime Object Introspection",
            MaxDepth: 8,
            MaxCollectionItems: 5,
            PriorityMembers: [],
            MaxMembersPerObject: 20,
            IncludeNonPublicMembers: false,
            MaxValueChars: 256,
            MaxTotalNodes: 25000,
            MaxElapsedMs: 12000);

        public static RuntimeObjectIntrospectionOptions StructureFirst => new(
            Title: "Structure-First Memory Dump",
            MaxDepth: 64,
            MaxCollectionItems: 5,
            PriorityMembers: [],
            MaxMembersPerObject: 20,
            IncludeNonPublicMembers: false,
            MaxValueChars: int.MaxValue,
            MaxTotalNodes: 25000,
            MaxElapsedMs: 12000);

        public static RuntimeObjectIntrospectionOptions VeryDeepAllData => new(
            Title: "Full Game Memory Dump",
            MaxDepth: int.MaxValue,
            MaxCollectionItems: 3,
            PriorityMembers: [],
            MaxMembersPerObject: int.MaxValue,
            IncludeNonPublicMembers: false,
            MaxValueChars: int.MaxValue,
            MaxTotalNodes: 60000,
            MaxElapsedMs: 30000);
    }

    internal static class RuntimeObjectIntrospection
    {
        private readonly record struct PendingNode(string Path, object? Value, int Depth);
        private readonly record struct NormalizedTraversalOptions(
            string Title,
            int MaxDepth,
            int MaxCollectionItems,
            IReadOnlyList<string> PriorityMembers,
            int MaxMembersPerObject,
            bool IncludeNonPublicMembers,
            int MaxValueChars,
            int MaxTotalNodes,
            int MaxElapsedMs);
        private readonly record struct TraversalEvent(
            TraversalEventKind Kind,
            string Path,
            Type? RuntimeType = null,
            object? Value = null,
            int Count = 0,
            string? Message = null);
        private enum TraversalEventKind
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
        private readonly record struct MemberCacheKey(Type Type, bool IncludeNonPublicMembers, string PriorityKey);
        private static readonly ConcurrentDictionary<MemberCacheKey, MemberInfo[]> ReadableMembersCache = new();

        public static string GetFileNameForProfile(IntrospectionProfile profile)
        {
            return profile switch
            {
                IntrospectionProfile.StructureFirst => "structure.dat",
                IntrospectionProfile.Full => "full.dat",
                _ => "memory.dat"
            };
        }

        public static RuntimeObjectIntrospectionOptions GetOptionsForProfile(IntrospectionProfile profile)
        {
            return profile switch
            {
                IntrospectionProfile.StructureFirst => RuntimeObjectIntrospectionOptions.StructureFirst,
                IntrospectionProfile.Full => RuntimeObjectIntrospectionOptions.VeryDeepAllData,
                _ => RuntimeObjectIntrospectionOptions.Default
            };
        }

        public static string BuildReport(object? root, RuntimeObjectIntrospectionOptions options)
        {
            NormalizedTraversalOptions normalized = NormalizeOptions(options);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"--- {normalized.Title} ---");

            if (root == null)
            {
                sb.AppendLine("Root: unavailable");
                return sb.ToString().TrimEnd();
            }

            var engine = new TraversalEngine(root, normalized, enforceElapsedBudget: false);
            while (!engine.IsFinished)
            {
                IReadOnlyList<TraversalEvent> events = engine.ProcessNext();
                AppendTraversalEvents(sb, events, normalized.MaxValueChars);
            }

            return sb.ToString().TrimEnd();
        }

        public static string WriteVeryDeepMemorySnapshotToFile(object? root, string filePath)
            => WriteReportToFile(root, filePath, RuntimeObjectIntrospectionOptions.VeryDeepAllData);

        public static string WriteStructureFirstMemorySnapshotToFile(object? root, string filePath)
            => WriteReportToFile(root, filePath, RuntimeObjectIntrospectionOptions.StructureFirst);

        public static string WriteMemorySnapshotToFile(
            object? root,
            string filePath,
            IntrospectionProfile profile)
            => WriteReportToFile(root, filePath, GetOptionsForProfile(profile));

        public static IEnumerator WriteVeryDeepMemorySnapshotCoroutine(
            object? root,
            string filePath,
            Action<string?, string?>? onCompleted = null,
            Action<int>? onProgress = null,
            int nodeBudgetPerYield = 250)
            => WriteReportToFileCoroutine(root, filePath, RuntimeObjectIntrospectionOptions.VeryDeepAllData, onCompleted, onProgress, nodeBudgetPerYield);

        public static IEnumerator WriteStructureFirstMemorySnapshotCoroutine(
            object? root,
            string filePath,
            Action<string?, string?>? onCompleted = null,
            Action<int>? onProgress = null,
            int nodeBudgetPerYield = 120)
            => WriteReportToFileCoroutine(root, filePath, RuntimeObjectIntrospectionOptions.StructureFirst, onCompleted, onProgress, nodeBudgetPerYield);

        public static IEnumerator WriteMemorySnapshotCoroutine(
            object? root,
            string filePath,
            IntrospectionProfile profile,
            Action<string?, string?>? onCompleted = null,
            Action<int>? onProgress = null,
            int nodeBudgetPerYield = 120)
            => WriteReportToFileCoroutine(root, filePath, GetOptionsForProfile(profile), onCompleted, onProgress, nodeBudgetPerYield);

        public static string WriteReportToFile(object? root, string filePath, RuntimeObjectIntrospectionOptions options)
        {
            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string content = BuildReport(root, options);
            File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return fullPath;
        }

        public static IEnumerator WriteReportToFileCoroutine(
            object? root,
            string filePath,
            RuntimeObjectIntrospectionOptions options,
            Action<string?, string?>? onCompleted = null,
            Action<int>? onProgress = null,
            int nodeBudgetPerYield = 250)
        {
            NormalizedTraversalOptions normalized = NormalizeOptions(options);
            int budget = Math.Max(1, nodeBudgetPerYield);

            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);
            try
            {
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                SafeInvokeCompleted(onCompleted, null, $"Failed to create dump directory: {ex.Message}");
                yield break;
            }

            StreamWriter? writer = null;
            try
            {
                writer = new StreamWriter(fullPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                SafeInvokeCompleted(onCompleted, null, $"Failed to open dump file: {ex.Message}");
                yield break;
            }

            using (writer)
            {
                if (!TryWriteLine(writer, $"--- {normalized.Title} ---", out string? headerWriteError))
                {
                    SafeInvokeCompleted(onCompleted, null, headerWriteError);
                    yield break;
                }

                SafeInvokeProgress(onProgress, 0);

                if (root == null)
                {
                    _ = TryWriteLine(writer, "Root: unavailable", out _);
                    SafeInvokeProgress(onProgress, 100);
                    SafeInvokeCompleted(onCompleted, fullPath, null);
                    yield break;
                }

                var engine = new TraversalEngine(root, normalized, enforceElapsedBudget: true);
                const int maxSliceMs = 1;
                var sliceStopwatch = Stopwatch.StartNew();

                int processedSinceYield = 0;
                int previousProcessedNodes = 0;
                while (!engine.IsFinished)
                {
                    IReadOnlyList<TraversalEvent> events = engine.ProcessNext();
                    if (!TryWriteTraversalEvents(writer, events, normalized.MaxValueChars, out string? traversalWriteError))
                    {
                        SafeInvokeCompleted(onCompleted, null, traversalWriteError);
                        yield break;
                    }

                    if (engine.TotalProcessedNodes > previousProcessedNodes)
                        processedSinceYield += engine.TotalProcessedNodes - previousProcessedNodes;
                    previousProcessedNodes = engine.TotalProcessedNodes;

                    if (processedSinceYield >= budget || sliceStopwatch.ElapsedMilliseconds >= maxSliceMs)
                    {
                        processedSinceYield = 0;
                        sliceStopwatch.Restart();
                        int pct = Math.Min(99, (int)((long)engine.TotalProcessedNodes * 100L / Math.Max(1, normalized.MaxTotalNodes)));
                        SafeInvokeProgress(onProgress, pct);

                        if (!TryFlush(writer, out string? flushError))
                        {
                            SafeInvokeCompleted(onCompleted, null, flushError);
                            yield break;
                        }

                        yield return null;
                    }
                }

                if (!TryFlush(writer, out string? finalFlushError))
                {
                    SafeInvokeCompleted(onCompleted, null, finalFlushError);
                    yield break;
                }

                SafeInvokeProgress(onProgress, 100);
                SafeInvokeCompleted(onCompleted, fullPath, null);
            }
        }

        private static NormalizedTraversalOptions NormalizeOptions(RuntimeObjectIntrospectionOptions options)
        {
            string title = string.IsNullOrWhiteSpace(options.Title)
                ? RuntimeObjectIntrospectionOptions.Default.Title
                : options.Title;

            return new NormalizedTraversalOptions(
                Title: title,
                MaxDepth: Math.Max(0, options.MaxDepth),
                MaxCollectionItems: Math.Max(1, options.MaxCollectionItems),
                PriorityMembers: options.PriorityMembers ?? [],
                MaxMembersPerObject: Math.Max(1, options.MaxMembersPerObject),
                IncludeNonPublicMembers: options.IncludeNonPublicMembers,
                MaxValueChars: Math.Max(1, options.MaxValueChars),
                MaxTotalNodes: Math.Max(1, options.MaxTotalNodes),
                MaxElapsedMs: Math.Max(500, options.MaxElapsedMs));
        }

        private static void AppendTraversalEvents(StringBuilder sb, IReadOnlyList<TraversalEvent> events, int maxValueChars)
        {
            for (int i = 0; i < events.Count; i++)
                sb.AppendLine(FormatTraversalEvent(events[i], maxValueChars));
        }

        private static bool TryWriteTraversalEvents(StreamWriter writer, IReadOnlyList<TraversalEvent> events, int maxValueChars, out string? error)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (!TryWriteLine(writer, FormatTraversalEvent(events[i], maxValueChars), out error))
                    return false;
            }

            error = null;
            return true;
        }

        private static string FormatTraversalEvent(TraversalEvent evt, int maxValueChars)
        {
            return evt.Kind switch
            {
                TraversalEventKind.NodeNull => $"{evt.Path}: null",
                TraversalEventKind.NodeSimple => $"{evt.Path}: {evt.RuntimeType?.Name} = {FormatValue(evt.Value, maxValueChars)}",
                TraversalEventKind.NodeType => $"{evt.Path}: {evt.RuntimeType?.FullName}",
                TraversalEventKind.NodeCycle => $"{evt.Path}: {evt.RuntimeType?.FullName} (cycle)",
                TraversalEventKind.NodeMaxDepth => $"{evt.Path}: max depth reached",
                TraversalEventKind.NodeNoReadableMembers => $"{evt.Path}: no readable public members",
                TraversalEventKind.EnumerableType => $"{evt.Path}: {evt.RuntimeType?.FullName} (enumerable)",
                TraversalEventKind.EnumerablePreviewCount => $"{evt.Path}: previewCount={evt.Count}",
                TraversalEventKind.EnumerableTruncated => $"{evt.Path}: collection output truncated (more entries omitted)",
                TraversalEventKind.MemberUnavailable => $"{evt.Path}: <unavailable>",
                TraversalEventKind.MemberNull => $"{evt.Path}: null",
                TraversalEventKind.MemberSimple => $"{evt.Path}: {FormatValue(evt.Value, maxValueChars)}",
                TraversalEventKind.MemberOutputTruncated => $"{evt.Path}: member output truncated ({evt.Count} omitted)",
                TraversalEventKind.NodeBudgetReachedWhileSchedulingMembers => $"{evt.Path}: node budget reached while scheduling members",
                TraversalEventKind.TraversalStoppedTimeBudget => $"Traversal stopped: elapsed-time budget reached ({evt.Count}ms).",
                TraversalEventKind.TraversalStoppedNodeBudget => $"Traversal stopped: node budget reached ({evt.Count}).",
                TraversalEventKind.TraversalCompleted => $"Traversal completed: processed {evt.Count} nodes.",
                TraversalEventKind.NodeProcessingError => $"{evt.Path}: <node processing error> {evt.Message}",
                _ => $"{evt.Path}: <unknown traversal event>"
            };
        }

        private static bool TryWriteLine(StreamWriter writer, string line, out string? error)
        {
            try
            {
                writer.WriteLine(line);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed while writing dump file: {ex.Message}";
                return false;
            }
        }

        private static bool TryFlush(StreamWriter writer, out string? error)
        {
            try
            {
                writer.Flush();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed while flushing dump file: {ex.Message}";
                return false;
            }
        }

        private static void SafeInvokeProgress(Action<int>? onProgress, int value)
        {
            try
            {
                onProgress?.Invoke(value);
            }
            catch
            {
                // Never allow UI callback failures to kill the dump coroutine.
            }
        }

        private static void SafeInvokeCompleted(Action<string?, string?>? onCompleted, string? path, string? error)
        {
            try
            {
                onCompleted?.Invoke(path, error);
            }
            catch
            {
                // Never throw from completion callback.
            }
        }

        private sealed class TraversalEngine
        {
            private readonly Stack<PendingNode> _stack;
            private readonly HashSet<object> _visited;
            private readonly Stopwatch _elapsedStopwatch;

            public TraversalEngine(object root, NormalizedTraversalOptions options, bool enforceElapsedBudget)
            {
                Options = options;
                EnforceElapsedBudget = enforceElapsedBudget;
                _stack = new Stack<PendingNode>();
                _visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                _elapsedStopwatch = Stopwatch.StartNew();
                _stack.Push(new PendingNode("Root", root, 0));
            }

            public NormalizedTraversalOptions Options { get; }
            public bool EnforceElapsedBudget { get; }
            public int TotalProcessedNodes { get; private set; }
            public bool IsFinished { get; private set; }

            public IReadOnlyList<TraversalEvent> ProcessNext()
            {
                if (IsFinished)
                    return [];

                if (EnforceElapsedBudget && _elapsedStopwatch.ElapsedMilliseconds >= Options.MaxElapsedMs)
                {
                    IsFinished = true;
                    return [new TraversalEvent(TraversalEventKind.TraversalStoppedTimeBudget, string.Empty, Count: Options.MaxElapsedMs)];
                }

                if (TotalProcessedNodes >= Options.MaxTotalNodes)
                {
                    IsFinished = true;
                    return [new TraversalEvent(TraversalEventKind.TraversalStoppedNodeBudget, string.Empty, Count: Options.MaxTotalNodes)];
                }

                if (_stack.Count == 0)
                {
                    IsFinished = true;
                    return [new TraversalEvent(TraversalEventKind.TraversalCompleted, string.Empty, Count: TotalProcessedNodes)];
                }

                PendingNode pending = _stack.Pop();
                TotalProcessedNodes++;

                var events = new List<TraversalEvent>();
                try
                {
                    object? value = pending.Value;
                    if (value == null)
                    {
                        events.Add(new TraversalEvent(TraversalEventKind.NodeNull, pending.Path));
                        return events;
                    }

                    Type type = value.GetType();
                    if (IsSimpleType(type))
                    {
                        events.Add(new TraversalEvent(TraversalEventKind.NodeSimple, pending.Path, RuntimeType: type, Value: value));
                        return events;
                    }

                    if (!type.IsValueType && !_visited.Add(value))
                    {
                        events.Add(new TraversalEvent(TraversalEventKind.NodeCycle, pending.Path, RuntimeType: type));
                        return events;
                    }

                    if (value is IEnumerable enumerable && value is not string)
                    {
                        events.Add(new TraversalEvent(TraversalEventKind.EnumerableType, pending.Path, RuntimeType: type));

                        var stagedEntries = new List<PendingNode>();
                        int previewCount = 0;
                        bool hasMore = false;
                        foreach (object? entry in enumerable)
                        {
                            if (previewCount < Options.MaxCollectionItems)
                            {
                                stagedEntries.Add(new PendingNode($"{pending.Path}[{previewCount}]", entry, pending.Depth + 1));
                                previewCount++;
                                continue;
                            }

                            hasMore = true;
                            break;
                        }

                        events.Add(new TraversalEvent(TraversalEventKind.EnumerablePreviewCount, pending.Path, Count: previewCount));
                        if (hasMore)
                            events.Add(new TraversalEvent(TraversalEventKind.EnumerableTruncated, pending.Path));

                        for (int i = stagedEntries.Count - 1; i >= 0; i--)
                            _stack.Push(stagedEntries[i]);

                        return events;
                    }

                    events.Add(new TraversalEvent(TraversalEventKind.NodeType, pending.Path, RuntimeType: type));
                    if (pending.Depth >= Options.MaxDepth)
                    {
                        events.Add(new TraversalEvent(TraversalEventKind.NodeMaxDepth, pending.Path));
                        return events;
                    }

                    IReadOnlyList<MemberInfo> members = GetReadableMembers(type, Options.PriorityMembers, Options.IncludeNonPublicMembers);
                    if (members.Count == 0)
                    {
                        events.Add(new TraversalEvent(TraversalEventKind.NodeNoReadableMembers, pending.Path));
                        return events;
                    }

                    int memberCount = Math.Min(members.Count, Options.MaxMembersPerObject);
                    if (members.Count > Options.MaxMembersPerObject)
                        events.Add(new TraversalEvent(TraversalEventKind.MemberOutputTruncated, pending.Path, Count: members.Count - Options.MaxMembersPerObject));

                    var stagedMembers = new List<PendingNode>();
                    for (int i = 0; i < memberCount; i++)
                    {
                        MemberInfo member = members[i];
                        string memberPath = $"{pending.Path}.{member.Name}";

                        if (!TryGetMemberValue(value, member, out object? memberValue))
                        {
                            events.Add(new TraversalEvent(TraversalEventKind.MemberUnavailable, memberPath));
                            continue;
                        }

                        if (memberValue == null)
                        {
                            events.Add(new TraversalEvent(TraversalEventKind.MemberNull, memberPath));
                            continue;
                        }

                        if (IsSimpleType(memberValue.GetType()))
                        {
                            events.Add(new TraversalEvent(TraversalEventKind.MemberSimple, memberPath, Value: memberValue));
                            continue;
                        }

                        if (TotalProcessedNodes + _stack.Count + stagedMembers.Count >= Options.MaxTotalNodes)
                        {
                            events.Add(new TraversalEvent(TraversalEventKind.NodeBudgetReachedWhileSchedulingMembers, pending.Path));
                            break;
                        }

                        stagedMembers.Add(new PendingNode(memberPath, memberValue, pending.Depth + 1));
                    }

                    for (int i = stagedMembers.Count - 1; i >= 0; i--)
                        _stack.Push(stagedMembers[i]);
                }
                catch (Exception ex)
                {
                    events.Add(new TraversalEvent(TraversalEventKind.NodeProcessingError, pending.Path, Message: $"{ex.GetType().Name}: {ex.Message}"));
                }

                return events;
            }
        }

        private static IReadOnlyList<MemberInfo> GetReadableMembers(Type type, IReadOnlyList<string> priorityMembers, bool includeNonPublicMembers)
        {
            string priorityKey = priorityMembers.Count == 0
                ? string.Empty
                : string.Join("|", priorityMembers);
            var cacheKey = new MemberCacheKey(type, includeNonPublicMembers, priorityKey);
            if (ReadableMembersCache.TryGetValue(cacheKey, out MemberInfo[]? cachedMembers))
                return cachedMembers;

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

            if (members.Count <= 1)
            {
                MemberInfo[] simpleMembers = [.. members];
                ReadableMembersCache.TryAdd(cacheKey, simpleMembers);
                return simpleMembers;
            }

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

            MemberInfo[] sortedMembers = [.. members];
            ReadableMembersCache.TryAdd(cacheKey, sortedMembers);
            return sortedMembers;
        }

        private static bool TryGetMemberValue(object source, MemberInfo member, out object? value)
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

        private static string FormatValue(object? value, int maxLen = 120)
        {
            if (value == null)
                return "null";

            string text = value switch
            {
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

            if (maxLen > 0 && text.Length > maxLen)
                text = text[..maxLen] + "...";

            return text;
        }

        private static bool IsSimpleType(Type type)
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

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}