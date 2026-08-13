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
                bool failed = false;
                IEnumerator routine = WriteReportCoroutine(
                    root,
                    writer,
                    options,
                    onProgress,
                    error => { failed = true; SafeInvokeCompleted(onCompleted, null, error); },
                    nodeBudgetPerYield);
                while (routine.MoveNext())
                    yield return null;
                if (!failed)
                    SafeInvokeCompleted(onCompleted, fullPath, null);
            }
        }

        // Streaming report core shared by the file writer and the clipboard dumper: writes the title, traverses the root, and writes event slices to the provided writer, yielding every budget nodes (or ~1ms) with progress + total-node callbacks. The report is never held in full.
        internal static IEnumerator WriteReportCoroutine(
            object? root,
            StreamWriter writer,
            RuntimeObjectIntrospectionOptions options,
            Action<int>? onProgress = null,
            Action<string?>? onError = null,
            int nodeBudgetPerYield = 120,
            Action<int>? onTotalNodes = null,
            int maxSliceMsPerYield = 1,
            bool measureProgressTotal = false)
        {
            RuntimeObjectTraversalOptions normalized = RuntimeObjectIntrospectionReportBuilder.NormalizeOptions(options);
            int budget = SystemMath.Max(1, nodeBudgetPerYield);

            if (!RuntimeObjectIntrospectionStreamWriter.TryWriteLine(writer, $"--- {normalized.Title} ---", out string? headerWriteError))
            {
                SafeInvokeError(onError, headerWriteError);
                yield break;
            }

            SafeInvokeProgress(onProgress, 0);

            if (root == null)
            {
                _ = RuntimeObjectIntrospectionStreamWriter.TryWriteLine(writer, "Root: unavailable", out _);
                SafeInvokeProgress(onProgress, 100);
                SafeInvokeTotalNodes(onTotalNodes, 0);
                yield break;
            }

            // When requested, count the reachable nodes with the same engine/options before writing so progress reflects the real graph instead of a fixed projection (the dump blacklist makes the real graph a small fraction of the old projection). The count phase maps into the lower half of the bar; the write phase reports 50..100 against the measured total.
            int progressTotal = normalized.ProgressNodeTotal;
            if (measureProgressTotal)
            {
                RuntimeObjectTraversalEngine counter = new(root, normalized, enforceElapsedBudget: true);
                int countedSinceYield = 0;
                int previousCounted = 0;
                while (!counter.IsFinished)
                {
                    _ = counter.ProcessNext();
                    if (counter.TotalProcessedNodes > previousCounted)
                        countedSinceYield += counter.TotalProcessedNodes - previousCounted;
                    previousCounted = counter.TotalProcessedNodes;

                    if (countedSinceYield >= budget)
                    {
                        countedSinceYield = 0;
                        SafeInvokeProgress(onProgress, CountPhaseProgress(counter.TotalProcessedNodes, normalized.MaxTotalNodes));
                        yield return null;
                    }
                }
                progressTotal = SystemMath.Max(1, counter.TotalProcessedNodes);
                SafeInvokeProgress(onProgress, 50);
            }

            RuntimeObjectTraversalEngine engine = new(root, normalized, enforceElapsedBudget: true);
            int maxSliceMs = SystemMath.Max(1, maxSliceMsPerYield);
            Stopwatch sliceStopwatch = Stopwatch.StartNew();

            int processedSinceYield = 0;
            int previousProcessedNodes = 0;
            while (!engine.IsFinished)
            {
                IReadOnlyList<RuntimeObjectTraversalEvent> events = engine.ProcessNext();
                if (!RuntimeObjectIntrospectionStreamWriter.TryWriteTraversalEvents(writer, events, normalized.MaxValueChars, out string? traversalWriteError))
                {
                    SafeInvokeError(onError, traversalWriteError);
                    yield break;
                }

                if (engine.TotalProcessedNodes > previousProcessedNodes)
                    processedSinceYield += engine.TotalProcessedNodes - previousProcessedNodes;
                previousProcessedNodes = engine.TotalProcessedNodes;

                if (processedSinceYield >= budget || sliceStopwatch.ElapsedMilliseconds >= maxSliceMs)
                {
                    processedSinceYield = 0;
                    sliceStopwatch.Restart();
                    int pct = SystemMath.Min(99, (int)(engine.TotalProcessedNodes * 100L / SystemMath.Max(1, progressTotal)));
                    if (measureProgressTotal)
                        pct = 50 + pct / 2;
                    SafeInvokeProgress(onProgress, pct);

                    if (!RuntimeObjectIntrospectionStreamWriter.TryFlush(writer, out string? flushError))
                    {
                        SafeInvokeError(onError, flushError);
                        yield break;
                    }

                    yield return null;
                }
            }

            SafeInvokeProgress(onProgress, 100);
            SafeInvokeTotalNodes(onTotalNodes, engine.TotalProcessedNodes);
        }

        // Rough count-phase progress (0..49) against the node cap, so the bar moves while counting.
        private static int CountPhaseProgress(int counted, int nodeCap)
            => SystemMath.Min(49, (int)(counted * 49L / SystemMath.Max(1, nodeCap)));

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

        private static void SafeInvokeTotalNodes(Action<int>? onTotalNodes, int value)
        {
            try
            {
                onTotalNodes?.Invoke(value);
            }
            catch
            {
            }
        }

        private static void SafeInvokeError(Action<string?>? onError, string? error)
        {
            try
            {
                onError?.Invoke(error);
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
