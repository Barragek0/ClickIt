using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using System.Reflection;
using SharpDX;
using ClickIt.Utils;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class DebugRendererTests
    {
        private object CreateRendererAndInject(out DeferredTextQueue dtq)
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
    }
}
