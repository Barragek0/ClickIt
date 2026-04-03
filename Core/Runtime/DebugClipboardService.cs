using System.Collections;
using System.Diagnostics;
using System.Text;

namespace ClickIt.Core.Runtime
{
    internal readonly record struct DebugClipboardServiceDependencies(
        PluginContext State,
        ClickIt Owner,
        Func<ClickItSettings?> GetEffectiveSettings,
        Func<GameController?> GetGameController);

    internal sealed class DebugClipboardService(DebugClipboardServiceDependencies dependencies)
    {
        private const IntrospectionProfile ActiveMemoryDumpProfile = IntrospectionProfile.Default;
        private const int DeepMemoryDumpNodeBudgetPerYield = 8;
        private static readonly bool EnableDeepMemoryDumpOnCopyAdditionalDebugInfo = false;
        private static readonly object DeepMemoryDumpStateLock = new();

        private readonly DebugClipboardServiceDependencies _dependencies = dependencies;
        private long _lastInventoryWarningAutoCopySuccessTimestampMs;
        private bool _copyAdditionalDebugInfoRequested;
        private bool _deepMemoryDumpInProgress;
        private string? _lastDeepMemoryDumpPath;
        private string? _lastDeepMemoryDumpError;
        private string _activeMemoryDumpFileName = RuntimeObjectIntrospection.GetFileNameForProfile(ActiveMemoryDumpProfile);

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
            if (!EnableDeepMemoryDumpOnCopyAdditionalDebugInfo)
                return;

            lock (DeepMemoryDumpStateLock)
            {
                PluginRuntimeState runtime = _dependencies.State.Runtime;
                if (runtime.IsShuttingDown)
                    return;

                if (runtime.DeepMemoryDumpCoroutine != null && !runtime.DeepMemoryDumpCoroutine.IsDone)
                    return;

                _deepMemoryDumpInProgress = true;
                _lastDeepMemoryDumpError = null;
                _activeMemoryDumpFileName = RuntimeObjectIntrospection.GetFileNameForProfile(ActiveMemoryDumpProfile);
                SetMemoryDumpUiState(inProgress: true, progressPercent: 0, succeeded: false, statusText: $"Writing {_activeMemoryDumpFileName}...", outputPath: null);

                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Plugins",
                    "Compiled",
                    "ClickIt",
                    _activeMemoryDumpFileName);

                IEnumerator dumpEnumerator = RuntimeObjectIntrospection.WriteMemorySnapshotCoroutine(
                    _dependencies.GetGameController(),
                    path,
                    ActiveMemoryDumpProfile,
                    OnDeepMemoryDumpCompleted,
                    OnDeepMemoryDumpProgress,
                    DeepMemoryDumpNodeBudgetPerYield);

                runtime.DeepMemoryDumpCoroutine = new Coroutine(
                    dumpEnumerator,
                    _dependencies.Owner,
                    "ClickIt.DeepMemoryDump",
                    true)
                {
                    Priority = CoroutinePriority.Normal
                };

                _ = global::ExileCore.Core.ParallelRunner.Run(runtime.DeepMemoryDumpCoroutine);
            }
        }

        public bool TryAutoCopyInventoryWarningDebugSnapshot(InventoryDebugSnapshot snapshot, long now, string[] debugLines)
        {
            ClickItSettings? settings = _dependencies.GetEffectiveSettings();
            if (settings?.AutoCopyInventoryWarningDebug?.Value != true)
                return false;

            string payload = BuildInventoryWarningClipboardPayload(snapshot, now, debugLines);
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

            string payload = BuildDebugClipboardPayload(debugLines);

            if (EnableDeepMemoryDumpOnCopyAdditionalDebugInfo)
            {
                QueueDeepMemoryDumpCoroutine();

                string status = GetDeepMemoryDumpStatusMessage();
                if (!string.IsNullOrWhiteSpace(status))
                    payload = payload + Environment.NewLine + Environment.NewLine + status;
            }

            if (string.IsNullOrWhiteSpace(payload))
                return;

            _ = TrySetClipboardText(payload);
        }

        private void OnDeepMemoryDumpProgress(int progressPercent)
        {
            SetMemoryDumpUiState(
                inProgress: true,
                progressPercent: Math.Clamp(progressPercent, 0, 100),
                succeeded: false,
                statusText: $"Writing {_activeMemoryDumpFileName}...",
                outputPath: null);
        }

        private void OnDeepMemoryDumpCompleted(string? dumpedPath, string? error)
        {
            lock (DeepMemoryDumpStateLock)
            {
                _deepMemoryDumpInProgress = false;
                _lastDeepMemoryDumpPath = dumpedPath;
                _lastDeepMemoryDumpError = error;

                bool success = string.IsNullOrWhiteSpace(error) && !string.IsNullOrWhiteSpace(dumpedPath);
                string statusText = success
                    ? _activeMemoryDumpFileName + " written successfully."
                    : "Memory dump failed: " + (error ?? "unknown error");

                SetMemoryDumpUiState(
                    inProgress: false,
                    progressPercent: success ? 100 : 0,
                    succeeded: success,
                    statusText: statusText,
                    outputPath: dumpedPath);
            }
        }

        private void SetMemoryDumpUiState(bool inProgress, int progressPercent, bool succeeded, string statusText, string? outputPath)
        {
            ClickItSettings? settings = _dependencies.GetEffectiveSettings();
            if (settings == null)
                return;

            settings.MemoryDumpInProgress = inProgress;
            settings.MemoryDumpProgressPercent = Math.Clamp(progressPercent, 0, 100);
            settings.MemoryDumpLastRunSucceeded = succeeded;
            settings.MemoryDumpStatusText = statusText ?? string.Empty;
            settings.MemoryDumpOutputPath = outputPath ?? string.Empty;
        }

        private string GetDeepMemoryDumpStatusMessage()
        {
            if (!EnableDeepMemoryDumpOnCopyAdditionalDebugInfo)
                return string.Empty;

            lock (DeepMemoryDumpStateLock)
            {
                bool isRunning = _deepMemoryDumpInProgress
                    || (_dependencies.State.Runtime.DeepMemoryDumpCoroutine != null && !_dependencies.State.Runtime.DeepMemoryDumpCoroutine.IsDone);
                if (isRunning)
                    return $"Runtime memory dump: in progress (coroutine, node budget {DeepMemoryDumpNodeBudgetPerYield}/slice).";

                if (!string.IsNullOrWhiteSpace(_lastDeepMemoryDumpPath))
                    return "Runtime memory dump written to:"
                        + Environment.NewLine
                        + _lastDeepMemoryDumpPath;

                if (!string.IsNullOrWhiteSpace(_lastDeepMemoryDumpError))
                    return "Runtime memory dump failed: " + _lastDeepMemoryDumpError;

                return "Runtime memory dump queued as coroutine ("
                    + _activeMemoryDumpFileName
                    + "). Available filenames by profile: memory.dat, structure.dat, full.dat.";
            }
        }

        internal static string BuildDebugClipboardPayload(string[] lines)
        {
            var sb = new StringBuilder(lines.Length * 32);
            sb.AppendLine("=== ClickIt Additional Debug Information ===");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    sb.AppendLine(lines[i]);
            }

            return sb.ToString().TrimEnd();
        }

        private string BuildInventoryWarningClipboardPayload(InventoryDebugSnapshot snapshot, long now, string[] debugLines)
        {
            string payload = BuildDebugClipboardPayload(debugLines);

            var sb = new StringBuilder(payload.Length + 512);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                sb.AppendLine(payload);
                sb.AppendLine();
            }

            sb.AppendLine("=== Inventory Warning Trigger Snapshot ===");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.AppendLine($"NowMs: {now}");
            sb.AppendLine($"LastAutoCopySuccessMs: {_lastInventoryWarningAutoCopySuccessTimestampMs}");
            sb.AppendLine($"Sequence: {snapshot.Sequence}");
            sb.AppendLine($"TimestampMs: {snapshot.TimestampMs}");
            sb.AppendLine($"Stage: {snapshot.Stage}");
            sb.AppendLine($"DecisionAllowPickup: {snapshot.DecisionAllowPickup}");
            sb.AppendLine($"InventoryFull: {snapshot.InventoryFull}");
            sb.AppendLine($"InventoryFullSource: {snapshot.InventoryFullSource}");
            sb.AppendLine($"HasPrimaryInventory: {snapshot.HasPrimaryInventory}");
            sb.AppendLine($"UsedFullFlag: {snapshot.UsedFullFlag}");
            sb.AppendLine($"FullFlagValue: {snapshot.FullFlagValue}");
            sb.AppendLine($"UsedCellOccupancy: {snapshot.UsedCellOccupancy}");
            sb.AppendLine($"CapacityCells: {snapshot.CapacityCells}");
            sb.AppendLine($"OccupiedCells: {snapshot.OccupiedCells}");
            sb.AppendLine($"InventoryEntityCount: {snapshot.InventoryEntityCount}");
            sb.AppendLine($"LayoutEntryCount: {snapshot.LayoutEntryCount}");
            sb.AppendLine($"GroundItemName: {snapshot.GroundItemName}");
            sb.AppendLine($"GroundItemPath: {snapshot.GroundItemPath}");
            sb.AppendLine($"IsGroundStackable: {snapshot.IsGroundStackable}");
            sb.AppendLine($"MatchingPathCount: {snapshot.MatchingPathCount}");
            sb.AppendLine($"PartialMatchingStackCount: {snapshot.PartialMatchingStackCount}");
            sb.AppendLine($"HasPartialMatchingStack: {snapshot.HasPartialMatchingStack}");
            sb.AppendLine($"Notes: {snapshot.Notes}");

            return sb.ToString().TrimEnd();
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