namespace ClickIt.Core.Runtime
{
    internal sealed class PluginRenderHost
    {
        private static double GetElapsedMs(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        internal static void Render(
            PluginContext state,
            ClickItSettings effectiveSettings,
            GameController? gameController,
            Graphics? graphics,
            DebugClipboardService debugClipboardService)
        {
            bool debugMode = effectiveSettings.DebugMode;
            bool renderDebug = effectiveSettings.RenderDebug;
            bool hasDebugRendering = debugMode && renderDebug;
            PluginServices services = state.Services;
            PluginRenderingState rendering = state.Rendering;

            services.PerformanceMonitor?.StartRenderTiming();
            try
            {
                services.PerformanceMonitor?.UpdateFPS();

                if (hasDebugRendering)
                {
                    int debugTextStartCount = 0;
                    bool shouldCopyDebugInfo = debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest;
                    if (shouldCopyDebugInfo)
                        debugTextStartCount = rendering.DeferredTextQueue?.GetPendingCount() ?? 0;

                    if (shouldCopyDebugInfo)
                    {
                        string[] debugLines = rendering.DeferredTextQueue?.GetPendingTextSnapshot(debugTextStartCount) ?? [];
                        debugClipboardService.CompleteAdditionalDebugInfoCopy(debugLines);
                    }
                }

                if (rendering.DeferredTextQueue != null && rendering.DeferredFrameQueue != null && rendering.DeferredDrawQueue != null)
                {
                    OverlayRenderContext overlayContext = new(
                        effectiveSettings,
                        gameController,
                        graphics,
                        gameController.Window.GetWindowRectangleTimeCache,
                        state.Services.CachedLabels?.Value,
                        rendering.DeferredTextQueue,
                        rendering.DeferredFrameQueue,
                        rendering.DeferredDrawQueue);
                    rendering.OverlayRenderHost?.Render(overlayContext, services.PerformanceMonitor);
                }

                long debugStart = Stopwatch.GetTimestamp();
                rendering.ImGuiDebugOverlay?.Draw();
                rendering.UiRegionRectangleOverlay?.Render();
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.DebugOverlay, GetElapsedMs(debugStart));
            }
            catch
            {
                rendering.DeferredTextQueue?.ClearPending();
                rendering.DeferredFrameQueue?.ClearPending();
                rendering.DeferredDrawQueue?.ClearPending();
                throw;
            }
            finally
            {
                long textFlushStart = Stopwatch.GetTimestamp();
                rendering.DeferredTextQueue?.Flush(graphics!);
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.TextFlush, GetElapsedMs(textFlushStart));

                long frameFlushStart = Stopwatch.GetTimestamp();
                rendering.DeferredFrameQueue?.Flush(graphics!);
                rendering.DeferredDrawQueue?.Flush(graphics!);
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.FrameFlush, GetElapsedMs(frameFlushStart));

                services.PerformanceMonitor?.StopRenderTiming();
            }
        }
    }
}