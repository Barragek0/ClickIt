namespace ClickIt.Core.Bootstrap
{
    internal readonly record struct RenderingDomainServices(
        StrongboxRenderer StrongboxRenderer,
        LazyModeRenderer LazyModeRenderer,
        ClickHotkeyToggleRenderer ClickHotkeyToggleRenderer,
        InventoryFullWarningRenderer InventoryFullWarningRenderer,
        PathfindingRenderer PathfindingRenderer,
        AltarChoiceEvaluator AltarChoiceEvaluator,
        AltarDisplayRenderer AltarDisplayRenderer,
        ImGuiDebugOverlay ImGuiDebugOverlay);

    internal static class RenderingDomainAssembler
    {
        /**
        Keep this thin runtime entry wrapper so the production bootstrap path stays
        readable and stable. The injected internal overload preserves direct proof
        over rendering composition without forcing tests through owner clipboard or
        graphics setup, so do not collapse this wrapper unless the replacement keeps
        the same testable separation.
         */
        public static RenderingDomainServices Assemble(ClickIt owner, ClickItSettings settings, CoreDomainServices core)
            => Assemble(
                owner,
                settings,
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
            CoreDomainServices core,
            Graphics graphics,
            Action<string, int> logMessage,
            Func<InventoryDebugSnapshot, long, bool> tryAutoCopyInventoryWarningTrigger)
        {
            StrongboxRenderer strongboxRenderer = new(settings, core.DeferredFrameQueue);
            LazyModeRenderer lazyModeRenderer = new(settings, core.DeferredTextQueue, core.InputHandler, core.LazyModeBlockerService);
            ClickHotkeyToggleRenderer clickHotkeyToggleRenderer = new(settings, core.DeferredTextQueue, core.InputHandler);
            InventoryFullWarningRenderer inventoryFullWarningRenderer = new(
                core.DeferredTextQueue,
                core.AreaService,
                core.InventoryProbeService.GetLatestDebug,
                tryAutoCopyInventoryWarningTrigger);
            PathfindingRenderer pathfindingRenderer = new(core.PathfindingService);
            AltarChoiceEvaluator altarChoiceEvaluator = new(settings, logMessage);

            AltarDisplayRenderer altarDisplayRenderer = new(
                graphics,
                core.WeightCalculator,
                altarChoiceEvaluator,
                core.DeferredTextQueue,
                core.DeferredFrameQueue,
                core.AltarService,
                logMessage);

            ImGuiDebugOverlay guiDebugOverlay = new(settings, core.PerformanceMonitor, core.BlightService, new PluginDebugTelemetrySource(plugin));

            return new RenderingDomainServices(
                strongboxRenderer,
                lazyModeRenderer,
                clickHotkeyToggleRenderer,
                inventoryFullWarningRenderer,
                pathfindingRenderer,
                altarChoiceEvaluator,
                altarDisplayRenderer,
                guiDebugOverlay);
        }

        public static UltimatumRenderer CreateUltimatumRenderer(ClickItSettings settings, IClickAutomationService clickAutomationPort, DeferredFrameQueue deferredFrameQueue)
            => new(settings, clickAutomationPort, deferredFrameQueue);
    }
}