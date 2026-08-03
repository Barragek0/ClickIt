namespace ClickIt.Core.Runtime
{
    internal readonly record struct DebugClipboardServiceDependencies(
        PluginContext State,
        ClickIt Owner,
        Func<ClickItSettings?> GetEffectiveSettings,
        Func<GameController?> GetGameController);

    internal sealed class DebugClipboardService(DebugClipboardServiceDependencies dependencies)
    {
        private readonly DeepMemoryDumpCoordinator _deepMemoryDumpCoordinator = new(dependencies);

        public bool HasPendingAdditionalDebugInfoCopyRequest { get; private set; }

        public void RequestAdditionalDebugInfoCopy()
        {
            HasPendingAdditionalDebugInfoCopyRequest = true;
        }

        public void CompleteAdditionalDebugInfoCopy(string[] debugLines)
        {
            try
            {
                TryCopyAdditionalDebugInfo(debugLines);
            }
            finally
            {
                HasPendingAdditionalDebugInfoCopyRequest = false;
            }
        }

        public void QueueDeepMemoryDumpCoroutine()
        {
            _deepMemoryDumpCoordinator.QueueDeepMemoryDumpCoroutine();
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

                using Process process = new();
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