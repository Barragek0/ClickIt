namespace ClickIt.Core.Runtime
{
    internal static class PluginContextServiceStateResetter
    {
        internal static void Reset(PluginContext context)
        {
            PluginServices serviceState = context.Services;
            PluginRenderingState renderingState = context.Rendering;

            serviceState.AreaService = null;
            serviceState.AltarService = null;
            serviceState.ShrineService = null;
            serviceState.InputHandler = null;
            serviceState.PathfindingService = null;
            serviceState.AlertService = null;
            serviceState.LabelService = null;
            serviceState.LabelFilterService = null;
            serviceState.ClickService = null;
            serviceState.Camera = null;
            serviceState.PerformanceMonitor = null;
            serviceState.ErrorHandler = null;
            serviceState.CachedLabels = null;

            renderingState.DebugRenderer = null;
            renderingState.StrongboxRenderer = null;
            renderingState.UltimatumRenderer = null;
            renderingState.LazyModeRenderer = null;
            renderingState.ClickHotkeyToggleRenderer = null;
            renderingState.InventoryFullWarningRenderer = null;
            renderingState.PathfindingRenderer = null;
            renderingState.DeferredTextQueue = null;
            renderingState.DeferredFrameQueue = null;
            renderingState.AltarDisplayRenderer = null;
            renderingState.ClickRuntimeHost = null;
        }
    }
}