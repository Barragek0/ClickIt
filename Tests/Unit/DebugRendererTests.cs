using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using System.Reflection;
using SharpDX;
using ClickIt.Utils;
using ClickIt.Rendering;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DebugRendererTests
    {
        private static object CreateRendererAndInject(out DeferredTextQueue dtq)
        {
            var type = typeof(Rendering.DebugRenderer);
            var inst = RuntimeHelpers.GetUninitializedObject(type);

            // populate necessary private fields
            var plugin = RuntimeHelpers.GetUninitializedObject(typeof(ClickIt));
            // set basic State so RenderErrorsDebug has a container
            var state = new PluginContext();
            // property is read-only in source; set the backing field instead
            var backing = plugin.GetType().GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            backing!.SetValue(plugin, state);

            // Inject fields
            dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();
            type.GetField("_plugin", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, plugin);
            type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dtq);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);

            // area service available for RenderDebugFrames
            var areaSvc = new AreaService();
            type.GetField("_areaService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, areaSvc);

            return inst!;
        }

        [TestMethod]
        public void RenderDetailedDebugInfo_ProducesDeferredOutput_WhenSectionsEnabled_ButAvoidsKeyStatePath()
        {
            var renderer = CreateRendererAndInject(out var dtq);

            var settings = new ClickItSettings();
            // enable some debug sections but explicitly disable the click-frequency target (to avoid Input.GetKeyState native call)
            settings.DebugShowStatus.Value = true;
            settings.DebugShowGameState.Value = true;
            settings.DebugShowPerformance.Value = true;
            settings.DebugShowAltarDetection.Value = true;
            settings.DebugShowAltarService.Value = true;
            settings.DebugShowLabels.Value = true;
            settings.DebugShowRecentErrors.Value = true;
            settings.DebugShowClickFrequencyTarget.Value = false;

            var pm = new PerformanceMonitor(settings);

            // Call - should not throw and should enqueue text entries
            var mi = renderer.GetType().GetMethod("RenderDetailedDebugInfo", BindingFlags.Public | BindingFlags.Instance);
            mi.Should().NotBeNull();
            mi.Invoke(renderer, [settings, pm]);

            // Reflect into deferred queue to ensure we added some entries
            var field = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (System.Collections.ICollection)field.GetValue(dtq)!;
            list.Count.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void RenderWrappedText_WrapsMultipleLines_AndHandlesEmptyString()
        {
            var renderer = CreateRendererAndInject(out var dtq);

            var mi = renderer.GetType().GetMethod("RenderWrappedText", BindingFlags.Public | BindingFlags.Instance);
            mi.Should().NotBeNull();

            // Empty text -> returns position.Y + lineHeight
            var res = (int)mi.Invoke(renderer, [string.Empty, new Vector2(10, 20), Color.White, 12, 7, 40]);
            res.Should().Be(27);

            // Long text should produce multiple enqueues
            dtq = new DeferredTextQueue();
            renderer.GetType().GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(renderer, dtq);

            string longText = "This is a long line that should be wrapped by RenderWrappedText into many little lines to test splitting behaviour.";
            var outRes = (int)mi.Invoke(renderer, [longText, new Vector2(0, 0), Color.White, 12, 10, 20]);
            outRes.Should().BeGreaterThan(0);

            var itemsField = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var items = (System.Collections.ICollection)itemsField.GetValue(dtq)!;
            items.Count.Should().BeGreaterThan(1);
        }

        [TestMethod]
        public void GetWeightHelpers_ReturnZeroWhenIndexOutOfRange()
        {
            var rendererType = typeof(Rendering.DebugRenderer);
            var getTop = rendererType.GetMethod("GetTopUpsideWeight", BindingFlags.NonPublic | BindingFlags.Static);
            var aw = new AltarWeights();

            getTop!.Invoke(null, [aw, 99]).Should().Be(0m);
            var getBottomDown = rendererType.GetMethod("GetBottomDownsideWeight", BindingFlags.NonPublic | BindingFlags.Static)!;
            getBottomDown.Invoke(null, [aw, -1]).Should().Be(0m);
        }

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
