using System.Diagnostics;
using System.Text;

namespace ClickIt
{
    public partial class ClickIt
    {
        private long _lastAdditionalDebugClipboardCopyMs;
        private string _lastAdditionalDebugClipboardText = string.Empty;

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
            State.PerformanceMonitor?.UpdateFPS();

            if (effective.LazyMode.Value)
            {
                long sectionStart = Stopwatch.GetTimestamp();
                State.LazyModeRenderer?.Render(GameController ?? throw new InvalidOperationException("GameController is null during render"), State);
                State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.LazyMode, GetElapsedMs(sectionStart));
            }

            if (hasDebugRendering)
            {
                int debugTextStartCount = 0;
                bool autoCopyDebugInfo = effective.AutoCopyAdditionalDebugInfoToClipboard.Value;
                if (autoCopyDebugInfo)
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

                if (autoCopyDebugInfo)
                {
                    string[] debugLines = State.DeferredTextQueue?.GetPendingTextSnapshot(debugTextStartCount) ?? [];
                    TryAutoCopyAdditionalDebugInfo(debugLines, effective);
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

            // Flush deferred text rendering to prevent freezes
            // Use no-op logger to prevent recursive logging during render loop
            long textFlushStart = Stopwatch.GetTimestamp();
            State.DeferredTextQueue?.Flush(Graphics, (msg, frame) => { });
            State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.TextFlush, GetElapsedMs(textFlushStart));

            long frameFlushStart = Stopwatch.GetTimestamp();
            State.DeferredFrameQueue?.Flush(Graphics, (msg, frame) => { });
            State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.FrameFlush, GetElapsedMs(frameFlushStart));

            State.PerformanceMonitor?.StopRenderTiming();
        }

        private void TryAutoCopyAdditionalDebugInfo(string[] debugLines, ClickItSettings settings)
        {
            if (debugLines == null || debugLines.Length == 0)
                return;

            if (ShouldApplyOffscreenNoDataAutoCopySkip(settings)
                && ShouldSkipAutoCopyForOffscreenMovementNoData(debugLines))
                return;

            long now = Environment.TickCount64;
            int intervalMs = Math.Max(250, settings.AutoCopyAdditionalDebugInfoIntervalMs.Value);
            if (now - _lastAdditionalDebugClipboardCopyMs < intervalMs)
                return;

            string payload = BuildDebugClipboardPayload(debugLines);
            if (string.IsNullOrWhiteSpace(payload) || string.Equals(payload, _lastAdditionalDebugClipboardText, StringComparison.Ordinal))
                return;

            if (!TrySetClipboardText(payload))
                return;

            _lastAdditionalDebugClipboardCopyMs = now;
            _lastAdditionalDebugClipboardText = payload;
        }

        private static bool ShouldApplyOffscreenNoDataAutoCopySkip(ClickItSettings settings)
        {
            if (settings == null || !settings.DebugShowPathfinding.Value)
                return false;

            return settings.IsOnlyPathfindingDetailedDebugSectionEnabled();
        }

        private static bool ShouldSkipAutoCopyForOffscreenMovementNoData(string[] debugLines)
        {
            for (int i = 0; i < debugLines.Length; i++)
            {
                string line = debugLines[i] ?? string.Empty;
                if (line.IndexOf("Offscreen Movement:", StringComparison.OrdinalIgnoreCase) >= 0
                    && line.IndexOf("<no data>", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
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


