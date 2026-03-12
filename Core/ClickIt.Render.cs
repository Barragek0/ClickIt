using System.Diagnostics;

namespace ClickIt
{
    public partial class ClickIt
    {
        private static double GetElapsedMs(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        }

        private void RenderInternal()
        {
            // Use EffectiveSettings (test seam) where possible to avoid null-reference
            // when tests inject settings via the test seam without setting the base Settings property.
            var effective = EffectiveSettings;
            bool debugMode = effective.DebugMode;
            bool renderDebug = effective.RenderDebug;
            bool hasDebugRendering = debugMode && renderDebug;

            int altarCount = State.AltarService?.GetAltarComponentCount() ?? 0;
            bool hasAltars = altarCount > 0;

            // Start timing only when actually rendering
            State.PerformanceMonitor?.StartRenderTiming();
            State.PerformanceMonitor?.UpdateFPS();

            // Render lazy mode indicator if enabled
            if (effective.LazyMode.Value)
            {
                long sectionStart = Stopwatch.GetTimestamp();
                State.LazyModeRenderer?.Render(GameController ?? throw new InvalidOperationException("GameController is null during render"), State);
                State.PerformanceMonitor?.RecordRenderSectionTiming(Utils.RenderSection.LazyMode, GetElapsedMs(sectionStart));
            }

            if (hasDebugRendering)
            {
                long sectionStart = Stopwatch.GetTimestamp();
                State.DebugRenderer?.RenderDebugFrames(effective);
                if (State.DebugRenderer != null && State.PerformanceMonitor != null)
                {
                    State.DebugRenderer.RenderDetailedDebugInfo(effective, State.PerformanceMonitor);
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

    }
}


