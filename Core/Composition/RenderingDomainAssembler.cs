using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;

namespace ClickIt.Composition
{
    internal readonly record struct RenderingDomainServices(
        Rendering.DebugRenderer DebugRenderer,
        Rendering.StrongboxRenderer StrongboxRenderer,
        Rendering.LazyModeRenderer LazyModeRenderer,
        Rendering.ClickHotkeyToggleRenderer ClickHotkeyToggleRenderer,
        Rendering.InventoryFullWarningRenderer InventoryFullWarningRenderer,
        Rendering.PathfindingRenderer PathfindingRenderer,
        Rendering.AltarDisplayRenderer AltarDisplayRenderer);

    internal static class RenderingDomainAssembler
    {
        public static RenderingDomainServices Assemble(ClickIt owner, ClickItSettings settings, GameController gameController, CoreDomainServices core)
        {
            var debugRenderer = new Rendering.DebugRenderer(owner, core.AltarService, core.AreaService, core.WeightCalculator, core.DeferredTextQueue, core.DeferredFrameQueue);
            var strongboxRenderer = new Rendering.StrongboxRenderer(settings, core.DeferredFrameQueue);
            var lazyModeRenderer = new Rendering.LazyModeRenderer(settings, core.DeferredTextQueue, core.InputHandler, core.LabelFilterService);
            var clickHotkeyToggleRenderer = new Rendering.ClickHotkeyToggleRenderer(settings, core.DeferredTextQueue, core.InputHandler);
            var inventoryFullWarningRenderer = new Rendering.InventoryFullWarningRenderer(
                core.DeferredTextQueue,
                core.AreaService,
                core.LabelFilterService.GetLatestInventoryDebug,
                owner.TryAutoCopyInventoryWarningDebugSnapshotForLifecycle);
            var pathfindingRenderer = new Rendering.PathfindingRenderer(core.PathfindingService);

            var altarDisplayRenderer = new Rendering.AltarDisplayRenderer(
                owner.Graphics,
                settings,
                gameController,
                core.WeightCalculator,
                core.DeferredTextQueue,
                core.DeferredFrameQueue,
                core.AltarService,
                owner.LogMessage);

            return new RenderingDomainServices(
                debugRenderer,
                strongboxRenderer,
                lazyModeRenderer,
                clickHotkeyToggleRenderer,
                inventoryFullWarningRenderer,
                pathfindingRenderer,
                altarDisplayRenderer);
        }

        public static Rendering.UltimatumRenderer CreateUltimatumRenderer(ClickItSettings settings, ClickService clickService, DeferredFrameQueue deferredFrameQueue)
            => new(settings, clickService, deferredFrameQueue);
    }
}
