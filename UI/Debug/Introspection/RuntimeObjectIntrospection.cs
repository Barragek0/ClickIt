using System.Collections;
namespace ClickIt.UI.Debug.Introspection
{
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
            => RuntimeObjectIntrospectionValueFormatter.FormatValue(value, maxLen);
    }
}