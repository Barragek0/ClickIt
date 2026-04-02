using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Core
{
    [TestClass]
    public class ClickItOnCloseTests
    {
        [TestMethod]
        public void OnClose_ClearsPluginContextFields()
        {
            var plugin = new ClickIt();

            var settings = new ClickItSettings();

            plugin.State.Services.PerformanceMonitor = new global::ClickIt.Utils.PerformanceMonitor(settings);
            plugin.State.Services.ErrorHandler = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });
            plugin.State.Services.AreaService = new global::ClickIt.Services.AreaService();
            plugin.State.Rendering.DeferredTextQueue = new global::ClickIt.Utils.DeferredTextQueue();
            plugin.State.Rendering.DeferredFrameQueue = new global::ClickIt.Utils.DeferredFrameQueue();

            plugin.State.Rendering.AltarDisplayRenderer = (global::ClickIt.Rendering.AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.AltarDisplayRenderer));

            global::ClickIt.Utils.LockManager.Instance = new global::ClickIt.Utils.LockManager(settings);

            plugin.OnClose();

            plugin.State.Services.PerformanceMonitor.Should().BeNull();
            plugin.State.Services.ErrorHandler.Should().BeNull();
            plugin.State.Services.AreaService.Should().BeNull();
            plugin.State.Rendering.DeferredTextQueue.Should().BeNull();
            plugin.State.Rendering.DeferredFrameQueue.Should().BeNull();
            plugin.State.Rendering.AltarDisplayRenderer.Should().BeNull();
            plugin.State.Runtime.IsShuttingDown.Should().BeTrue();

            global::ClickIt.Utils.LockManager.Instance.Should().BeNull();
        }
    }
}
