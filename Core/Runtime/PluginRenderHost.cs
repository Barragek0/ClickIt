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

            int altarCount = services.AltarService?.GetAltarComponentCount() ?? 0;
            bool hasAltars = altarCount > 0;

            services.PerformanceMonitor?.StartRenderTiming();
            try
            {
                services.PerformanceMonitor?.UpdateFPS();

                if (effectiveSettings.LazyMode.Value)
                {
                    long sectionStart = Stopwatch.GetTimestamp();
                    rendering.LazyModeRenderer?.Render(gameController ?? throw new InvalidOperationException("GameController is null during render"), state);
                    services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.LazyMode, GetElapsedMs(sectionStart));
                }

                if (effectiveSettings.IsClickHotkeyToggleModeEnabled())
                    rendering.ClickHotkeyToggleRenderer?.Render(gameController ?? throw new InvalidOperationException("GameController is null during render"));


                rendering.InventoryFullWarningRenderer?.Render(gameController ?? throw new InvalidOperationException("GameController is null during render"));

                if (hasDebugRendering)
                {
                    int debugTextStartCount = 0;
                    bool shouldCopyDebugInfo = debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest;
                    if (shouldCopyDebugInfo)
                        debugTextStartCount = rendering.DeferredTextQueue?.GetPendingCount() ?? 0;


                    long sectionStart = Stopwatch.GetTimestamp();
                    rendering.DebugRenderer?.RenderDebugFrames(effectiveSettings);
                    if (rendering.DebugRenderer != null
                        && services.PerformanceMonitor != null
                        && effectiveSettings.IsAnyDetailedDebugSectionEnabled())
                        rendering.DebugRenderer.RenderDetailedDebugInfo(effectiveSettings, services.PerformanceMonitor);


                    if (shouldCopyDebugInfo)
                    {
                        string[] debugLines = rendering.DeferredTextQueue?.GetPendingTextSnapshot(debugTextStartCount) ?? [];
                        debugClipboardService.CompleteAdditionalDebugInfoCopy(debugLines);
                    }

                    services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.DebugOverlay, GetElapsedMs(sectionStart));
                }

                if (hasAltars)
                {
                    long sectionStart = Stopwatch.GetTimestamp();
                    rendering.AltarDisplayRenderer?.RenderAltarComponents();
                    services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.AltarOverlay, GetElapsedMs(sectionStart));
                }

                long ultimatumStart = Stopwatch.GetTimestamp();
                rendering.UltimatumRenderer?.Render();
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.UltimatumOverlay, GetElapsedMs(ultimatumStart));

                long strongboxStart = Stopwatch.GetTimestamp();
                rendering.StrongboxRenderer?.Render(gameController);
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.StrongboxOverlay, GetElapsedMs(strongboxStart));

                long pathfindingStart = Stopwatch.GetTimestamp();
                rendering.PathfindingRenderer?.Render(gameController, graphics, effectiveSettings);
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.PathfindingOverlay, GetElapsedMs(pathfindingStart));
            }
            catch
            {
                rendering.DeferredTextQueue?.ClearPending();
                rendering.DeferredFrameQueue?.ClearPending();
                throw;
            }
            finally
            {
                long textFlushStart = Stopwatch.GetTimestamp();
                rendering.DeferredTextQueue?.Flush(graphics!);
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.TextFlush, GetElapsedMs(textFlushStart));

                long frameFlushStart = Stopwatch.GetTimestamp();
                rendering.DeferredFrameQueue?.Flush(graphics!);
                services.PerformanceMonitor?.RecordRenderSectionTiming(RenderSection.FrameFlush, GetElapsedMs(frameFlushStart));

                services.PerformanceMonitor?.StopRenderTiming();
            }
        }
    }
}