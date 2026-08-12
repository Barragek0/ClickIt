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

                // The "Copy Additional Debug Info" button must be consumed regardless of whether the
                // debug overlays are rendering — otherwise the pending request latches forever and a
                // later debug-mode toggle flushes a stale backlog. With no debug rendering the queue
                // is empty and the service clears the flag without copying.
                bool shouldCopyDebugInfo = debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest;
                if (shouldCopyDebugInfo)
                {
                    int debugTextStartCount = rendering.DeferredTextQueue?.GetPendingCount() ?? 0;
                    string[] debugLines = rendering.DeferredTextQueue?.GetPendingTextSnapshot(debugTextStartCount) ?? [];
                    // The copy service builds the payload + writes the clipboard off-thread; the
                    // recorder surfaces that background cost under the Dump processing section.
                    PerformanceMonitor? perf = services.PerformanceMonitor;
                    debugClipboardService.CompleteAdditionalDebugInfoCopy(debugLines,
                        (bytes, ms) =>
                        {
                            perf?.RecordProcessingTiming(ProcessingSection.GameStateDump, ms);
                            perf?.RecordAllocation(ProcessingSection.GameStateDump, bytes);
                        });
                }

                if (rendering.DeferredTextQueue != null && rendering.DeferredFrameQueue != null && rendering.DeferredDrawQueue != null)
                {
                    OverlayRenderContext overlayContext = new(
                        effectiveSettings,
                        gameController,
                        graphics,
                        gameController?.Window?.GetWindowRectangleTimeCache ?? RectangleF.Empty,
                        state.Services.CachedLabels?.Value,
                        rendering.DeferredTextQueue,
                        rendering.DeferredFrameQueue,
                        rendering.DeferredDrawQueue);
                    rendering.OverlayRenderHost?.Render(overlayContext, services.PerformanceMonitor);
                }

                long debugStart = Stopwatch.GetTimestamp();
                rendering.ImGuiDebugOverlay?.Draw();
                PerformanceSettingsPanelRenderer.DrawStartupSetupFlow();
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.DebugOverlay, GetElapsedMs(debugStart));

                long uiRectStart = Stopwatch.GetTimestamp();
                rendering.UiRegionRectangleOverlay?.Render();
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.UiRegionRectangle, GetElapsedMs(uiRectStart));
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
                PerformanceMonitor? pm = services.PerformanceMonitor;

                // Flush attribution: every deferred item is stamped with the overlay section that
                // enqueued it, so each feature's render row includes its own actual draw cost. Only
                // items enqueued outside the overlay host (section Unknown) fall back to the
                // aggregate TextFlush/FrameFlush rows, keeping the render-total sum non-redundant.
                double textUnknownMs = 0;
                rendering.DeferredTextQueue?.Flush(graphics!, (section, ms) =>
                {
                    if (section == RenderSection.Unknown)
                        textUnknownMs += ms;
                    else
                        pm?.AccumulateRenderSectionFlush(section, ms);
                });
                pm?.RecordRenderSectionTiming(RenderSection.TextFlush, textUnknownMs);

                double frameUnknownMs = 0;
                rendering.DeferredFrameQueue?.Flush(graphics!, (section, ms) =>
                {
                    if (section == RenderSection.Unknown)
                        frameUnknownMs += ms;
                    else
                        pm?.AccumulateRenderSectionFlush(section, ms);
                });
                rendering.DeferredDrawQueue?.Flush(graphics!, (section, ms) =>
                {
                    if (section == RenderSection.Unknown)
                        frameUnknownMs += ms;
                    else
                        pm?.AccumulateRenderSectionFlush(section, ms);
                });
                pm?.RecordRenderSectionTiming(RenderSection.FrameFlush, frameUnknownMs);

                // Combine this frame's per-section enqueue + flush into one sample per section.
                pm?.CompleteRenderSectionFrame();

                pm?.StopRenderTiming();
            }
        }
    }
}