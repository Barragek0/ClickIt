namespace ClickIt.Core.Bootstrap
{
    internal readonly record struct RenderingDomainServices(
        DebugRenderer DebugRenderer,
        StrongboxRenderer StrongboxRenderer,
        LazyModeRenderer LazyModeRenderer,
        ClickHotkeyToggleRenderer ClickHotkeyToggleRenderer,
        InventoryFullWarningRenderer InventoryFullWarningRenderer,
        PathfindingRenderer PathfindingRenderer,
        AltarChoiceEvaluator AltarChoiceEvaluator,
        AltarDisplayRenderer AltarDisplayRenderer);

    internal static class RenderingDomainAssembler
    {
        /**
        Keep this thin runtime entry wrapper so the production bootstrap path stays
        readable and stable. The injected internal overload preserves direct proof
        over rendering composition without forcing tests through owner clipboard or
        graphics setup, so do not collapse this wrapper unless the replacement keeps
        the same testable separation.
         */
        public static RenderingDomainServices Assemble(ClickIt owner, ClickItSettings settings, GameController gameController, CoreDomainServices core)
            => Assemble(
                owner,
                settings,
                gameController,
                core,
                owner.Graphics,
                owner.LogMessage,
                (snapshot, now) => owner.GetDebugClipboardService().TryAutoCopyInventoryWarningDebugSnapshot(
                    snapshot,
                    now,
                    core.DeferredTextQueue.GetPendingTextSnapshot(0)));

        internal static RenderingDomainServices Assemble(
            BaseSettingsPlugin<ClickItSettings> plugin,
            ClickItSettings settings,
            GameController gameController,
            CoreDomainServices core,
            Graphics graphics,
            Action<string, int> logMessage,
            Func<InventoryDebugSnapshot, long, bool> tryAutoCopyInventoryWarningTrigger)
        {
            var debugRenderer = new DebugRenderer(plugin, core.AltarService, core.AreaService, core.WeightCalculator, core.DeferredTextQueue, core.DeferredFrameQueue);
            var strongboxRenderer = new StrongboxRenderer(settings, core.DeferredFrameQueue);
            var lazyModeRenderer = new LazyModeRenderer(settings, core.DeferredTextQueue, core.InputHandler, core.LazyModeBlockerService);
            var clickHotkeyToggleRenderer = new ClickHotkeyToggleRenderer(settings, core.DeferredTextQueue, core.InputHandler);
            var inventoryFullWarningRenderer = new InventoryFullWarningRenderer(
                core.DeferredTextQueue,
                core.AreaService,
                core.InventoryProbeService.GetLatestDebug,
                tryAutoCopyInventoryWarningTrigger);
            var pathfindingRenderer = new PathfindingRenderer(core.PathfindingService);
            var altarChoiceEvaluator = new AltarChoiceEvaluator(settings, logMessage);

            var altarDisplayRenderer = new AltarDisplayRenderer(
                graphics,
                settings,
                gameController,
                core.WeightCalculator,
                altarChoiceEvaluator,
                core.DeferredTextQueue,
                core.DeferredFrameQueue,
                core.AltarService,
                logMessage);

            return new RenderingDomainServices(
                debugRenderer,
                strongboxRenderer,
                lazyModeRenderer,
                clickHotkeyToggleRenderer,
                inventoryFullWarningRenderer,
                pathfindingRenderer,
                altarChoiceEvaluator,
                altarDisplayRenderer);
        }

        public static UltimatumRenderer CreateUltimatumRenderer(ClickItSettings settings, IClickAutomationService clickAutomationPort, DeferredFrameQueue deferredFrameQueue)
            => new(settings, clickAutomationPort, deferredFrameQueue);
    }
}