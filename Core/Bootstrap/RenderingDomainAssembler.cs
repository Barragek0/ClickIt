namespace ClickIt.Core.Bootstrap
{
    internal readonly record struct RenderingDomainServices(
        DebugRenderer DebugRenderer,
        StrongboxRenderer StrongboxRenderer,
        LazyModeRenderer LazyModeRenderer,
        ClickHotkeyToggleRenderer ClickHotkeyToggleRenderer,
        InventoryFullWarningRenderer InventoryFullWarningRenderer,
        PathfindingRenderer PathfindingRenderer,
        AltarDisplayRenderer AltarDisplayRenderer);

    internal static class RenderingDomainAssembler
    {
        public static RenderingDomainServices Assemble(ClickIt owner, ClickItSettings settings, GameController gameController, CoreDomainServices core)
        {
            var debugRenderer = new DebugRenderer(owner, core.AltarService, core.AreaService, core.WeightCalculator, core.DeferredTextQueue, core.DeferredFrameQueue);
            var strongboxRenderer = new StrongboxRenderer(settings, core.DeferredFrameQueue);
            var lazyModeRenderer = new LazyModeRenderer(settings, core.DeferredTextQueue, core.InputHandler, core.LabelFilterPort.GetLazyModeBlockerService());
            var clickHotkeyToggleRenderer = new ClickHotkeyToggleRenderer(settings, core.DeferredTextQueue, core.InputHandler);
            var inventoryFullWarningRenderer = new InventoryFullWarningRenderer(
                core.DeferredTextQueue,
                core.AreaService,
                core.LabelFilterPort.GetLatestInventoryDebug,
                (snapshot, now) => owner.GetDebugClipboardService().TryAutoCopyInventoryWarningDebugSnapshot(
                    snapshot,
                    now,
                    core.DeferredTextQueue.GetPendingTextSnapshot(0)));
            var pathfindingRenderer = new PathfindingRenderer(core.PathfindingService);

            var altarDisplayRenderer = new AltarDisplayRenderer(
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

        public static UltimatumRenderer CreateUltimatumRenderer(ClickItSettings settings, ClickService clickAutomationPort, DeferredFrameQueue deferredFrameQueue)
            => new(settings, clickAutomationPort, deferredFrameQueue);
    }
}