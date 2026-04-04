namespace ClickIt.Tests.Core.Lifecycle
{
    [TestClass]
    public class PluginLifecycleCoordinatorTests
    {
        [TestMethod]
        public void Shutdown_ClearsThreadLocalBuffers_AndAltarRuntimeCaches()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            var altarService = new AltarService(plugin, settings, null);
            plugin.State.Services.AltarService = altarService;

            LabelElementSearch.AddNullElementToThreadLocal();
            LabelElementSearch.GetThreadLocalElementsCount().Should().BeGreaterThan(0);

            List<object?> primedSkillBarBuffer = MovementSkillMath.GetThreadSkillBarEntriesBuffer(4);
            primedSkillBarBuffer.Add(new object());
            primedSkillBarBuffer.Should().HaveCount(1);

            altarService.RecordUnmatchedMod("mod1", "Players");
            altarService.DebugInfo.RecentUnmatchedMods.Should().NotBeEmpty();

            PluginLifecycleCoordinator.Shutdown(plugin, settings);

            plugin.State.Runtime.IsShuttingDown.Should().BeTrue();
            LabelElementSearch.GetThreadLocalElementsCount().Should().Be(0);

            List<object?> freshSkillBarBuffer = MovementSkillMath.GetThreadSkillBarEntriesBuffer(1);
            freshSkillBarBuffer.Should().BeEmpty();
            freshSkillBarBuffer.Should().NotBeSameAs(primedSkillBarBuffer);

            altarService.DebugInfo.RecentUnmatchedMods.Should().BeEmpty();
        }

        [TestMethod]
        public void Shutdown_DisposesCompositionRoot_AfterRuntimeCleanup()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            plugin.State.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            plugin.State.Services.ErrorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            plugin.State.Services.AreaService = new AreaService();
            plugin.State.Services.AltarService = new AltarService(plugin, settings, null);
            plugin.State.Rendering.DeferredTextQueue = new DeferredTextQueue();
            plugin.State.Rendering.DeferredFrameQueue = new DeferredFrameQueue();
            LockManager.Instance = new LockManager(settings);

            PluginLifecycleCoordinator.Shutdown(plugin, settings);

            plugin.State.Services.PerformanceMonitor.Should().BeNull();
            plugin.State.Services.ErrorHandler.Should().BeNull();
            plugin.State.Services.AreaService.Should().BeNull();
            plugin.State.Services.AltarService.Should().BeNull();
            plugin.State.Rendering.DeferredTextQueue.Should().BeNull();
            plugin.State.Rendering.DeferredFrameQueue.Should().BeNull();
            LockManager.Instance.Should().BeNull();
        }
    }
}