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
            string title = string.IsNullOrWhiteSpace(options.Title)
                ? RuntimeObjectIntrospectionOptions.Default.Title
                : options.Title;

            int maxDepth = Math.Max(0, options.MaxDepth);
            int maxCollectionItems = Math.Max(1, options.MaxCollectionItems);
            int maxMembersPerObject = Math.Max(1, options.MaxMembersPerObject);
            int maxValueChars = Math.Max(1, options.MaxValueChars);
            int maxTotalNodes = Math.Max(1, options.MaxTotalNodes);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"--- {title} ---");

            if (root == null)
            {
                sb.AppendLine("Root: unavailable");
                return sb.ToString().TrimEnd();
            }

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            int nodesProcessed = 0;
            AppendNode(
                sb,
                "Root",
                root,
                0,
                maxDepth,
                maxCollectionItems,
                options.PriorityMembers ?? [],
                options.IncludeNonPublicMembers,
                maxMembersPerObject,
                maxValueChars,
                visited,
                maxTotalNodes,
                ref nodesProcessed);

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
            int maxDepth = Math.Max(0, options.MaxDepth);
            int maxCollectionItems = Math.Max(1, options.MaxCollectionItems);
            int maxMembersPerObject = Math.Max(1, options.MaxMembersPerObject);
            int maxValueChars = Math.Max(1, options.MaxValueChars);
            int maxTotalNodes = Math.Max(1, options.MaxTotalNodes);
            int maxElapsedMs = Math.Max(500, options.MaxElapsedMs);
            int budget = Math.Max(1, nodeBudgetPerYield);
            IReadOnlyList<string> priorityMembers = options.PriorityMembers ?? [];

            string title = string.IsNullOrWhiteSpace(options.Title)
                ? RuntimeObjectIntrospectionOptions.Default.Title
                : options.Title;

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
                if (!TryWriteLine(writer, $"--- {title} ---", out string? headerWriteError))
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

                var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var stack = new Stack<PendingNode>();
                stack.Push(new PendingNode("Root", root, 0));
                int totalProcessedNodes = 0;
                const int maxSliceMs = 1;
                var sliceStopwatch = Stopwatch.StartNew();
                var totalStopwatch = Stopwatch.StartNew();

                int processedSinceYield = 0;
                bool stoppedByNodeBudget = false;
                bool stoppedByTimeBudget = false;
                while (stack.Count > 0)
                {
                    if (totalStopwatch.ElapsedMilliseconds >= maxElapsedMs)
                    {
                        if (!TryWriteLine(writer, $"Traversal stopped: elapsed-time budget reached ({maxElapsedMs}ms).", out string? timeWriteError))
                        {
                            SafeInvokeCompleted(onCompleted, null, timeWriteError);
                            yield break;
                        }

                        stoppedByTimeBudget = true;
                        SafeInvokeProgress(onProgress, 100);
                        break;
                    }

                    if (totalProcessedNodes >= maxTotalNodes)
                    {
                        if (!TryWriteLine(writer, $"Traversal stopped: node budget reached ({maxTotalNodes}).", out string? budgetWriteError))
                        {
                            SafeInvokeCompleted(onCompleted, null, budgetWriteError);
                            yield break;
                        }

                        stoppedByNodeBudget = true;
                        SafeInvokeProgress(onProgress, 100);
                        break;
                    }

                    PendingNode pending = stack.Pop();
                    totalProcessedNodes++;
                    try
                    {
                        object? value = pending.Value;

                        if (value == null)
                        {
                            writer.WriteLine($"{pending.Path}: null");
                        }
                        else
                        {
                            Type type = value.GetType();
                            if (IsSimpleType(type))
                            {
                                writer.WriteLine($"{pending.Path}: {type.Name} = {FormatValue(value, maxValueChars)}");
                            }
                            else
                            {
                                if (!type.IsValueType)
                                {
                                    if (!visited.Add(value))
                                    {
                                        writer.WriteLine($"{pending.Path}: {type.FullName} (cycle)");
                                        goto AfterNode;
                                    }
                                }

                                if (value is IEnumerable enumerable && value is not string)
                                {
                                    writer.WriteLine($"{pending.Path}: {type.FullName} (enumerable)");

                                    var stagedEntries = new List<PendingNode>();
                                    int previewCount = 0;
                                    bool hasMore = false;
                                    foreach (object? entry in enumerable)
                                    {
                                        if (previewCount < maxCollectionItems)
                                        {
                                            stagedEntries.Add(new PendingNode($"{pending.Path}[{previewCount}]", entry, pending.Depth + 1));
                                            previewCount++;
                                            continue;
                                        }

                                        hasMore = true;
                                        break;
                                    }

                                    writer.WriteLine($"{pending.Path}: previewCount={previewCount}");
                                    if (hasMore)
                                        writer.WriteLine($"{pending.Path}: collection output truncated (more entries omitted)");

                                    for (int i = stagedEntries.Count - 1; i >= 0; i--)
                                        stack.Push(stagedEntries[i]);

                                    goto AfterNode;
                                }

                                writer.WriteLine($"{pending.Path}: {type.FullName}");
                                if (pending.Depth >= maxDepth)
                                {
                                    writer.WriteLine($"{pending.Path}: max depth reached");
                                    goto AfterNode;
                                }

                                IReadOnlyList<MemberInfo> members = GetReadableMembers(type, priorityMembers, options.IncludeNonPublicMembers);
                                if (members.Count == 0)
                                {
                                    writer.WriteLine($"{pending.Path}: no readable public members");
                                    goto AfterNode;
                                }

                                int memberCount = Math.Min(members.Count, maxMembersPerObject);
                                if (members.Count > maxMembersPerObject)
                                    writer.WriteLine($"{pending.Path}: member output truncated ({members.Count - maxMembersPerObject} omitted)");

                                var stagedMembers = new List<PendingNode>();
                                for (int i = 0; i < memberCount; i++)
                                {
                                    MemberInfo member = members[i];
                                    string memberPath = $"{pending.Path}.{member.Name}";

                                    if (!TryGetMemberValue(value, member, out object? memberValue))
                                    {
                                        writer.WriteLine($"{memberPath}: <unavailable>");
                                        continue;
                                    }

                                    if (memberValue == null)
                                    {
                                        writer.WriteLine($"{memberPath}: null");
                                        continue;
                                    }

                                    if (IsSimpleType(memberValue.GetType()))
                                    {
                                        writer.WriteLine($"{memberPath}: {FormatValue(memberValue, maxValueChars)}");
                                        continue;
                                    }

                                    if (totalProcessedNodes + stack.Count + stagedMembers.Count >= maxTotalNodes)
                                    {
                                        writer.WriteLine($"{pending.Path}: node budget reached while scheduling members");
                                        break;
                                    }

                                    stagedMembers.Add(new PendingNode(memberPath, memberValue, pending.Depth + 1));
                                }

                                for (int i = stagedMembers.Count - 1; i >= 0; i--)
                                    stack.Push(stagedMembers[i]);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!TryWriteLine(writer, $"{pending.Path}: <node processing error> {ex.GetType().Name}: {ex.Message}", out string? nodeErrorWriteFailure))
                        {
                            SafeInvokeCompleted(onCompleted, null, nodeErrorWriteFailure);
                            yield break;
                        }
                    }

                AfterNode:
                    processedSinceYield++;
                    if (processedSinceYield >= budget || sliceStopwatch.ElapsedMilliseconds >= maxSliceMs)
                    {
                        processedSinceYield = 0;
                        sliceStopwatch.Restart();
                        int pct = Math.Min(99, (int)((long)totalProcessedNodes * 100L / Math.Max(1, maxTotalNodes)));
                        SafeInvokeProgress(onProgress, pct);

                        if (!TryFlush(writer, out string? flushError))
                        {
                            SafeInvokeCompleted(onCompleted, null, flushError);
                            yield break;
                        }

                        yield return null;
                    }
                }

                if (!stoppedByNodeBudget && !stoppedByTimeBudget)
                {
                    if (!TryWriteLine(writer, $"Traversal completed: processed {totalProcessedNodes} nodes.", out string? completeWriteError))
                    {
                        SafeInvokeCompleted(onCompleted, null, completeWriteError);
                        yield break;
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

        private static void AppendNode(
            StringBuilder sb,
            string path,
            object? value,
            int depth,
            int maxDepth,
            int maxCollectionItems,
            IReadOnlyList<string> priorityMembers,
            bool includeNonPublicMembers,
            int maxMembersPerObject,
            int maxValueChars,
            HashSet<object> visited,
            int maxTotalNodes,
            ref int nodesProcessed)
        {
            if (nodesProcessed >= maxTotalNodes)
            {
                sb.AppendLine($"{path}: node budget reached ({maxTotalNodes})");
                return;
            }

            nodesProcessed++;

            if (value == null)
            {
                sb.AppendLine($"{path}: null");
                return;
            }

            Type type = value.GetType();
            if (IsSimpleType(type))
            {
                sb.AppendLine($"{path}: {type.Name} = {FormatValue(value, maxValueChars)}");
                return;
            }

            if (!type.IsValueType)
            {
                if (!visited.Add(value))
                {
                    sb.AppendLine($"{path}: {type.FullName} (cycle)");
                    return;
                }
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                AppendEnumerable(
                    sb,
                    path,
                    type,
                    enumerable,
                    depth,
                    maxDepth,
                    maxCollectionItems,
                    priorityMembers,
                    includeNonPublicMembers,
                    maxMembersPerObject,
                    maxValueChars,
                    visited,
                    maxTotalNodes,
                    ref nodesProcessed);
                return;
            }

            sb.AppendLine($"{path}: {type.FullName}");
            if (depth >= maxDepth)
            {
                sb.AppendLine($"{path}: max depth reached");
                return;
            }

            IReadOnlyList<MemberInfo> members = GetReadableMembers(type, priorityMembers, includeNonPublicMembers);
            if (members.Count == 0)
            {
                sb.AppendLine($"{path}: no readable public members");
                return;
            }

            int memberCount = Math.Min(members.Count, maxMembersPerObject);
            for (int i = 0; i < memberCount; i++)
            {
                MemberInfo member = members[i];
                string memberPath = $"{path}.{member.Name}";

                if (!TryGetMemberValue(value, member, out object? memberValue))
                {
                    sb.AppendLine($"{memberPath}: <unavailable>");
                    continue;
                }

                if (memberValue == null)
                {
                    sb.AppendLine($"{memberPath}: null");
                    continue;
                }

                if (IsSimpleType(memberValue.GetType()))
                {
                    sb.AppendLine($"{memberPath}: {FormatValue(memberValue, maxValueChars)}");
                    continue;
                }

                AppendNode(
                    sb,
                    memberPath,
                    memberValue,
                    depth + 1,
                    maxDepth,
                    maxCollectionItems,
                    priorityMembers,
                    includeNonPublicMembers,
                    maxMembersPerObject,
                    maxValueChars,
                        visited,
                        maxTotalNodes,
                        ref nodesProcessed);
            }

            if (members.Count > maxMembersPerObject)
                sb.AppendLine($"{path}: member output truncated ({members.Count - maxMembersPerObject} omitted)");
        }

        private static void AppendEnumerable(
            StringBuilder sb,
            string path,
            Type type,
            IEnumerable enumerable,
            int depth,
            int maxDepth,
            int maxCollectionItems,
            IReadOnlyList<string> priorityMembers,
            bool includeNonPublicMembers,
            int maxMembersPerObject,
            int maxValueChars,
            HashSet<object> visited,
            int maxTotalNodes,
            ref int nodesProcessed)
        {
            sb.AppendLine($"{path}: {type.FullName} (enumerable)");

            int previewCount = 0;
            bool hasMore = false;
            foreach (object? entry in enumerable)
            {
                if (previewCount < maxCollectionItems)
                {
                    AppendNode(
                        sb,
                        $"{path}[{previewCount}]",
                        entry,
                        depth + 1,
                        maxDepth,
                        maxCollectionItems,
                        priorityMembers,
                        includeNonPublicMembers,
                        maxMembersPerObject,
                        maxValueChars,
                        visited,
                        maxTotalNodes,
                        ref nodesProcessed);

                    previewCount++;
                    continue;
                }

                hasMore = true;
                break;
            }

            sb.AppendLine($"{path}: previewCount={previewCount}");
            if (hasMore)
                sb.AppendLine($"{path}: collection output truncated (more entries omitted)");
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