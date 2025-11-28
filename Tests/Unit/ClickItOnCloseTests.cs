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

            // Inject Settings via test seam so plugin internals are stable
            var settings = new ClickItSettings();
            plugin.__Test_SetSettings(settings);

            // Populate a handful of context fields with real or uninitialized objects so OnClose can clear them
            plugin.State.PerformanceMonitor = new global::ClickIt.Utils.PerformanceMonitor(settings);
            plugin.State.ErrorHandler = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (s, f) => { });
            plugin.State.AreaService = new Services.AreaService();
            plugin.State.DeferredTextQueue = new global::ClickIt.Utils.DeferredTextQueue();
            plugin.State.DeferredFrameQueue = new global::ClickIt.Utils.DeferredFrameQueue();

            // Some types are cumbersome to construct (renderers etc) — use uninitialized objects so the fields are non-null
            plugin.State.AltarDisplayRenderer = (Rendering.AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(Rendering.AltarDisplayRenderer));

            // Ensure LockManager singleton is set so OnClose should clear it
            global::ClickIt.Utils.LockManager.Instance = new global::ClickIt.Utils.LockManager(settings);

            // Ensure the backing Settings property is populated (some base implementations use a different storage)
            var settingsProp = plugin.GetType().GetProperty("Settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (settingsProp != null && settingsProp.CanWrite)
            {
                settingsProp.SetValue(plugin, settings);
            }

            // Call OnClose (should clear many state fields + LockManager.Instance)
            plugin.OnClose();

            plugin.State.PerformanceMonitor.Should().BeNull();
            plugin.State.ErrorHandler.Should().BeNull();
            plugin.State.AreaService.Should().BeNull();
            plugin.State.DeferredTextQueue.Should().BeNull();
            plugin.State.DeferredFrameQueue.Should().BeNull();
            plugin.State.AltarDisplayRenderer.Should().BeNull();

            global::ClickIt.Utils.LockManager.Instance.Should().BeNull();
        }
    }
}
