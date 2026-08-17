namespace ClickIt.UI.Debug.Introspection
{
    internal static class RuntimeObjectIntrospection
    {
        public static string BuildReport(object? root, RuntimeObjectIntrospectionOptions options)
            => RuntimeObjectIntrospectionReportBuilder.BuildReport(root, options);

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

        public static IEnumerator WriteReportCoroutine(
            object? root,
            StreamWriter writer,
            RuntimeObjectIntrospectionOptions options,
            Action<int>? onProgress = null,
            Action<string?>? onError = null,
            int nodeBudgetPerYield = 120,
            Action<int>? onTotalNodes = null,
            int maxSliceMsPerYield = 1,
            bool measureProgressTotal = false)
            => RuntimeObjectIntrospectionCoroutineWriter.WriteReportCoroutine(root, writer, options, onProgress, onError, nodeBudgetPerYield, onTotalNodes, maxSliceMsPerYield, measureProgressTotal);

        internal static string FormatValue(object? value, int maxLen = 120)
            => RuntimeObjectIntrospectionValueFormatter.FormatValue(value, maxLen);
    }
}