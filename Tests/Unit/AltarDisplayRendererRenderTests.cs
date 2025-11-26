using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Reflection;
using SharpDX;
using ClickIt.Utils;
using ClickIt.Tests.TestUtils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererRenderTests
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
            // Some draw helper paths short-circuit when graphics is null - set a non-null placeholder so tests can exercise text/frame enqueue logic
            var fakeGraphics = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.Graphics));
            type.GetField("_graphics", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, fakeGraphics);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);
            type.GetField("_logMessage", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, new System.Action<string, int>((s, f) => { }));

            return inst!;
        }

        [TestMethod]
        public void DrawWeightTexts_EnqueuesExpectedText()
        {
            var renderer = CreateRendererWithDefaults(out var dtq, out _);

            var mi = renderer.GetType().GetMethod("DrawWeightTexts", BindingFlags.Public | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var aw = new AltarWeights();
            aw.InitializeFromArrays(new decimal[] {1,2,3,4,5,6,7,8}, new decimal[] {1,1,1,1,1,1,1,1}, new decimal[] {2,2,2,2,2,2,2,2}, new decimal[] {3,3,3,3,3,3,3,3});
            aw.TopUpsideWeight = 16m;
            aw.TopDownsideWeight = 8m;
            aw.BottomUpsideWeight = 16m;
            aw.BottomDownsideWeight = 8m;
            aw.TopWeight = 2m;
            aw.BottomWeight = 2m;

            mi.Invoke(renderer, [aw, new Vector2(0,0), new Vector2(10,10)]);

            // inspect deferred text queue
            var field = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var items = (System.Collections.ICollection)field.GetValue(dtq)!;
            items.Count.Should().BeGreaterThan(0);

            // Ensure some weight labels were enqueued
            bool hasUpside = false;
            foreach (var t in (System.Collections.IEnumerable)items)
            {
                if (t.ToString().Contains("Upside")) hasUpside = true;
            }
            hasUpside.Should().BeTrue();
        }

        [TestMethod]
        public void DrawUnrecognizedAndFailedText_EnqueuesWhenAppropriate()
        {
            var renderer = CreateRendererWithDefaults(out var dtq, out _);

            var drawUnrec = renderer.GetType().GetMethod("DrawUnrecognizedWeightText", BindingFlags.NonPublic | BindingFlags.Instance);
            var drawFail = renderer.GetType().GetMethod("DrawFailedToMatchModText", BindingFlags.NonPublic | BindingFlags.Instance);
            drawUnrec.Should().NotBeNull();
            drawFail.Should().NotBeNull();

            // Call with empty mods -> should not add anything
            drawUnrec.Invoke(renderer, ["Top upside", new string[] { }, new Vector2(1,1)]);
            var itemsField = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var items = (System.Collections.ICollection)itemsField.GetValue(dtq)!;
            int before = items.Count;

            // Call with some mods -> should enqueue explanatory text
            drawUnrec.Invoke(renderer, ["Top upside", new string[] { "modA", "modB" }, new Vector2(1,1)]);
            items = (System.Collections.ICollection)itemsField.GetValue(dtq)!;
            items.Count.Should().BeGreaterThan(before);

            // Now call failed-to-match
            drawFail.Invoke(renderer, [new Vector2(2,2)]);
            items = (System.Collections.ICollection)itemsField.GetValue(dtq)!;
            bool foundFailed = false;
            foreach (var x in (System.Collections.IEnumerable)items)
            {
                if (x.ToString().Contains("Failed to match mod")) foundFailed = true;
            }
            foundFailed.Should().BeTrue();
        }

        [TestMethod]
        public void DrawRedAndYellowFrames_EnqueuesToFrameQueue()
        {
            var renderer = CreateRendererWithDefaults(out _, out var dfq);

            var drawRed = renderer.GetType().GetMethod("DrawRedFrames", BindingFlags.NonPublic | BindingFlags.Instance);
            var drawYellow = renderer.GetType().GetMethod("DrawYellowFrames", BindingFlags.NonPublic | BindingFlags.Instance);
            drawRed.Should().NotBeNull();
            drawYellow.Should().NotBeNull();

            var top = new RectangleF(0, 0, 10, 10);
            var bottom = new RectangleF(0, 0, 20, 20);

            var framesField = typeof(DeferredFrameQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var before = ((System.Collections.ICollection)framesField.GetValue(dfq)!).Count;

            drawRed.Invoke(renderer, [top, bottom]);
            var after = ((System.Collections.ICollection)framesField.GetValue(dfq)!).Count;
            after.Should().BeGreaterThan(before);

            drawYellow.Invoke(renderer, [top, bottom]);
            var after2 = ((System.Collections.ICollection)framesField.GetValue(dfq)!).Count;
            after2.Should().BeGreaterThan(after);
        }

        [TestMethod]
        public void DetermineAltarChoice_InvalidRectangles_EnqueuesMessage_AndReturnsNull()
        {
            var renderer = CreateRendererWithDefaults(out var dtq, out _);
            var method = renderer.GetType().GetMethod("DetermineAltarChoice", BindingFlags.Public | BindingFlags.Instance)!
                ;

            var primary = TestBuilders.BuildPrimary();
            var aw = new AltarWeights();

            var res = method.Invoke(renderer, [primary, aw, new RectangleF(0,0,0,0), new RectangleF(0,0,0,0), new Vector2(0,0)]);
            res.Should().BeNull();

            var field = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var items = (System.Collections.ICollection)field.GetValue(dtq)!;
            bool found = false;
            foreach (var x in (System.Collections.IEnumerable)items)
            {
                if (x.ToString().Contains("Invalid altar rectangles detected")) found = true;
            }
            found.Should().BeTrue();
        }
    }
}
