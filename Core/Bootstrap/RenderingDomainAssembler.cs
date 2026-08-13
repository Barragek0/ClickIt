namespace ClickIt.Core.Bootstrap
{
    internal readonly record struct RenderingDomainServices(
        AltarChoiceEvaluator AltarChoiceEvaluator,
        ImGuiDebugOverlay ImGuiDebugOverlay,
        UiRegionRectangleOverlay UiRegionRectangleOverlay,
        OverlayRenderHost OverlayRenderHost);

    internal static class RenderingDomainAssembler
    {
        // Thin runtime entry wrapper so the injected internal overload stays testable without runtime traversal.
        public static RenderingDomainServices Assemble(ClickIt owner, ClickItSettings settings, CoreDomainServices core)
            => Assemble(
                owner,
                settings,
                core,
                owner.LogMessage);

        internal static RenderingDomainServices Assemble(
            BaseSettingsPlugin<ClickItSettings> plugin,
            ClickItSettings settings,
            CoreDomainServices core,
            Action<string, int> logMessage)
        {
            OverlayRenderHost overlayRenderHost = new();
            overlayRenderHost.Register(new StrongboxOverlay(
                (ReadOnlySpan<long> bytes, ReadOnlySpan<double> ms) => core.PerformanceMonitor.RecordBreakdown(ProcessingSection.Strongbox, bytes, ms)));

            AltarChoiceEvaluator altarChoiceEvaluator = new(settings, logMessage);
            overlayRenderHost.Register(new AltarOverlay(core.WeightCalculator, altarChoiceEvaluator, core.AltarService, logMessage));
            overlayRenderHost.Register(new HarvestOverlay(core.HarvestService));
            overlayRenderHost.Register(new LazyModeOverlay(core.InputHandler, core.LazyModeBlockerService));
            overlayRenderHost.Register(new ClickHotkeyToggleOverlay(core.InputHandler));
            overlayRenderHost.Register(new InventoryFullWarningOverlay(core.AreaService, core.InventoryProbeService.GetLatestDebug));
            overlayRenderHost.Register(new PathfindingOverlay(core.PathfindingService));
            overlayRenderHost.Register(new BlightOverlay(core.BlightService));
            overlayRenderHost.Register(new PerformanceInGameOverlay(core.PerformanceMonitor.GetDebugSnapshot, () => AreaService.IsInMap(plugin.GameController)));

            ImGuiDebugOverlay guiDebugOverlay = new(settings, core.PerformanceMonitor, core.BlightService, new PluginDebugTelemetrySource(plugin), core.HarvestService);

            UiRegionRectangleOverlay uiRegionRectangleOverlay = new(settings, core.AreaService);

            return new RenderingDomainServices(
                altarChoiceEvaluator,
                guiDebugOverlay,
                uiRegionRectangleOverlay,
                overlayRenderHost);
        }

        public static UltimatumOverlay CreateUltimatumOverlay(ClickAutomationPort clickAutomationPort)
            => new(
                () => clickAutomationPort.TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews) ? previews : null,
                () => clickAutomationPort.RefreshUltimatumPreview());
    }
}
