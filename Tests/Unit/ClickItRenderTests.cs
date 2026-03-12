using FluentAssertions;
using ClickIt.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

            // Ensure performance monitor is null -> Render should return without change
            clickIt.State.PerformanceMonitor = null;
            clickIt.State.IsRendering = false;

            // No exception, and IsRendering should remain false after call
            clickIt.Render();
            clickIt.State.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_SetsIsRendering_AndRestoresIt_AroundRenderInternal()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            // Provide a simple PerformanceMonitor so Render proceeds into RenderInternal
            clickIt.State.PerformanceMonitor = new PerformanceMonitor(clickIt.__Test_GetSettings());

            // Ensure required queues exist; Graphics can remain null (flush is no-op in that case)
            clickIt.State.DeferredTextQueue = new DeferredTextQueue();
            clickIt.State.DeferredFrameQueue = new DeferredFrameQueue();

            // Sanity: Render should not throw and IsRendering should be false when Render returns
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

            // DebugOverlay section timing still records the frame even when detailed sections are disabled.
            monitor.GetRenderSectionStats(RenderSection.DebugOverlay).SampleCount.Should().BeGreaterThan(0);

            var before = clickIt.State.DeferredTextQueue.GetPendingCount();

            settings.DebugShowStatus.Value = true;
            clickIt.Render();

            var after = clickIt.State.DeferredTextQueue.GetPendingCount();
            after.Should().BeGreaterThanOrEqualTo(before);
            monitor.GetRenderSectionStats(RenderSection.DebugOverlay).SampleCount.Should().BeGreaterThan(1);
        }
    }
}
