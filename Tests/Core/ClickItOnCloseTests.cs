using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItOnCloseTests
    {
        [TestMethod]
        public void OnClose_ClearsPluginContextFields()
        {
            var plugin = new ClickIt();

            var settings = new ClickItSettings();

            plugin.State.PerformanceMonitor = new global::ClickIt.Utils.PerformanceMonitor(settings);
            plugin.State.ErrorHandler = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });
            plugin.State.AreaService = new Services.AreaService();
            plugin.State.DeferredTextQueue = new global::ClickIt.Utils.DeferredTextQueue();
            plugin.State.DeferredFrameQueue = new global::ClickIt.Utils.DeferredFrameQueue();

            plugin.State.AltarDisplayRenderer = (global::ClickIt.Rendering.AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Rendering.AltarDisplayRenderer));

            global::ClickIt.Utils.LockManager.Instance = new global::ClickIt.Utils.LockManager(settings);

            var settingsProp = plugin.GetType().GetProperty("Settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (settingsProp != null && settingsProp.CanWrite)
            {
                settingsProp.SetValue(plugin, settings);
            }

            plugin.OnClose();

            plugin.State.PerformanceMonitor.Should().BeNull();
            plugin.State.ErrorHandler.Should().BeNull();
            plugin.State.AreaService.Should().BeNull();
            plugin.State.DeferredTextQueue.Should().BeNull();
            plugin.State.DeferredFrameQueue.Should().BeNull();
            plugin.State.AltarDisplayRenderer.Should().BeNull();
            plugin.State.IsShuttingDown.Should().BeTrue();

            global::ClickIt.Utils.LockManager.Instance.Should().BeNull();
        }
    }
}
