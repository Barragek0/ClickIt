using ClickIt.Composition;
using ClickIt.Services.Click.Application;

namespace ClickIt.Core.Runtime
{
    internal static class PluginContextServiceStateInitializer
    {
        internal static void InitializeFromComposedServices(PluginContext context, ComposedServices services)
        {
            PluginServices serviceState = context.Services;
            PluginRenderingState renderingState = context.Rendering;

            serviceState.PerformanceMonitor = services.PerformanceMonitor;
            serviceState.ErrorHandler = services.ErrorHandler;
            serviceState.AreaService = services.AreaService;
            serviceState.LabelService = services.LabelService;
            serviceState.CachedLabels = services.CachedLabels;
            serviceState.Camera = services.Camera;
            serviceState.AltarService = services.AltarService;
            serviceState.LabelFilterService = services.LabelFilterService;
            serviceState.ShrineService = services.ShrineService;
            serviceState.InputHandler = services.InputHandler;
            serviceState.PathfindingService = services.PathfindingService;
            serviceState.ClickService = services.ClickService;
            serviceState.AlertService = services.AlertService;

            renderingState.DeferredTextQueue = services.DeferredTextQueue;
            renderingState.DeferredFrameQueue = services.DeferredFrameQueue;
            renderingState.DebugRenderer = services.DebugRenderer;
            renderingState.StrongboxRenderer = services.StrongboxRenderer;
            renderingState.LazyModeRenderer = services.LazyModeRenderer;
            renderingState.ClickHotkeyToggleRenderer = services.ClickHotkeyToggleRenderer;
            renderingState.InventoryFullWarningRenderer = services.InventoryFullWarningRenderer;
            renderingState.PathfindingRenderer = services.PathfindingRenderer;
            renderingState.AltarDisplayRenderer = services.AltarDisplayRenderer;
            renderingState.ClickRuntimeHost = new ClickRuntimeHost(() => serviceState.ClickService as IClickAutomationService);
            renderingState.UltimatumRenderer = services.UltimatumRenderer;
        }
    }
}