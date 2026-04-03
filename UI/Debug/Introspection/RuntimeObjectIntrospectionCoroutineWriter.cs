namespace ClickIt.UI.Debug.Introspection
{
    internal static class RuntimeObjectIntrospectionCoroutineWriter
    {
        internal static IEnumerator WriteReportToFileCoroutine(
            object? root,
            string filePath,
            RuntimeObjectIntrospectionOptions options,
            Action<string?, string?>? onCompleted = null,
            Action<int>? onProgress = null,
            int nodeBudgetPerYield = 250)
        {
            RuntimeObjectTraversalOptions normalized = RuntimeObjectIntrospectionReportBuilder.NormalizeOptions(options);
            int budget = SystemMath.Max(1, nodeBudgetPerYield);

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
                        int pct = SystemMath.Min(99, (int)((long)engine.TotalProcessedNodes * 100L / SystemMath.Max(1, normalized.MaxTotalNodes)));
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

        private static void SafeInvokeProgress(Action<int>? onProgress, int value)
        {
            try
            {
                onProgress?.Invoke(value);
            }
            catch
            {
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
            }
        }
    }
}