using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Reflection;
using ClickIt.Rendering;
using ClickIt.Utils;
using ClickIt.Services;
using ClickIt.Tests.TestUtils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarDisplayRendererRenderComponentsTests
    {
        private static object CreateRendererAndInject(out DeferredTextQueue dtq, out DeferredFrameQueue dfq, AltarService? altarService = null)
        {
            var type = typeof(AltarDisplayRenderer);
            var inst = RuntimeHelpers.GetUninitializedObject(type);

            dtq = new DeferredTextQueue();
            dfq = new DeferredFrameQueue();

            // inject minimal required fields
            type.GetField("_deferredTextQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dtq);
            type.GetField("_deferredFrameQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, dfq);

            // Provide a weight calculator (unused in some tests)
            var wc = new WeightCalculator(new ClickItSettings());
            type.GetField("_weightCalculator", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, wc);

            // optional altar service
            type.GetField("_altarService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(inst, altarService);

            return inst!;
        }

        [TestMethod]
        public void RenderAltarComponents_WithNullService_DoesNotThrow_OrEnqueue()
        {
            var renderer = CreateRendererAndInject(out var dtq, out var dfq, null);

            // Should be safe to call when there's no altar service
            var mi = renderer.GetType().GetMethod("RenderAltarComponents", BindingFlags.Public | BindingFlags.Instance);
            mi.Should().NotBeNull();
            mi.Invoke(renderer, null);

            // No frames/text should've been enqueued
            var itemsField = typeof(DeferredFrameQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var frames = (System.Collections.ICollection)itemsField.GetValue(dfq)!;
            frames.Count.Should().Be(0);

            var tField = typeof(DeferredTextQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var texts = (System.Collections.ICollection)tField.GetValue(dtq)!;
            texts.Count.Should().Be(0);
        }

        [TestMethod]
        public void RenderSingleAltar_EarlyReturn_WhenComponentNotValid()
        {
            var renderer = CreateRendererAndInject(out _, out var dfq);

            // Make a primary component with null elements (invalid) - the renderer should early return
            var primary = TestBuilders.BuildPrimary();

            // Ensure IsValidCached returns false by leaving elements null
            var mi = renderer.GetType().GetMethod("RenderSingleAltar", BindingFlags.Public | BindingFlags.Instance);
            mi.Should().NotBeNull();

            // call - should not throw
            mi.Invoke(renderer, new object[] { primary, false, false, false, Vector2.Zero });

            // still no enqueues
            var itemsField = typeof(DeferredFrameQueue).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var frames = (System.Collections.ICollection)itemsField.GetValue(dfq)!;
            frames.Count.Should().Be(0);
        }
    }
}
