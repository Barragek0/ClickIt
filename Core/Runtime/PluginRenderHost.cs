namespace ClickIt.Core.Runtime
{
    internal sealed class PluginRenderHost
    {
        private static double GetElapsedMs(long startTimestamp)
            => StopwatchMath.ElapsedMs(startTimestamp);

        internal static void Render(
            PluginContext state,
            ClickItSettings effectiveSettings,
            GameController? gameController,
            Graphics? graphics,
            DebugClipboardService debugClipboardService,
            Action drawStartupSetupFlow)
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

                // Consume pending copy requests even when debug overlays are off so the pending flag is always cleared.
                bool shouldCopyDebugInfo = debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest;
                if (shouldCopyDebugInfo)
                {
                    int debugTextStartCount = rendering.DeferredDrawQueue?.GetPendingCount() ?? 0;
                    string[] debugLines = rendering.DeferredDrawQueue?.GetPendingTextSnapshot(debugTextStartCount) ?? [];
                    // The copy service builds the payload + writes the clipboard off-thread; the recorder surfaces that background cost under the Dump processing section.
                    PerformanceMonitor? perf = services.PerformanceMonitor;
                    debugClipboardService.CompleteAdditionalDebugInfoCopy(debugLines,
                        (bytes, ms) =>
                        {
                            perf?.RecordProcessingTiming(ProcessingSection.GameStateDump, ms);
                            perf?.RecordAllocation(ProcessingSection.GameStateDump, bytes);
                        });
                }

                if (rendering.DeferredDrawQueue != null)
                {
                    OverlayRenderContext overlayContext = new(
                        effectiveSettings,
                        gameController,
                        graphics,
                        gameController?.Window?.GetWindowRectangleTimeCache ?? RectangleF.Empty,
                        state.Services.CachedLabels?.Value,
                        rendering.DeferredDrawQueue);
                    rendering.OverlayRenderHost?.Render(overlayContext, services.PerformanceMonitor);
                }

                long debugStart = Stopwatch.GetTimestamp();
                rendering.ImGuiDebugOverlay?.Draw();
                drawStartupSetupFlow();
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.DebugOverlay, GetElapsedMs(debugStart));

                long uiRectStart = Stopwatch.GetTimestamp();
                rendering.UiRegionRectangleOverlay?.Render();
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.UiRegionRectangle, GetElapsedMs(uiRectStart));
            }
            catch
            {
                rendering.DeferredDrawQueue?.ClearPending();
                throw;
            }
            finally
            {
                PerformanceMonitor? pm = services.PerformanceMonitor;

                // Flush attribution: every deferred item is stamped with the overlay section that enqueued it, so each feature's render row includes its own actual draw cost. Only items enqueued outside the overlay host (section Unknown) fall back to the aggregate FrameFlush row, keeping the render-total sum non-redundant.
                double unknownFlushMs = 0;
                rendering.DeferredDrawQueue?.Flush(graphics!, (section, ms) =>
                {
                    if (section == RenderSection.Unknown)
                        unknownFlushMs += ms;
                    else
                        pm?.AccumulateRenderSectionFlush(section, ms);
                });
                pm?.RecordRenderSectionTiming(RenderSection.FrameFlush, unknownFlushMs);

                // Combine this frame's per-section enqueue + flush into one sample per section.
                pm?.CompleteRenderSectionFrame();

                pm?.StopRenderTiming();
            }
        }
    }
}
