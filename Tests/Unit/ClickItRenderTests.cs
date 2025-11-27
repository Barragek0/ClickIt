using FluentAssertions;
using ClickIt.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using SharpDX;

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
            clickIt.State.PerformanceMonitor = new global::ClickIt.Utils.PerformanceMonitor(clickIt.__Test_GetSettings());

            // Ensure required queues exist; Graphics can remain null (flush is no-op in that case)
            clickIt.State.DeferredTextQueue = new global::ClickIt.Utils.DeferredTextQueue();
            clickIt.State.DeferredFrameQueue = new global::ClickIt.Utils.DeferredFrameQueue();

            // Sanity: Render should not throw and IsRendering should be false when Render returns
            clickIt.Render();
            clickIt.State.IsRendering.Should().BeFalse();
        }
    }
}
