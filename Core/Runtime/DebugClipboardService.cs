namespace ClickIt.Core.Runtime
{
    internal readonly record struct DebugClipboardServiceDependencies(
        PluginContext State,
        ClickIt Owner,
        Func<GameController?> GetGameController);

    internal sealed class DebugClipboardService(DebugClipboardServiceDependencies dependencies)
    {
        internal GameStateDumpCoordinator GameStateDump { get; } = new(dependencies);

        public bool HasPendingAdditionalDebugInfoCopyRequest { get; private set; }

        public void RequestAdditionalDebugInfoCopy()
        {
            HasPendingAdditionalDebugInfoCopyRequest = true;
        }

        // Render-thread call: clears the request immediately (so a second hotkey press during the copy re-arms properly), then builds the payload + writes the clipboard off-thread because a large pending text queue can block rendering for hundreds of ms. recordCost reports the background work's (bytes, ms) so the debug tables can show the copy under the Dump row.
        public void CompleteAdditionalDebugInfoCopy(string[] debugLines, Action<long, double>? recordCost = null)
        {
            HasPendingAdditionalDebugInfoCopyRequest = false;
            if (debugLines == null || debugLines.Length == 0)
                return;

            string[] lines = debugLines;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                long start = Stopwatch.GetTimestamp();
                long allocStart = GC.GetAllocatedBytesForCurrentThread();
                try
                {
                    TryCopyAdditionalDebugInfo(lines);
                }
                finally
                {
                    recordCost?.Invoke(
                        GC.GetAllocatedBytesForCurrentThread() - allocStart,
                        (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                }
            });
        }

        private static void TryCopyAdditionalDebugInfo(string[] debugLines)
        {
            if (debugLines == null || debugLines.Length == 0)
                return;

            string payload = DebugClipboardPayloadBuilder.BuildDebugClipboardPayload(debugLines);
            if (string.IsNullOrWhiteSpace(payload))
                return;

            _ = ClipboardText.TryCopy(payload);
        }
    }
}
