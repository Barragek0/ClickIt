namespace ClickIt.Tests.Core.Bootstrap
{
    [TestClass]
    public class PluginCompositionBootstrapperTests
    {
        [TestMethod]
        public void ApplyPorts_AssignsFeatureAndOverlayPorts()
        {
            var ctx = new PluginContext();
            var core = new CoreDomainServices(
                PerformanceMonitor: (PerformanceMonitor)RuntimeHelpers.GetUninitializedObject(typeof(PerformanceMonitor)),
                ErrorHandler: (ErrorHandler)RuntimeHelpers.GetUninitializedObject(typeof(ErrorHandler)),
                AreaService: (AreaService)RuntimeHelpers.GetUninitializedObject(typeof(AreaService)),
                LabelReadModelService: (LabelReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(LabelReadModelService)),
                LabelService: (LabelService)RuntimeHelpers.GetUninitializedObject(typeof(LabelService)),
                CachedLabels: new TimeCache<List<LabelOnGround>>(() => new List<LabelOnGround>(), 50),
                Camera: (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera)),
                AltarService: (AltarService)RuntimeHelpers.GetUninitializedObject(typeof(AltarService)),
                LabelFilterPort: (LabelFilterService)RuntimeHelpers.GetUninitializedObject(typeof(LabelFilterService)),
                ShrineService: (ShrineService)RuntimeHelpers.GetUninitializedObject(typeof(ShrineService)),
                InputHandler: (InputHandler)RuntimeHelpers.GetUninitializedObject(typeof(InputHandler)),
                PathfindingService: (PathfindingService)RuntimeHelpers.GetUninitializedObject(typeof(PathfindingService)),
                WeightCalculator: (WeightCalculator)RuntimeHelpers.GetUninitializedObject(typeof(WeightCalculator)),
                DeferredTextQueue: (DeferredTextQueue)RuntimeHelpers.GetUninitializedObject(typeof(DeferredTextQueue)),
                DeferredFrameQueue: (DeferredFrameQueue)RuntimeHelpers.GetUninitializedObject(typeof(DeferredFrameQueue)));
            var rendering = new RenderingDomainServices(
                DebugRenderer: (DebugRenderer)RuntimeHelpers.GetUninitializedObject(typeof(DebugRenderer)),
                StrongboxRenderer: (StrongboxRenderer)RuntimeHelpers.GetUninitializedObject(typeof(StrongboxRenderer)),
                LazyModeRenderer: (LazyModeRenderer)RuntimeHelpers.GetUninitializedObject(typeof(LazyModeRenderer)),
                ClickHotkeyToggleRenderer: (ClickHotkeyToggleRenderer)RuntimeHelpers.GetUninitializedObject(typeof(ClickHotkeyToggleRenderer)),
                InventoryFullWarningRenderer: (InventoryFullWarningRenderer)RuntimeHelpers.GetUninitializedObject(typeof(InventoryFullWarningRenderer)),
                PathfindingRenderer: (PathfindingRenderer)RuntimeHelpers.GetUninitializedObject(typeof(PathfindingRenderer)),
                AltarDisplayRenderer: (AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(AltarDisplayRenderer)));
            var clickAutomationPort = (ClickService)RuntimeHelpers.GetUninitializedObject(typeof(ClickService));
            var ultimatumRenderer = (UltimatumRenderer)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumRenderer));
            var alertService = (AlertService)RuntimeHelpers.GetUninitializedObject(typeof(AlertService));

            PluginCompositionBootstrapper.ApplyPorts(ctx, core, rendering, clickAutomationPort, ultimatumRenderer, alertService);

            ctx.Services.AreaService.Should().BeSameAs(core.AreaService);
            ctx.Services.LabelFilterPort.Should().BeSameAs(core.LabelFilterPort);
            ctx.Services.ClickAutomationPort.Should().BeSameAs(clickAutomationPort);
            ctx.Services.AlertService.Should().BeSameAs(alertService);
            ctx.Rendering.ClickRuntimeHost.Should().NotBeNull();
        }
    }
}