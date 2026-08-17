namespace ClickIt.Tests.Core.Lifecycle
{
    [TestClass]
    [DoNotParallelize]
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
            plugin.State.Rendering.OverlayRenderHost = new OverlayRenderHost();

            LockManager.Instance = new LockManager(settings);

            plugin.OnClose();

            plugin.State.Services.PerformanceMonitor.Should().BeNull();
            plugin.State.Services.ErrorHandler.Should().BeNull();
            plugin.State.Services.AreaService.Should().BeNull();
            plugin.State.Rendering.OverlayRenderHost.Should().BeNull();
            plugin.State.Runtime.IsShuttingDown.Should().BeTrue();

            LockManager.Instance.Should().BeNull();
        }
    }
}