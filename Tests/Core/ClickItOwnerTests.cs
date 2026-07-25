namespace ClickIt.Tests.Core
{
    [TestClass]
    [DoNotParallelize]
    public class ClickItOwnerTests
    {
        [TestMethod]
        public void OnLoad_EnablesMultithreading()
        {
            var plugin = new ClickIt();

            plugin.OnLoad();

            plugin.CanUseMultiThreading.Should().BeTrue();
        }

        [TestMethod]
        public void Initialise_WhenSettingsMissing_ThrowsInvalidOperationException()
        {
            var plugin = new ClickIt();

            Action act = () => plugin.Initialise();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Settings is null during plugin initialization.");
        }

        [TestMethod]
        public void Initialise_WhenSettingsPresent_DelegatesToLifecycleCoordinator()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            RuntimeMemberAccessor.SetRequiredMember(plugin, nameof(ClickIt.Settings), settings);

            Action act = () => plugin.Initialise();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*GameController is null during plugin initialization.*");
        }

        [TestMethod]
        public void OnClose_WithAssignedSettings_ClearsPluginContextFields_BeforeBaseSaveFailure()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            RuntimeMemberAccessor.SetRequiredMember(plugin, nameof(ClickIt.Settings), settings);
            plugin.State.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            plugin.State.Services.ErrorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            plugin.State.Services.AreaService = new AreaService();
            plugin.State.Rendering.DeferredTextQueue = new DeferredTextQueue();
            plugin.State.Rendering.DeferredFrameQueue = new DeferredFrameQueue();
            plugin.State.Rendering.AltarDisplayRenderer = (AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(AltarDisplayRenderer));
            LockManager.Instance = new LockManager(settings);

            Action act = () => plugin.OnClose();

            act.Should().Throw<NullReferenceException>();

            plugin.State.Services.PerformanceMonitor.Should().BeNull();
            plugin.State.Services.ErrorHandler.Should().BeNull();
            plugin.State.Services.AreaService.Should().BeNull();
            plugin.State.Rendering.DeferredTextQueue.Should().BeNull();
            plugin.State.Rendering.DeferredFrameQueue.Should().BeNull();
            plugin.State.Rendering.AltarDisplayRenderer.Should().BeNull();
            plugin.State.Runtime.IsShuttingDown.Should().BeTrue();
            LockManager.Instance.Should().BeNull();
        }

        [TestMethod]
        public void GetEffectiveSettingsForLifecycle_ReturnsAssignedSettings()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            RuntimeMemberAccessor.SetRequiredMember(plugin, nameof(ClickIt.Settings), settings);

            ClickItSettings effectiveSettings = plugin.GetEffectiveSettingsForLifecycle();

            effectiveSettings.Should().BeSameAs(settings);
        }

        [TestMethod]
        public void GetDebugClipboardService_ReturnsCachedInstance()
        {
            var plugin = new ClickIt();

            DebugClipboardService first = plugin.GetDebugClipboardService();
            DebugClipboardService second = plugin.GetDebugClipboardService();

            second.Should().BeSameAs(first);
        }

        [TestMethod]
        public void Render_ReturnsEarly_WhenShuttingDown()
        {
            var plugin = new ClickIt();

            plugin.State.Runtime.IsShuttingDown = true;
            plugin.State.Rendering.IsRendering = false;

            FluentActions.Invoking(plugin.Render)
                .Should().NotThrow();

            plugin.State.Rendering.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_ReturnsEarly_WhenPerformanceMonitorMissing()
        {
            var plugin = new ClickIt();

            plugin.State.Runtime.IsShuttingDown = false;
            plugin.State.Services.PerformanceMonitor = null;

            FluentActions.Invoking(plugin.Render)
                .Should().NotThrow();

            plugin.State.Rendering.IsRendering.Should().BeFalse();
        }

        [TestMethod]
        public void Render_ResetsRenderingFlag_AfterDelegatingToRenderHost()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            plugin.State.Runtime.IsShuttingDown = false;
            plugin.State.Services.PerformanceMonitor = new PerformanceMonitor(settings);

            FluentActions.Invoking(plugin.Render)
                .Should().NotThrow();

            plugin.State.Rendering.IsRendering.Should().BeFalse();
        }
    }
}