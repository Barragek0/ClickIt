using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Rendering;
using SharpDX;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class StrongboxRendererRenderTests
    {
        [TestMethod]
        public void RenderFromLabels_WithEmptyList_DoesNotEnqueue()
        {
            var settings = new ClickItSettings();
            // ensure frames are enabled but pass an empty collection
            settings.ShowStrongboxFrames.Value = true;

            var queue = new DeferredFrameQueue();
            var renderer = new StrongboxRenderer(settings, queue);

            renderer.RenderFromLabels(System.Array.Empty<ExileCore.PoEMemory.Elements.LabelOnGround>(), new RectangleF(0, 0, 1000, 1000));

            var field = typeof(DeferredFrameQueue).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var frames = (System.Collections.ICollection)field.GetValue(queue)!;
            frames.Count.Should().Be(0);
        }

        [TestMethod]
        public void Render_WithNullGameController_DoesNotThrow()
        {
            var settings = new ClickItSettings();
            var queue = new DeferredFrameQueue();
            var renderer = new StrongboxRenderer(settings, queue);

            // should simply return and not throw and leave queue unchanged
            renderer.Render(null, null);
            var field = typeof(DeferredFrameQueue).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var frames = (System.Collections.ICollection)field.GetValue(queue)!;
            frames.Count.Should().Be(0);
        }
    }
}
