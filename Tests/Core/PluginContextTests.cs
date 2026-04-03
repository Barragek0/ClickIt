using FluentAssertions;
using ClickIt.Features.Observability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ClickIt.Tests.Core
{
    [TestClass]
    public class PluginContextTests
    {
        [TestMethod]
        public void Constructor_InitializesExpectedDefaults()
        {
            var state = new PluginContext();

            state.Random.Should().NotBeNull();
            state.Services.Should().NotBeNull();
            state.Runtime.Should().NotBeNull();
            state.Rendering.Should().NotBeNull();
            state.Runtime.LastRenderTimer.Should().NotBeNull();
            state.Runtime.LastTickTimer.Should().NotBeNull();
            state.Runtime.Timer.Should().NotBeNull();
            state.Runtime.SecondTimer.Should().NotBeNull();
            state.Runtime.LastHotkeyState.Should().BeFalse();
            state.Runtime.WorkFinished.Should().BeFalse();
            state.Services.PerformanceMonitor.Should().BeNull();
            state.Services.AreaService.Should().BeNull();
            state.Services.Camera.Should().BeNull();
        }

        [TestMethod]
        public void MutableProperties_CanBeSetAndReadBack()
        {
            var state = new PluginContext
            {
                Runtime = { LastHotkeyState = true, WorkFinished = true }
            };

            state.Runtime.LastHotkeyState.Should().BeTrue();
            state.Runtime.WorkFinished.Should().BeTrue();
        }

        [TestMethod]
        public void DisposeCompositionRoot_ClearsTrackedServiceReferences()
        {
            var state = new PluginContext
            {
                Services =
                {
                    PerformanceMonitor = new PerformanceMonitor(new ClickItSettings()),
                    ErrorHandler = new ErrorHandler(new ClickItSettings(), static (_, _) => { }, static (_, _) => { }),
                    AreaService = new AreaService(),
                    LabelFilterService = (LabelFilterService)RuntimeHelpers.GetUninitializedObject(typeof(LabelFilterService)),
                    LabelService = (global::ClickIt.Features.Labels.LabelService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Features.Labels.LabelService)),
                    ClickService = (ClickService)RuntimeHelpers.GetUninitializedObject(typeof(ClickService)),
                    PathfindingService = (PathfindingService)RuntimeHelpers.GetUninitializedObject(typeof(PathfindingService)),
                    AlertService = (AlertService)RuntimeHelpers.GetUninitializedObject(typeof(AlertService))
                },
                Rendering =
                {
                    DeferredTextQueue = new DeferredTextQueue(),
                    DeferredFrameQueue = new DeferredFrameQueue()
                }
            };

            state.DisposeCompositionRoot();

            state.Services.PerformanceMonitor.Should().BeNull();
            state.Services.AreaService.Should().BeNull();
            state.Rendering.DeferredTextQueue.Should().BeNull();
            state.Rendering.DeferredFrameQueue.Should().BeNull();
            state.Services.ClickService.Should().BeNull();
            state.Services.ErrorHandler.Should().BeNull();
            state.Services.AreaService.Should().BeNull();
            state.Rendering.DeferredTextQueue.Should().BeNull();
            state.Rendering.DeferredFrameQueue.Should().BeNull();
            state.Services.LabelFilterService.Should().BeNull();
            state.Services.LabelService.Should().BeNull();
            state.Services.ClickService.Should().BeNull();
            state.Services.PathfindingService.Should().BeNull();
            state.Services.AlertService.Should().BeNull();
        }

        [TestMethod]
        public void GetDebugTelemetrySnapshot_WhenServicesUnavailable_ReturnsEmptyServiceSnapshots()
        {
            var state = new PluginContext();

            DebugTelemetrySnapshot snapshot = state.GetDebugTelemetrySnapshot();

            snapshot.Click.ServiceAvailable.Should().BeFalse();
            snapshot.Label.ServiceAvailable.Should().BeFalse();
            snapshot.Pathfinding.ServiceAvailable.Should().BeFalse();
            snapshot.Click.Click.HasData.Should().BeFalse();
            snapshot.Click.RuntimeLog.HasData.Should().BeFalse();
            snapshot.Click.Ultimatum.HasData.Should().BeFalse();
            snapshot.Label.Label.HasData.Should().BeFalse();
            snapshot.Pathfinding.OffscreenMovement.HasData.Should().BeFalse();
            snapshot.Inventory.Inventory.HasData.Should().BeFalse();
        }

        [TestMethod]
        public void GetDebugTelemetrySnapshot_WhenServicesUnavailable_ReturnsEmptyTrails()
        {
            var state = new PluginContext();

            DebugTelemetrySnapshot snapshot = state.GetDebugTelemetrySnapshot();

            snapshot.Click.ClickTrail.Should().BeEmpty();
            snapshot.Click.RuntimeLogTrail.Should().BeEmpty();
            snapshot.Click.UltimatumTrail.Should().BeEmpty();
            snapshot.Click.UltimatumOptionPreview.Should().BeEmpty();
            snapshot.Label.LabelTrail.Should().BeEmpty();
            snapshot.Pathfinding.OffscreenMovementTrail.Should().BeEmpty();
            snapshot.Inventory.InventoryTrail.Should().BeEmpty();
        }

        [TestMethod]
        public void FreezeDebugTelemetrySnapshot_ActivatesHoldState_WhenDurationPositive()
        {
            var state = new PluginContext();

            state.FreezeDebugTelemetrySnapshot("offscreen-click", 1000);

            state.TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason).Should().BeTrue();
            remainingMs.Should().BeGreaterThan(0);
            reason.Should().Be("offscreen-click");
        }

        [TestMethod]
        public void FreezeDebugTelemetrySnapshot_ExpiresAfterRequestedWindow()
        {
            var state = new PluginContext();

            state.FreezeDebugTelemetrySnapshot("short-hold", 20);
            Thread.Sleep(40);

            state.TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason).Should().BeFalse();
            remainingMs.Should().Be(0);
            reason.Should().BeEmpty();
        }
    }
}