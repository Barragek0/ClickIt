namespace ClickIt.Core.Runtime
{
    internal readonly record struct DebugClipboardServiceDependencies(
        PluginContext State,
        ClickIt Owner,
        Func<ClickItSettings?> GetEffectiveSettings,
        Func<GameController?> GetGameController);

    internal sealed class DebugClipboardService(DebugClipboardServiceDependencies dependencies)
    {
        private readonly DebugClipboardServiceDependencies _dependencies = dependencies;
        private readonly DeepMemoryDumpCoordinator _deepMemoryDumpCoordinator = new(dependencies);
        private long _lastInventoryWarningAutoCopySuccessTimestampMs;
        private bool _copyAdditionalDebugInfoRequested;

        public bool HasPendingAdditionalDebugInfoCopyRequest => _copyAdditionalDebugInfoRequested;

        public void RequestAdditionalDebugInfoCopy()
        {
            _copyAdditionalDebugInfoRequested = true;
        }

        public void CompleteAdditionalDebugInfoCopy(string[] debugLines)
        {
            try
            {
                TryCopyAdditionalDebugInfo(debugLines);
            }
            finally
            {
                _copyAdditionalDebugInfoRequested = false;
            }
        }

        public void QueueDeepMemoryDumpCoroutine()
        {
            _deepMemoryDumpCoordinator.QueueDeepMemoryDumpCoroutine();
        }

        public bool TryAutoCopyInventoryWarningDebugSnapshot(InventoryDebugSnapshot snapshot, long now, string[] debugLines)
        {
            ClickItSettings? settings = _dependencies.GetEffectiveSettings();
            if (settings?.AutoCopyInventoryWarningDebug?.Value != true)
                return false;

            string payload = DebugClipboardPayloadBuilder.BuildInventoryWarningClipboardPayload(
                snapshot,
                now,
                _lastInventoryWarningAutoCopySuccessTimestampMs,
                debugLines);
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            bool copied = TrySetClipboardText(payload);
            if (copied)
                _lastInventoryWarningAutoCopySuccessTimestampMs = now;

            return copied;
        }

        private void TryCopyAdditionalDebugInfo(string[] debugLines)
        {
            if (debugLines == null || debugLines.Length == 0)
                return;

            string payload = DebugClipboardPayloadBuilder.BuildDebugClipboardPayload(debugLines);

            QueueDeepMemoryDumpCoroutine();

            string status = _deepMemoryDumpCoordinator.GetDeepMemoryDumpStatusMessage();
            if (!string.IsNullOrWhiteSpace(status))
                payload = payload + Environment.NewLine + Environment.NewLine + status;

            if (string.IsNullOrWhiteSpace(payload))
                return;

            _ = TrySetClipboardText(payload);
        }

        private static bool TrySetClipboardText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                {
                    Clipboard.SetText(text);
                    return true;
                }

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "clip.exe",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };

                if (!process.Start())
                    return false;

                process.StandardInput.Write(text);
                process.StandardInput.Close();

                if (!process.WaitForExit(500))
                {
                    try { process.Kill(); } catch { }
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}