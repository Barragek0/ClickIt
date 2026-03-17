using System.Diagnostics;
using System.Text;

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

        private void TryCopyAdditionalDebugInfo(string[] debugLines)
        {
            if (debugLines == null || debugLines.Length == 0)
                return;

            string payload = BuildDebugClipboardPayload(debugLines);
            if (string.IsNullOrWhiteSpace(payload))
                return;

            _ = TrySetClipboardText(payload);
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


