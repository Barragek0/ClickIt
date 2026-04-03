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

            plugin.State.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            plugin.State.Services.ErrorHandler = new ErrorHandler(settings, (s, f) => { }, (s, f) => { });
            plugin.State.Services.AreaService = new AreaService();
            plugin.State.Rendering.DeferredTextQueue = new DeferredTextQueue();
            plugin.State.Rendering.DeferredFrameQueue = new DeferredFrameQueue();

            plugin.State.Rendering.AltarDisplayRenderer = (AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(AltarDisplayRenderer));

            LockManager.Instance = new LockManager(settings);

            plugin.OnClose();

            plugin.State.Services.PerformanceMonitor.Should().BeNull();
            plugin.State.Services.ErrorHandler.Should().BeNull();
            plugin.State.Services.AreaService.Should().BeNull();
            plugin.State.Rendering.DeferredTextQueue.Should().BeNull();
            plugin.State.Rendering.DeferredFrameQueue.Should().BeNull();
            plugin.State.Rendering.AltarDisplayRenderer.Should().BeNull();
            plugin.State.Runtime.IsShuttingDown.Should().BeTrue();

            LockManager.Instance.Should().BeNull();
        }
    }
}
