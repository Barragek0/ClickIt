
namespace ClickIt
{
    public partial class ClickIt
    {

        private void RenderInternal()
        {
            bool debugMode = Settings.DebugMode;
            bool renderDebug = Settings.RenderDebug;
            bool hasDebugRendering = debugMode && renderDebug;

            int altarCount = State.AltarService?.GetAltarComponents()?.Count ?? 0;
            bool hasAltars = altarCount > 0;

            bool hasLazyModeIndicator = Settings.LazyMode.Value;

            if (!hasDebugRendering && !hasAltars && !hasLazyModeIndicator)
            {
                return; // Skip all timer operations for no-op renders
            }

            // Start timing only when actually rendering
            State.PerformanceMonitor?.StartRenderTiming();
            State.PerformanceMonitor?.UpdateFPS();

            // Render lazy mode indicator if enabled
            if (Settings.LazyMode.Value)
            {
                State.LazyModeRenderer?.Render(GameController ?? throw new InvalidOperationException("GameController is null during render"), State);
            }

            if (hasDebugRendering)
            {
                State.DebugRenderer?.RenderDebugFrames(Settings);
                if (State.DebugRenderer != null && State.PerformanceMonitor != null)
                {
                    State.DebugRenderer.RenderDetailedDebugInfo(Settings, State.PerformanceMonitor);
                }
            }

            if (hasAltars)
            {
                State.AltarDisplayRenderer?.RenderAltarComponents();
            }

            State.StrongboxRenderer?.Render(GameController, State);

            State.PerformanceMonitor?.StopRenderTiming();

            // Flush deferred text rendering to prevent freezes
            // Use no-op logger to prevent recursive logging during render loop
            State.DeferredTextQueue?.Flush(Graphics, (msg, frame) => { });
            State.DeferredFrameQueue?.Flush(Graphics, (msg, frame) => { });
        }





    }
}
