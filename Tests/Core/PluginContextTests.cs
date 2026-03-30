using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class PluginContextTests
    {
        [TestMethod]
        public void Constructor_InitializesExpectedDefaults()
        {
            var state = new PluginContext();

            state.Random.Should().NotBeNull();
            state.LastRenderTimer.Should().NotBeNull();
            state.LastTickTimer.Should().NotBeNull();
            state.Timer.Should().NotBeNull();
            state.SecondTimer.Should().NotBeNull();
            state.LastHotkeyState.Should().BeFalse();
            state.WorkFinished.Should().BeFalse();
            state.PerformanceMonitor.Should().BeNull();
            state.AreaService.Should().BeNull();
            state.Camera.Should().BeNull();
        }

        [TestMethod]
        public void MutableProperties_CanBeSetAndReadBack()
        {
            var state = new PluginContext
            {
                LastHotkeyState = true,
                WorkFinished = true
            };

            state.LastHotkeyState.Should().BeTrue();
            state.WorkFinished.Should().BeTrue();
        }

        [TestMethod]
        public void DisposeCompositionRoot_ClearsTrackedServiceReferences()
        {
            var state = new PluginContext
            {
                PerformanceMonitor = new global::ClickIt.Utils.PerformanceMonitor(new ClickItSettings()),
                ErrorHandler = new global::ClickIt.Utils.ErrorHandler(new ClickItSettings(), static (_, _) => { }, static (_, _) => { }),
                AreaService = new Services.AreaService(),
                DeferredTextQueue = new global::ClickIt.Utils.DeferredTextQueue(),
                DeferredFrameQueue = new global::ClickIt.Utils.DeferredFrameQueue(),
                LabelFilterService = (Services.LabelFilterService)RuntimeHelpers.GetUninitializedObject(typeof(Services.LabelFilterService)),
                LabelService = (Services.LabelService)RuntimeHelpers.GetUninitializedObject(typeof(Services.LabelService)),
                ClickService = (Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(Services.ClickService)),
                PathfindingService = (Services.PathfindingService)RuntimeHelpers.GetUninitializedObject(typeof(Services.PathfindingService)),
                AlertService = (Services.AlertService)RuntimeHelpers.GetUninitializedObject(typeof(Services.AlertService))
            };

            state.DisposeCompositionRoot();

            state.PerformanceMonitor.Should().BeNull();
            state.ErrorHandler.Should().BeNull();
            state.AreaService.Should().BeNull();
            state.DeferredTextQueue.Should().BeNull();
            state.DeferredFrameQueue.Should().BeNull();
            state.LabelFilterService.Should().BeNull();
            state.LabelService.Should().BeNull();
            state.ClickService.Should().BeNull();
            state.PathfindingService.Should().BeNull();
            state.AlertService.Should().BeNull();
        }
    }
}