namespace ClickIt.Core.Runtime
{
    internal static class PluginContextServiceStateResetter
    {
        internal static void Reset(PluginContext context)
        {
            context.AreaService = null;
            context.AltarService = null;
            context.ShrineService = null;
            context.InputHandler = null;
            context.DebugRenderer = null;
            context.StrongboxRenderer = null;
            context.UltimatumRenderer = null;
            context.LazyModeRenderer = null;
            context.ClickHotkeyToggleRenderer = null;
            context.InventoryFullWarningRenderer = null;
            context.PathfindingRenderer = null;
            context.DeferredTextQueue = null;
            context.DeferredFrameQueue = null;
            context.AltarDisplayRenderer = null;
            context.PathfindingService = null;
            context.AlertService = null;
            context.LabelService = null;
            context.LabelFilterService = null;
            context.ClickService = null;
            context.ClickRuntimeHost = null;
            context.Camera = null;
            context.PerformanceMonitor = null;
            context.ErrorHandler = null;
            context.CachedLabels = null;
        }
    }
}