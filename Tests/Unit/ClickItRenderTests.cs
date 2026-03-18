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
        public void CopyAdditionalDebugInfoButtonPressed_SetsCopyRequestFlag()
        {
            var method = typeof(ClickIt).GetMethod("CopyAdditionalDebugInfoButtonPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            var flagField = typeof(ClickIt).GetField("_copyAdditionalDebugInfoRequested", BindingFlags.NonPublic | BindingFlags.Instance);
            flagField.Should().NotBeNull();

            flagField!.SetValue(clickIt, false);
            method!.Invoke(clickIt, null);

            ((bool)flagField.GetValue(clickIt)!).Should().BeTrue();
        }

        [TestMethod]
        public void BuildDebugClipboardPayload_FormatsHeaderAndSkipsBlankLines()
        {
            var method = typeof(ClickIt).GetMethod("BuildDebugClipboardPayload", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            string[] lines = [
                "--- Clicking ---",
                "",
                "Stage: ClickExecuted",
                "   "
            ];

            string payload = (string)method!.Invoke(null, [lines])!;
            payload.Should().Contain("=== ClickIt Additional Debug Information ===");
            payload.Should().Contain("--- Clicking ---");
            payload.Should().Contain("Stage: ClickExecuted");
        }

        [TestMethod]
        public void GetDeepMemoryDumpStatusMessage_ReturnsEmpty_WhenCopyMemoryDumpFeatureDisabled()
        {
            var method = typeof(ClickIt).GetMethod("GetDeepMemoryDumpStatusMessage", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            string status = (string)method!.Invoke(clickIt, null)!;
            status.Should().BeEmpty();
        }

    }
}
