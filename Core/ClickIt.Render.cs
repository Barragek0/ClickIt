
namespace ClickIt
{
    public partial class ClickIt
    {

        private void RenderInternal()
        {
            // Use EffectiveSettings (test seam) where possible to avoid null-reference
            // when tests inject settings via the test seam without setting the base Settings property.
            var effective = EffectiveSettings;
            bool debugMode = effective.DebugMode;
            bool renderDebug = effective.RenderDebug;
            bool hasDebugRendering = debugMode && renderDebug;

            int altarCount = State.AltarService?.GetAltarComponents()?.Count ?? 0;
            bool hasAltars = altarCount > 0;


            // Start timing only when actually rendering
            State.PerformanceMonitor?.StartRenderTiming();
            State.PerformanceMonitor?.UpdateFPS();

            // Render lazy mode indicator if enabled
            if (effective.LazyMode.Value)
            {
                State.LazyModeRenderer?.Render(GameController ?? throw new InvalidOperationException("GameController is null during render"), State);
            }

            if (hasDebugRendering)
            {
                State.DebugRenderer?.RenderDebugFrames(effective);
                if (State.DebugRenderer != null && State.PerformanceMonitor != null)
                {
                    State.DebugRenderer.RenderDetailedDebugInfo(effective, State.PerformanceMonitor);
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
