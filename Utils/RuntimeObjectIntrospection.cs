using System.Collections;
using System.Diagnostics;
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
        public static string GetFileNameForProfile(IntrospectionProfile profile)
        {
            return RuntimeObjectIntrospectionProfileMapper.GetFileName(profile);
        }

        public static RuntimeObjectIntrospectionOptions GetOptionsForProfile(IntrospectionProfile profile)
        {
            return RuntimeObjectIntrospectionProfileMapper.GetOptions(profile);
        }

        public static string BuildReport(object? root, RuntimeObjectIntrospectionOptions options)
        {
            RuntimeObjectTraversalOptions normalized = NormalizeOptions(options);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"--- {normalized.Title} ---");

            if (root == null)
            {
                sb.AppendLine("Root: unavailable");
                return sb.ToString().TrimEnd();
            }

            var engine = new RuntimeObjectTraversalEngine(root, normalized, enforceElapsedBudget: false);
            while (!engine.IsFinished)
            {
                IReadOnlyList<RuntimeObjectTraversalEvent> events = engine.ProcessNext();
                RuntimeObjectIntrospectionEventFormatter.AppendTraversalEvents(sb, events, normalized.MaxValueChars);
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
            RuntimeObjectTraversalOptions normalized = NormalizeOptions(options);
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
                if (!RuntimeObjectIntrospectionStreamWriter.TryWriteLine(writer, $"--- {normalized.Title} ---", out string? headerWriteError))
                {
                    SafeInvokeCompleted(onCompleted, null, headerWriteError);
                    yield break;
                }

                SafeInvokeProgress(onProgress, 0);

                if (root == null)
                {
                    _ = RuntimeObjectIntrospectionStreamWriter.TryWriteLine(writer, "Root: unavailable", out _);
                    SafeInvokeProgress(onProgress, 100);
                    SafeInvokeCompleted(onCompleted, fullPath, null);
                    yield break;
                }

                var engine = new RuntimeObjectTraversalEngine(root, normalized, enforceElapsedBudget: true);
                const int maxSliceMs = 1;
                var sliceStopwatch = Stopwatch.StartNew();

                int processedSinceYield = 0;
                int previousProcessedNodes = 0;
                while (!engine.IsFinished)
                {
                    IReadOnlyList<RuntimeObjectTraversalEvent> events = engine.ProcessNext();
                    if (!RuntimeObjectIntrospectionStreamWriter.TryWriteTraversalEvents(writer, events, normalized.MaxValueChars, out string? traversalWriteError))
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

                        if (!RuntimeObjectIntrospectionStreamWriter.TryFlush(writer, out string? flushError))
                        {
                            SafeInvokeCompleted(onCompleted, null, flushError);
                            yield break;
                        }

                        yield return null;
                    }
                }

                if (!RuntimeObjectIntrospectionStreamWriter.TryFlush(writer, out string? finalFlushError))
                {
                    SafeInvokeCompleted(onCompleted, null, finalFlushError);
                    yield break;
                }

                SafeInvokeProgress(onProgress, 100);
                SafeInvokeCompleted(onCompleted, fullPath, null);
            }
        }

        private static RuntimeObjectTraversalOptions NormalizeOptions(RuntimeObjectIntrospectionOptions options)
        {
            string title = string.IsNullOrWhiteSpace(options.Title)
                ? RuntimeObjectIntrospectionOptions.Default.Title
                : options.Title;

            return new RuntimeObjectTraversalOptions(
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

        internal static string FormatValue(object? value, int maxLen = 120)
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
    }
}