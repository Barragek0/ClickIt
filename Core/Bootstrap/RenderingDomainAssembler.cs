namespace ClickIt.Core.Bootstrap
{
    internal readonly record struct RenderingDomainServices(
        AltarChoiceEvaluator AltarChoiceEvaluator,
        ImGuiDebugOverlay ImGuiDebugOverlay,
        UiRegionRectangleOverlay UiRegionRectangleOverlay,
        OverlayRenderHost OverlayRenderHost);

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
                owner.LogMessage);

        internal static RenderingDomainServices Assemble(
            BaseSettingsPlugin<ClickItSettings> plugin,
            ClickItSettings settings,
            CoreDomainServices core,
            Graphics graphics,
            Action<string, int> logMessage)
        {
            OverlayRenderHost overlayRenderHost = new();
            overlayRenderHost.Register(new StrongboxOverlay());

            AltarChoiceEvaluator altarChoiceEvaluator = new(settings, logMessage);
            overlayRenderHost.Register(new AltarOverlay(core.WeightCalculator, altarChoiceEvaluator, core.AltarService, logMessage));
            overlayRenderHost.Register(new HarvestOverlay(core.HarvestService));
            overlayRenderHost.Register(new LazyModeOverlay(core.InputHandler, core.LazyModeBlockerService));
            overlayRenderHost.Register(new ClickHotkeyToggleOverlay(core.InputHandler));
            overlayRenderHost.Register(new InventoryFullWarningOverlay(core.AreaService, core.InventoryProbeService.GetLatestDebug));
            overlayRenderHost.Register(new PathfindingOverlay(core.PathfindingService));
            overlayRenderHost.Register(new BlightOverlay(core.BlightService));
            overlayRenderHost.Register(new PerformanceInGameOverlay(core.PerformanceMonitor.GetDebugSnapshot, () => AreaService.IsInMap(plugin.GameController)));

            ImGuiDebugOverlay guiDebugOverlay = new(settings, core.PerformanceMonitor, core.BlightService, new PluginDebugTelemetrySource(plugin));

            UiRegionRectangleOverlay uiRegionRectangleOverlay = new(settings, core.AreaService);

            return new RenderingDomainServices(
                altarChoiceEvaluator,
                guiDebugOverlay,
                uiRegionRectangleOverlay,
                overlayRenderHost);
        }

        public static UltimatumOverlay CreateUltimatumOverlay(ClickItSettings settings, ClickAutomationPort clickAutomationPort)
            => new(
                () => clickAutomationPort.TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews) ? previews : null,
                () => clickAutomationPort.RefreshUltimatumPreview());
    }
}