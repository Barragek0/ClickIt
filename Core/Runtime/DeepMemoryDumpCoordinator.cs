namespace ClickIt.Core.Runtime
{
    internal sealed class DeepMemoryDumpCoordinator(DebugClipboardServiceDependencies dependencies)
    {
        private const IntrospectionProfile ActiveMemoryDumpProfile = IntrospectionProfile.Default;
        private const int DeepMemoryDumpNodeBudgetPerYield = 8;
        private static readonly bool EnableDeepMemoryDumpOnCopyAdditionalDebugInfo = false;

        private readonly DebugClipboardServiceDependencies _dependencies = dependencies;
        private readonly Lock _stateLock = new();
        private bool _deepMemoryDumpInProgress;
        private string? _lastDeepMemoryDumpPath;
        private string? _lastDeepMemoryDumpError;
        private string _activeMemoryDumpFileName = RuntimeObjectIntrospection.GetFileNameForProfile(ActiveMemoryDumpProfile);

        public void QueueDeepMemoryDumpCoroutine()
        {
            if (!EnableDeepMemoryDumpOnCopyAdditionalDebugInfo)
                return;

            lock (_stateLock)
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

                _ = ExileCoreApi.ParallelRunner.Run(runtime.DeepMemoryDumpCoroutine);
            }
        }

        public string GetDeepMemoryDumpStatusMessage()
        {
            if (!EnableDeepMemoryDumpOnCopyAdditionalDebugInfo)
                return string.Empty;

            lock (_stateLock)
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

        private void OnDeepMemoryDumpProgress(int progressPercent)
        {
            SetMemoryDumpUiState(
                inProgress: true,
                progressPercent: SystemMath.Clamp(progressPercent, 0, 100),
                succeeded: false,
                statusText: $"Writing {_activeMemoryDumpFileName}...",
                outputPath: null);
        }

        private void OnDeepMemoryDumpCompleted(string? dumpedPath, string? error)
        {
            lock (_stateLock)
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
            settings.MemoryDumpProgressPercent = SystemMath.Clamp(progressPercent, 0, 100);
            settings.MemoryDumpLastRunSucceeded = succeeded;
            settings.MemoryDumpStatusText = statusText ?? string.Empty;
            settings.MemoryDumpOutputPath = outputPath ?? string.Empty;
        }
    }
}