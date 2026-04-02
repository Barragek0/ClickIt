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
            => RuntimeObjectIntrospectionReportBuilder.BuildReport(root, options);

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
            => RuntimeObjectIntrospectionReportBuilder.WriteReportToFile(root, filePath, options);

        public static IEnumerator WriteReportToFileCoroutine(
            object? root,
            string filePath,
            RuntimeObjectIntrospectionOptions options,
            Action<string?, string?>? onCompleted = null,
            Action<int>? onProgress = null,
            int nodeBudgetPerYield = 250)
            => RuntimeObjectIntrospectionCoroutineWriter.WriteReportToFileCoroutine(root, filePath, options, onCompleted, onProgress, nodeBudgetPerYield);

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