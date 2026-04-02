using FluentAssertions;
using ClickIt.Services.Observability;
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
                AreaService = new global::ClickIt.Services.AreaService(),
                DeferredTextQueue = new global::ClickIt.Utils.DeferredTextQueue(),
                DeferredFrameQueue = new global::ClickIt.Utils.DeferredFrameQueue(),
                LabelFilterService = (global::ClickIt.Services.LabelFilterService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.LabelFilterService)),
                LabelService = (global::ClickIt.Services.LabelService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.LabelService)),
                ClickService = (global::ClickIt.Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.ClickService)),
                PathfindingService = (global::ClickIt.Services.PathfindingService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.PathfindingService)),
                AlertService = (global::ClickIt.Services.AlertService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.AlertService))
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