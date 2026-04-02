using ClickIt.Composition;
using ClickIt.Services.Click.Application;

namespace ClickIt.Core.Runtime
{
    internal static class PluginContextServiceStateInitializer
    {
        internal static void InitializeFromComposedServices(PluginContext context, ComposedServices services)
        {
            context.PerformanceMonitor = services.PerformanceMonitor;
            context.ErrorHandler = services.ErrorHandler;
            context.AreaService = services.AreaService;
            context.LabelService = services.LabelService;
            context.CachedLabels = services.CachedLabels;
            context.Camera = services.Camera;
            context.AltarService = services.AltarService;
            context.LabelFilterService = services.LabelFilterService;
            context.ShrineService = services.ShrineService;
            context.InputHandler = services.InputHandler;
            context.PathfindingService = services.PathfindingService;
            context.DeferredTextQueue = services.DeferredTextQueue;
            context.DeferredFrameQueue = services.DeferredFrameQueue;
            context.DebugRenderer = services.DebugRenderer;
            context.StrongboxRenderer = services.StrongboxRenderer;
            context.LazyModeRenderer = services.LazyModeRenderer;
            context.ClickHotkeyToggleRenderer = services.ClickHotkeyToggleRenderer;
            context.InventoryFullWarningRenderer = services.InventoryFullWarningRenderer;
            context.PathfindingRenderer = services.PathfindingRenderer;
            context.AltarDisplayRenderer = services.AltarDisplayRenderer;
            context.ClickService = services.ClickService;
            context.ClickRuntimeHost = new ClickRuntimeHost(() => context.ClickService as IClickAutomationService);
            context.UltimatumRenderer = services.UltimatumRenderer;
            context.AlertService = services.AlertService;
        }
    }
}