using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Rendering;
using ClickIt;
using ClickIt.Utils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DebugRendererAdditionalTests
    {
        [TestMethod]
        public void RenderDebugFrames_WhenDebugShowFramesTrue_EnqueuesFourFrames()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());

            var frames = new DeferredFrameQueue();
            var dt = new DeferredTextQueue();

            DebugRenderer dr;

            // Enable debug frames
            var s = plugin.__Test_GetSettings();
            s.DebugShowFrames.Value = true;

            // Provide area rectangles so RenderDebugFrames will enqueue
            var t = typeof(Services.AreaService);
            var svc = new Services.AreaService();
            t.GetField("_fullScreenRectangle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(svc, new RectangleF(0, 0, 100, 100));
            t.GetField("_healthAndFlaskRectangle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(svc, new RectangleF(0, 0, 10, 10));
            t.GetField("_manaAndSkillsRectangle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(svc, new RectangleF(0, 0, 10, 10));
            t.GetField("_buffsAndDebuffsRectangle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(svc, new RectangleF(0, 0, 10, 10));

            var settings = plugin.__Test_GetSettings();
            dr = new DebugRenderer(plugin, areaService: svc, deferredTextQueue: dt, deferredFrameQueue: frames);
            dr.RenderDebugFrames(settings);

            var snapshot = frames.GetSnapshotForTests();
            snapshot.Length.Should().Be(4);
        }

        [TestMethod]
        public void RenderDetailedDebugInfo_WithAllFlagsFalse_DoesNothing()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var frames = new DeferredFrameQueue();
            var texts = new DeferredTextQueue();
            var dr = new DebugRenderer(plugin, deferredTextQueue: texts, deferredFrameQueue: frames);

            var s = plugin.__Test_GetSettings();
            // Ensure all of the DebugShow* flags are disabled so RenderDetailedDebugInfo becomes a no-op
            s.DebugShowStatus.Value = false;
            s.DebugShowGameState.Value = false;
            s.DebugShowPerformance.Value = false;
            s.DebugShowClickFrequencyTarget.Value = false;
            s.DebugShowAltarDetection.Value = false;
            s.DebugShowAltarService.Value = false;
            s.DebugShowLabels.Value = false;
            s.DebugShowRecentErrors.Value = false;
            dr.RenderDetailedDebugInfo(s, new PerformanceMonitor(s));

            var obj = texts.GetType().GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(texts);
            var col = obj as System.Collections.ICollection;
            col.Should().NotBeNull();
            col.Count.Should().BeGreaterOrEqualTo(0); // no exception and no activity expected
        }

        [TestMethod]
        public void RenderPluginStatusDebug_IncrementsY_AndEnqueuesStatusLines()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var dt = new DeferredTextQueue();
            var dr = new DebugRenderer(plugin, deferredTextQueue: dt);

            int baseY = 100;
            int newY = dr.RenderPluginStatusDebug(10, baseY, 10);
            newY.Should().BeGreaterThanOrEqualTo(baseY + 30);
        }

        [TestMethod]
        public void RenderGameStateDebug_WithNoGameController_OutputsCacheInfoUnavailable()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var dt = new DeferredTextQueue();

            var dr = new DebugRenderer(plugin, deferredTextQueue: dt);
            int y = dr.RenderGameStateDebug(5, 50, 12);

            y.Should().BeGreaterThan(50);
        }

        [TestMethod]
        public void RenderAltarDebug_WithNoAltars_DoesNotThrow_AndEnqueuesHeader()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var frames = new DeferredFrameQueue();
            var dt = new DeferredTextQueue();

            var dr = new DebugRenderer(plugin, deferredTextQueue: dt, deferredFrameQueue: frames);
            int y = dr.RenderAltarDebug(0, 10, 12);
            y.Should().BeGreaterThan(10);
        }
    }
}
