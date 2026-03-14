using FluentAssertions;
using ClickIt.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItRenderTests
    {
        [TestMethod]
        public void Render_ReturnsQuickly_WhenPerformanceMonitorNull()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            clickIt.State.PerformanceMonitor = null;
            clickIt.State.IsRendering = false;

            clickIt.Render();
            clickIt.State.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_SetsIsRendering_AndRestoresIt_AroundRenderInternal()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            clickIt.State.PerformanceMonitor = new PerformanceMonitor(clickIt.__Test_GetSettings());

            clickIt.State.DeferredTextQueue = new DeferredTextQueue();
            clickIt.State.DeferredFrameQueue = new DeferredFrameQueue();

            clickIt.Render();
            clickIt.State.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_RecordsCoreRenderSections_WhenPerformanceMonitorPresent()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            clickIt.__Test_SetSettings(settings);

            var monitor = new PerformanceMonitor(settings);
            clickIt.State.PerformanceMonitor = monitor;
            clickIt.State.DeferredTextQueue = new DeferredTextQueue();
            clickIt.State.DeferredFrameQueue = new DeferredFrameQueue();

            clickIt.Render();

            monitor.GetRenderSectionStats(RenderSection.UltimatumOverlay).SampleCount.Should().BeGreaterThan(0);
            monitor.GetRenderSectionStats(RenderSection.StrongboxOverlay).SampleCount.Should().BeGreaterThan(0);
            monitor.GetRenderSectionStats(RenderSection.TextFlush).SampleCount.Should().BeGreaterThan(0);
            monitor.GetRenderSectionStats(RenderSection.FrameFlush).SampleCount.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void Render_RecordsDebugOverlayOnlyWhenDebugRenderingEnabled()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            clickIt.__Test_SetSettings(settings);

            var monitor = new PerformanceMonitor(settings);
            clickIt.State.PerformanceMonitor = monitor;
            clickIt.State.DeferredTextQueue = new DeferredTextQueue();
            clickIt.State.DeferredFrameQueue = new DeferredFrameQueue();

            settings.DebugMode.Value = false;
            settings.RenderDebug.Value = false;
            clickIt.Render();

            monitor.GetRenderSectionStats(RenderSection.DebugOverlay).SampleCount.Should().Be(0);

            settings.DebugMode.Value = true;
            settings.RenderDebug.Value = true;
            settings.DebugShowFrames.Value = false;
            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowRecentErrors.Value = false;

            clickIt.Render();

            monitor.GetRenderSectionStats(RenderSection.DebugOverlay).SampleCount.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void Render_DetailedDebugSectionGate_ControlsDebugOverlayRecording()
        {
            var clickIt = new ClickIt();
            var settings = new ClickItSettings();
            clickIt.__Test_SetSettings(settings);

            var monitor = new PerformanceMonitor(settings);
            clickIt.State.PerformanceMonitor = monitor;
            clickIt.State.DeferredTextQueue = new DeferredTextQueue();
            clickIt.State.DeferredFrameQueue = new DeferredFrameQueue();

            settings.DebugMode.Value = true;
            settings.RenderDebug.Value = true;
            settings.DebugShowFrames.Value = false;
            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowRecentErrors.Value = false;

            clickIt.Render();

            monitor.GetRenderSectionStats(RenderSection.DebugOverlay).SampleCount.Should().BeGreaterThan(0);

            var before = clickIt.State.DeferredTextQueue.GetPendingCount();

            settings.DebugShowStatus.Value = true;
            clickIt.Render();

            var after = clickIt.State.DeferredTextQueue.GetPendingCount();
            after.Should().BeGreaterThanOrEqualTo(before);
            monitor.GetRenderSectionStats(RenderSection.DebugOverlay).SampleCount.Should().BeGreaterThan(1);
        }

        [TestMethod]
        public void ShouldSkipAutoCopyForOffscreenMovementNoData_ReturnsTrue_WhenNoDataLinePresent()
        {
            var method = typeof(ClickIt).GetMethod("ShouldSkipAutoCopyForOffscreenMovementNoData", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            string[] lines =
            [
                "--- Pathfinding ---",
                "Offscreen Movement: <no data>",
                "Some other debug line"
            ];

            bool shouldSkip = (bool)method!.Invoke(null, [lines])!;
            shouldSkip.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSkipAutoCopyForOffscreenMovementNoData_ReturnsFalse_WhenMovementDataExists()
        {
            var method = typeof(ClickIt).GetMethod("ShouldSkipAutoCopyForOffscreenMovementNoData", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            string[] lines =
            [
                "--- Pathfinding ---",
                "Offscreen Stage: Clicked | built=True | fromPath=True | clickPoint=True",
                "Offscreen Target: Metadata/Terrain/Leagues/Ultimatum/Objects/UltimatumChallengeInteractable"
            ];

            bool shouldSkip = (bool)method!.Invoke(null, [lines])!;
            shouldSkip.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldApplyOffscreenNoDataAutoCopySkip_ReturnsTrue_WhenPathfindingIsOnlyDetailedSectionEnabled()
        {
            var method = typeof(ClickIt).GetMethod("ShouldApplyOffscreenNoDataAutoCopySkip", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var settings = new ClickItSettings();
            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowRecentErrors.Value = false;
            settings.DebugShowPathfinding.Value = true;

            bool shouldApply = (bool)method!.Invoke(null, [settings])!;
            shouldApply.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldApplyOffscreenNoDataAutoCopySkip_ReturnsFalse_WhenAnotherDetailedSectionIsEnabled()
        {
            var method = typeof(ClickIt).GetMethod("ShouldApplyOffscreenNoDataAutoCopySkip", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var settings = new ClickItSettings();
            settings.DebugShowStatus.Value = true;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowRecentErrors.Value = false;
            settings.DebugShowPathfinding.Value = true;

            bool shouldApply = (bool)method!.Invoke(null, [settings])!;
            shouldApply.Should().BeFalse();
        }

    }
}
