using ClickIt.Composition;
using ClickIt.Core.Runtime;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class PluginContextServiceStateInitializerTests
    {
        [TestMethod]
        public void InitializeFromComposedServices_AssignsStateAndCreatesRuntimeHost()
        {
            var ctx = new PluginContext();
            var services = new ComposedServices(
                PerformanceMonitor: (global::ClickIt.Utils.PerformanceMonitor)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Utils.PerformanceMonitor)),
                ErrorHandler: (global::ClickIt.Utils.ErrorHandler)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Utils.ErrorHandler)),
                AreaService: (global::ClickIt.Services.AreaService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.AreaService)),
                LabelService: (global::ClickIt.Services.LabelService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.LabelService)),
                CachedLabels: new TimeCache<List<LabelOnGround>>(() => new List<LabelOnGround>(), 50),
                Camera: (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera)),
                AltarService: (global::ClickIt.Services.AltarService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.AltarService)),
                LabelFilterService: (global::ClickIt.Services.LabelFilterService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.LabelFilterService)),
                ShrineService: (global::ClickIt.Services.ShrineService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.ShrineService)),
                InputHandler: (global::ClickIt.Utils.InputHandler)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Utils.InputHandler)),
                PathfindingService: (global::ClickIt.Services.PathfindingService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.PathfindingService)),
                DeferredTextQueue: (global::ClickIt.Utils.DeferredTextQueue)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Utils.DeferredTextQueue)),
                DeferredFrameQueue: (global::ClickIt.Utils.DeferredFrameQueue)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Utils.DeferredFrameQueue)),
                DebugRenderer: (global::ClickIt.Rendering.DebugRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.DebugRenderer)),
                StrongboxRenderer: (global::ClickIt.Rendering.StrongboxRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.StrongboxRenderer)),
                LazyModeRenderer: (global::ClickIt.Rendering.LazyModeRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.LazyModeRenderer)),
                ClickHotkeyToggleRenderer: (global::ClickIt.Rendering.ClickHotkeyToggleRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.ClickHotkeyToggleRenderer)),
                InventoryFullWarningRenderer: (global::ClickIt.Rendering.InventoryFullWarningRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.InventoryFullWarningRenderer)),
                PathfindingRenderer: (global::ClickIt.Rendering.PathfindingRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.PathfindingRenderer)),
                AltarDisplayRenderer: (global::ClickIt.Rendering.AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.AltarDisplayRenderer)),
                ClickService: (global::ClickIt.Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.ClickService)),
                UltimatumRenderer: (global::ClickIt.Rendering.UltimatumRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.UltimatumRenderer)),
                AlertService: (global::ClickIt.Services.AlertService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.AlertService)),
                EffectiveSettings: new global::ClickIt.ClickItSettings());

            PluginContextServiceStateInitializer.InitializeFromComposedServices(ctx, services);

            ctx.AreaService.Should().BeSameAs(services.AreaService);
            ctx.ClickService.Should().BeSameAs(services.ClickService);
            ctx.AlertService.Should().BeSameAs(services.AlertService);
            ctx.ClickRuntimeHost.Should().NotBeNull();
        }
    }
}