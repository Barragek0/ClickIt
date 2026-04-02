using System.Diagnostics;
using System.IO;
using System.Collections;
using System.Text;
using System.Threading;
using ClickIt.Services;
using ClickIt.Services.Label.Inventory;
using ClickIt.Utils;
using ExileCore;
using ExileCore.Shared;
using ExileCore.Shared.Enums;

namespace ClickIt
{
    public partial class ClickIt
    {
        private bool _copyAdditionalDebugInfoRequested;

        private static double GetElapsedMs(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        }

        private void RenderInternal()
        {
            // Use EffectiveSettings (test seam) where possible to avoid null-reference
            var effective = EffectiveSettings;
            bool debugMode = effective.DebugMode;
            bool renderDebug = effective.RenderDebug;
            bool hasDebugRendering = debugMode && renderDebug;

            int altarCount = State.AltarService?.GetAltarComponentCount() ?? 0;
            bool hasAltars = altarCount > 0;

            State.PerformanceMonitor?.StartRenderTiming();
            try
            {
                State.PerformanceMonitor?.UpdateFPS();

                if (effective.LazyMode.Value)
                {
                    long sectionStart = Stopwatch.GetTimestamp();
                    State.LazyModeRenderer?.Render(GameController ?? throw new InvalidOperationException("GameController is null during render"), State);
                    State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.LazyMode, GetElapsedMs(sectionStart));
                }

                if (effective.IsClickHotkeyToggleModeEnabled())
                {
                    State.ClickHotkeyToggleRenderer?.Render(GameController ?? throw new InvalidOperationException("GameController is null during render"));
                }

                State.InventoryFullWarningRenderer?.Render(GameController ?? throw new InvalidOperationException("GameController is null during render"));

                if (hasDebugRendering)
                {
                    int debugTextStartCount = 0;
                    bool shouldCopyDebugInfo = _copyAdditionalDebugInfoRequested;
                    if (shouldCopyDebugInfo)
                    {
                        debugTextStartCount = State.DeferredTextQueue?.GetPendingCount() ?? 0;
                    }

                    long sectionStart = Stopwatch.GetTimestamp();
                    State.DebugRenderer?.RenderDebugFrames(effective);
                    if (State.DebugRenderer != null
                        && State.PerformanceMonitor != null
                        && effective.IsAnyDetailedDebugSectionEnabled())
                    {
                        State.DebugRenderer.RenderDetailedDebugInfo(effective, State.PerformanceMonitor);
                    }

                    if (shouldCopyDebugInfo)
                    {
                        string[] debugLines = State.DeferredTextQueue?.GetPendingTextSnapshot(debugTextStartCount) ?? [];
                        TryCopyAdditionalDebugInfo(debugLines);
                        _copyAdditionalDebugInfoRequested = false;
                    }

                    State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.DebugOverlay, GetElapsedMs(sectionStart));
                }

                if (hasAltars)
                {
                    long sectionStart = Stopwatch.GetTimestamp();
                    State.AltarDisplayRenderer?.RenderAltarComponents();
                    State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.AltarOverlay, GetElapsedMs(sectionStart));
                }

                long ultimatumStart = Stopwatch.GetTimestamp();
                State.UltimatumRenderer?.Render();
                State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.UltimatumOverlay, GetElapsedMs(ultimatumStart));

                long strongboxStart = Stopwatch.GetTimestamp();
                State.StrongboxRenderer?.Render(GameController, State);
                State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.StrongboxOverlay, GetElapsedMs(strongboxStart));

                long pathfindingStart = Stopwatch.GetTimestamp();
                State.PathfindingRenderer?.Render(GameController, Graphics, effective);
                State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.PathfindingOverlay, GetElapsedMs(pathfindingStart));
            }
            catch
            {
                State.DeferredTextQueue?.ClearPending();
                State.DeferredFrameQueue?.ClearPending();
                throw;
            }
            finally
            {
                // Flush deferred rendering in finally so a section exception cannot leave buffered entries growing frame-over-frame.
                long textFlushStart = Stopwatch.GetTimestamp();
                State.DeferredTextQueue?.Flush(Graphics, (msg, frame) => { });
                State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.TextFlush, GetElapsedMs(textFlushStart));

                long frameFlushStart = Stopwatch.GetTimestamp();
                State.DeferredFrameQueue?.Flush(Graphics, (msg, frame) => { });
                State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.FrameFlush, GetElapsedMs(frameFlushStart));

                State.PerformanceMonitor?.StopRenderTiming();
            }
        }

        private const IntrospectionProfile ActiveMemoryDumpProfile = IntrospectionProfile.Default;
        private const int DeepMemoryDumpNodeBudgetPerYield = 8;
        private static readonly bool EnableDeepMemoryDumpOnCopyAdditionalDebugInfo = false;
        private static readonly object DeepMemoryDumpStateLock = new();
        private long _lastInventoryWarningAutoCopySuccessTimestampMs;
        private bool _deepMemoryDumpInProgress;
        private string? _lastDeepMemoryDumpPath;
        private string? _lastDeepMemoryDumpError;
        private string _activeMemoryDumpFileName = RuntimeObjectIntrospection.GetFileNameForProfile(ActiveMemoryDumpProfile);

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

        private void QueueDeepMemoryDumpCoroutine()
        {
            if (!EnableDeepMemoryDumpOnCopyAdditionalDebugInfo)
                return;

            lock (DeepMemoryDumpStateLock)
            {
                if (State.IsShuttingDown)
                    return;

                if (State.DeepMemoryDumpCoroutine != null && !State.DeepMemoryDumpCoroutine.IsDone)
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
                    GameController,
                    path,
                    ActiveMemoryDumpProfile,
                    OnDeepMemoryDumpCompleted,
                    OnDeepMemoryDumpProgress,
                    DeepMemoryDumpNodeBudgetPerYield);

                State.DeepMemoryDumpCoroutine = new Coroutine(
                    dumpEnumerator,
                    this,
                    "ClickIt.DeepMemoryDump",
                    true)
                {
                    Priority = CoroutinePriority.Normal
                };

                _ = global::ExileCore.Core.ParallelRunner.Run(State.DeepMemoryDumpCoroutine);
                State.DeepMemoryDumpCoroutine.Resume();
            }
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
            ClickItSettings? settings = EffectiveSettings;
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
                    || (State.DeepMemoryDumpCoroutine != null && !State.DeepMemoryDumpCoroutine.IsDone);
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

        private static string BuildDebugClipboardPayload(string[] lines)
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

        private bool TryAutoCopyInventoryWarningDebugSnapshot(InventoryDebugSnapshot snapshot, long now)
        {
            ClickItSettings? settings = EffectiveSettings;
            if (settings?.AutoCopyInventoryWarningDebug?.Value != true)
                return false;

            string payload = BuildInventoryWarningClipboardPayload(snapshot, now);
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            bool copied = TrySetClipboardText(payload);
            if (copied)
                _lastInventoryWarningAutoCopySuccessTimestampMs = now;

            return copied;
        }

        internal bool TryAutoCopyInventoryWarningDebugSnapshotForLifecycle(InventoryDebugSnapshot snapshot, long now)
        {
            return TryAutoCopyInventoryWarningDebugSnapshot(snapshot, now);
        }

        private string BuildInventoryWarningClipboardPayload(InventoryDebugSnapshot snapshot, long now)
        {
            string[] debugLines = State.DeferredTextQueue?.GetPendingTextSnapshot(0) ?? [];
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

        private bool TrySetClipboardText(string text)
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


