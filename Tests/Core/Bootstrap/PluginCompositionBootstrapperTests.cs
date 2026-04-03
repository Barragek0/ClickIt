using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
                LabelReadModelService: (LabelSelection.LabelReadModelService)RuntimeHelpers.GetUninitializedObject(typeof(LabelSelection.LabelReadModelService)),
                LabelService: (global::ClickIt.Features.Labels.LabelService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Features.Labels.LabelService)),
                CachedLabels: new TimeCache<List<LabelOnGround>>(() => new List<LabelOnGround>(), 50),
                Camera: (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera)),
                AltarService: (Altars.AltarService)RuntimeHelpers.GetUninitializedObject(typeof(Altars.AltarService)),
                LabelFilterService: (LabelFilterService)RuntimeHelpers.GetUninitializedObject(typeof(LabelFilterService)),
                ShrineService: (Shrines.ShrineService)RuntimeHelpers.GetUninitializedObject(typeof(Shrines.ShrineService)),
                InputHandler: (InputHandler)RuntimeHelpers.GetUninitializedObject(typeof(InputHandler)),
                PathfindingService: (PathfindingService)RuntimeHelpers.GetUninitializedObject(typeof(PathfindingService)),
                WeightCalculator: (Altars.WeightCalculator)RuntimeHelpers.GetUninitializedObject(typeof(Altars.WeightCalculator)),
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
            var clickService = (ClickService)RuntimeHelpers.GetUninitializedObject(typeof(ClickService));
            var ultimatumRenderer = (UltimatumRenderer)RuntimeHelpers.GetUninitializedObject(typeof(UltimatumRenderer));
            var alertService = (AlertService)RuntimeHelpers.GetUninitializedObject(typeof(AlertService));

            PluginCompositionBootstrapper.ApplyPorts(ctx, core, rendering, clickService, ultimatumRenderer, alertService);

            ctx.Services.AreaService.Should().BeSameAs(core.AreaService);
            ctx.Services.ClickService.Should().BeSameAs(clickService);
            ctx.Services.AlertService.Should().BeSameAs(alertService);
            ctx.Rendering.ClickRuntimeHost.Should().NotBeNull();
        }
    }
}