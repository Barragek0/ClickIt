using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Reflection;
using SharpDX;
using ClickIt.Utils;
using ClickIt.Rendering;
using ClickIt.Tests.TestUtils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererEdgeCasesTests
    {
        private static object CreateRendererWithDefaults(out DeferredTextQueue dtq, out DeferredFrameQueue dfq)
        {
            var type = typeof(Rendering.AltarDisplayRenderer);
            var inst = RuntimeHelpers.GetUninitializedObject(type);

            var settings = new ClickItSettings();
            dtq = new DeferredTextQueue();
            dfq = new DeferredFrameQueue();

            type.GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, settings);
            type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dtq);
            // set a non-null graphics object so DrawUnrecognizedWeightText enqueues when invoked
            var fakeGraphics = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.Graphics));
            type.GetField("_graphics", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, fakeGraphics);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);
            type.GetField("_logMessage", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, new System.Action<string, int>((s, f) => { }));

            return inst!;
        }

        [TestMethod]
        public void DetermineAltarChoice_BottomUpsideZero_EnqueuesUnrecognizedAndReturnsNull()
        {
            var renderer = CreateRendererWithDefaults(out var dtq, out var dfq);

            var method = renderer.GetType().GetMethod("DetermineAltarChoice", BindingFlags.Public | BindingFlags.Instance)!;

            var primary = TestBuilders.BuildPrimary(TestBuilders.BuildSecondary(new string[] { "top1" }, new string[] { "topd" }), TestBuilders.BuildSecondary(new string[] { "bottomA" }, new string[] { }));

            var aw = TestBuilders.BuildAltarWeights(topDown: new decimal[8] { 1, 1, 1, 1, 1, 1, 1, 1 }, bottomDown: new decimal[8] { 1, 1, 1, 1, 1, 1, 1, 1 }, topUp: new decimal[8] { 2, 2, 0, 0, 0, 0, 0, 0 }, bottomUp: new decimal[8] { 0, 0, 0, 0, 0, 0, 0, 0 }, topWeight: 5m, bottomWeight: 0m);

            var res = (ExileCore.PoEMemory.Element?)method.Invoke(renderer, new object[] { primary, aw, new RectangleF(0, 0, 10, 10), new RectangleF(0, 0, 10, 10), new SharpDX.Vector2(1, 1) });

            res.Should().BeNull();

            // should have enqueued warning about bottom upside
            var txtList = (System.Collections.ICollection)typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dtq)!;
            bool found = false;
            foreach (var x in (System.Collections.IEnumerable)txtList) if (x.ToString().Contains("Bottom upside")) found = true;
            found.Should().BeTrue();

            var frames = (System.Collections.ICollection)typeof(DeferredFrameQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dfq)!;
            frames.Count.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void DetermineAltarChoice_BottomDownsideZero_EnqueuesUnrecognizedAndReturnsNull()
        {
            var renderer = CreateRendererWithDefaults(out var dtq, out var dfq);
            var method = renderer.GetType().GetMethod("DetermineAltarChoice", BindingFlags.Public | BindingFlags.Instance)!;

            var primary = TestBuilders.BuildPrimary(TestBuilders.BuildSecondary(new string[] { "top1" }, new string[] { "topd" }), TestBuilders.BuildSecondary(new string[] { "bottom1" }, new string[] { "bdown" }));

            var aw = TestBuilders.BuildAltarWeights(topDown: new decimal[8] { 1, 1, 1, 1, 1, 1, 1, 1 }, bottomDown: new decimal[8] { 0, 0, 0, 0, 0, 0, 0, 0 }, topUp: new decimal[8] { 2, 2, 0, 0, 0, 0, 0, 0 }, bottomUp: new decimal[8] { 2, 2, 0, 0, 0, 0, 0, 0 }, topWeight: 5m, bottomWeight: 2m);

            var res = (ExileCore.PoEMemory.Element?)method.Invoke(renderer, new object[] { primary, aw, new RectangleF(0, 0, 10, 10), new RectangleF(0, 0, 10, 10), new SharpDX.Vector2(1, 1) });

            res.Should().BeNull();

            var txtList = (System.Collections.ICollection)typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dtq)!;
            bool found = false;
            foreach (var x in (System.Collections.IEnumerable)txtList) if (x.ToString().Contains("Bottom downside")) found = true;
            found.Should().BeTrue();

            var frames = (System.Collections.ICollection)typeof(DeferredFrameQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dfq)!;
            frames.Count.Should().BeGreaterThan(0);
        }
    }
}
