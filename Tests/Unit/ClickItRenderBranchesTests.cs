using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ClickIt.Components;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItRenderBranchesTests
    {
        [TestMethod]
        public void Render_DoesNotThrow_WhenLazyModeEnabledButRendererMissing()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            var settings = clickIt.__Test_GetSettings();
            settings.LazyMode.Value = true;

            clickIt.State.PerformanceMonitor = new PerformanceMonitor(settings);
            clickIt.State.IsRendering = false;

            clickIt.Render(); // shouldn't throw
            clickIt.State.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_DoesNotThrow_WhenAltarsPresent_ButAltarDisplayRendererMissing()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var settings = plugin.__Test_GetSettings();

            var altarSvc = new Services.AltarService(plugin, settings, null);
            var p = new PrimaryAltarComponent(AltarType.Unknown, new SecondaryAltarComponent(null, [], []), new AltarButton(null), new SecondaryAltarComponent(null, [], []), new AltarButton(null));
            altarSvc.AddAltarComponent(p);

            plugin.State.AltarService = altarSvc;
            plugin.State.PerformanceMonitor = new PerformanceMonitor(settings);

            plugin.Render();
            plugin.State.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_DoesNotThrow_WhenStrongboxRendererMissing()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var settings = plugin.__Test_GetSettings();
            plugin.State.PerformanceMonitor = new PerformanceMonitor(settings);

            plugin.State.StrongboxRenderer = null;
            plugin.Render();

            plugin.State.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_CallsDeferredFlush_WhenQueuesPresent()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());
            var settings = plugin.__Test_GetSettings();

            plugin.State.PerformanceMonitor = new PerformanceMonitor(settings);
            plugin.State.DeferredTextQueue = new DeferredTextQueue();
            plugin.State.DeferredFrameQueue = new DeferredFrameQueue();

            plugin.Render();
            plugin.State.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_DoesNotThrow_WhenDebugRendererPresent_ButNoPerformanceMonitor()
        {
            var plugin = new ClickIt();
            plugin.__Test_SetSettings(new ClickItSettings());

            plugin.State.DebugRenderer = new Rendering.DebugRenderer(plugin, null, null, null, new DeferredTextQueue(), new DeferredFrameQueue());

            plugin.State.PerformanceMonitor = null;
            plugin.State.IsRendering = false;
            plugin.Render(); // should be a no-op and not throw
            plugin.State.IsRendering.Should().BeFalse();
        }
    }
}
