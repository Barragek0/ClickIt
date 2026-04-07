namespace ClickIt.Tests.Core.Bootstrap
{
    [TestClass]
    public class PluginCompositionBootstrapperTests
    {
        [TestMethod]
        public void ApplyPorts_PublishesCurrentDirectServices_AndRenderingSlots()
        {
            var settings = new ClickItSettings();
            var context = new PluginContext();
            CoreDomainServices core = CreateCoreDomainServices(settings);
            RenderingDomainServices rendering = CreateRenderingDomainServices();
            ClickAutomationPort clickAutomationPort = CreateClickAutomationPort(settings, core);
            UltimatumRenderer ultimatumRenderer = CreateOpaque<UltimatumRenderer>();
            AlertService alertService = CreateOpaque<AlertService>();

            InvokeApplyPorts(context, core, rendering, clickAutomationPort, ultimatumRenderer, alertService);

            context.Services.PerformanceMonitor.Should().BeSameAs(core.PerformanceMonitor);
            context.Services.ErrorHandler.Should().BeSameAs(core.ErrorHandler);
            context.Services.AreaService.Should().BeSameAs(core.AreaService);
            context.Services.CachedLabels.Should().BeSameAs(core.CachedLabels);
            context.Services.Camera.Should().BeSameAs(core.Camera);
            context.Services.AltarService.Should().BeSameAs(core.AltarService);
            context.Services.LabelFilterPort.Should().BeSameAs(core.LabelFilterPort);
            context.Services.LabelDebugService.Should().BeSameAs(core.LabelDebugService);
            context.Services.LazyModeBlockerService.Should().BeSameAs(core.LazyModeBlockerService);
            context.Services.InventoryProbeService.Should().BeSameAs(core.InventoryProbeService);
            context.Services.InventoryInteractionPolicy.Should().BeSameAs(core.InventoryInteractionPolicy);
            context.Services.ClickAutomationPort.Should().BeSameAs(clickAutomationPort);
            context.Services.ClickAutomationSupport.Should().BeSameAs(clickAutomationPort.ClickAutomationSupport);
            context.Services.LockedInteractionDispatcher.Should().BeSameAs(clickAutomationPort.LockedInteractionDispatcher);
            context.Services.ShrineService.Should().BeSameAs(core.ShrineService);
            context.Services.InputHandler.Should().BeSameAs(core.InputHandler);
            context.Services.PathfindingService.Should().BeSameAs(core.PathfindingService);
            context.Services.AlertService.Should().BeSameAs(alertService);
            context.Services.WeightCalculator.Should().BeSameAs(core.WeightCalculator);
            context.Rendering.DeferredTextQueue.Should().BeSameAs(core.DeferredTextQueue);
            context.Rendering.DeferredFrameQueue.Should().BeSameAs(core.DeferredFrameQueue);
            context.Rendering.DebugRenderer.Should().BeSameAs(rendering.DebugRenderer);
            context.Rendering.StrongboxRenderer.Should().BeSameAs(rendering.StrongboxRenderer);
            context.Rendering.LazyModeRenderer.Should().BeSameAs(rendering.LazyModeRenderer);
            context.Rendering.ClickHotkeyToggleRenderer.Should().BeSameAs(rendering.ClickHotkeyToggleRenderer);
            context.Rendering.InventoryFullWarningRenderer.Should().BeSameAs(rendering.InventoryFullWarningRenderer);
            context.Rendering.PathfindingRenderer.Should().BeSameAs(rendering.PathfindingRenderer);
            context.Rendering.AltarDisplayRenderer.Should().BeSameAs(rendering.AltarDisplayRenderer);
            context.Rendering.UltimatumRenderer.Should().BeSameAs(ultimatumRenderer);
            context.Rendering.ClickRuntimeHost.Should().NotBeNull();
        }

        [TestMethod]
        public void DisposeCompositionRoot_ClearsProviders_DebugTelemetry_AndPublishedSlots()
        {
            var settings = new ClickItSettings();
            var context = new PluginContext();
            GameController gameController = ExileCoreOpaqueFactory.CreateOpaqueGameController();
            bool registryDisposed = false;
            context.FreezeDebugTelemetrySnapshot("composition-hold", 1000);
            context.SetGameControllerProvider(() => gameController);
            context.SetSettingsProvider(() => settings);
            context.ServiceRegistry.Register(() => registryDisposed = true);
            context.Services.ErrorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            context.Services.PathfindingService = CreateOpaque<PathfindingService>();
            context.Services.AlertService = CreateOpaque<AlertService>();
            context.Rendering.DebugRenderer = CreateOpaque<DebugRenderer>();
            context.Rendering.DeferredTextQueue = new DeferredTextQueue();
            context.Rendering.DeferredFrameQueue = new DeferredFrameQueue();
            context.Rendering.ClickRuntimeHost = new ClickRuntimeHost(() => context.Services.ClickAutomationPort);
            context.Rendering.IsRendering = true;

            context.TryGetDebugTelemetryFreezeState(out _, out _).Should().BeTrue();

            PluginCompositionBootstrapper.DisposeCompositionRoot(context);

            registryDisposed.Should().BeTrue();
            context.Services.ErrorHandler.Should().BeNull();
            context.Services.PathfindingService.Should().BeNull();
            context.Services.AlertService.Should().BeNull();
            context.Rendering.DebugRenderer.Should().BeNull();
            context.Rendering.DeferredTextQueue.Should().BeNull();
            context.Rendering.DeferredFrameQueue.Should().BeNull();
            context.Rendering.ClickRuntimeHost.Should().BeNull();
            context.Rendering.IsRendering.Should().BeFalse();
            context.GetDebugTelemetrySnapshot().Status.GameControllerAvailable.Should().BeFalse();
            context.TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason).Should().BeFalse();
            remainingMs.Should().Be(0);
            reason.Should().BeEmpty();
        }

        [TestMethod]
        public void InitializeCompositionRoot_WhenGameControllerMissing_ResetsWarmStateBeforeThrowing()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            var context = new PluginContext();
            bool disposed = false;

            context.ServiceRegistry.Register(() => disposed = true);
            context.Runtime.IsShuttingDown = true;
            context.FreezeDebugTelemetrySnapshot("startup-hold", 1000);

            Action act = () => PluginCompositionBootstrapper.InitializeCompositionRoot(context, plugin, settings);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*GameController is null during plugin initialization.*");
            context.Runtime.IsShuttingDown.Should().BeFalse();
            context.TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason).Should().BeFalse();
            remainingMs.Should().Be(0);
            reason.Should().BeEmpty();

            PluginCompositionBootstrapper.DisposeCompositionRoot(context);

            disposed.Should().BeFalse();
        }

        [TestMethod]
        public void FinalizeCompositionRootForStartup_NormalizesWeights_ReloadsAlertSound_AndStartsTimers()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            var context = new PluginContext();
            var performanceMonitor = new PerformanceMonitor(settings);
            string divineKey = ClickItSettings.BuildCompositeKey(
                ClickItSettings.AltarTypeMinion,
                "#% chance to drop an additional Divine Orb");
            string configDir = Path.Combine(Path.GetTempPath(), "clickit_bootstrap_" + Guid.NewGuid().ToString("N"));
            string alertPath = Path.Combine(configDir, "alert.wav");

            Directory.CreateDirectory(configDir);

            try
            {
                File.WriteAllText(alertPath, "empty");
                settings.ModAlerts.Remove(divineKey);
                context.Services.PerformanceMonitor = performanceMonitor;
                context.Services.AlertService = new AlertService(
                    () => settings,
                    () => settings,
                    () => configDir,
                    () => null,
                    static (_, _) => { },
                    static (_, _) => { });

                PluginCompositionBootstrapper.FinalizeCompositionRootForStartup(context, plugin, settings);
                Thread.Sleep(10);

                settings.ModAlerts.Should().ContainKey(divineKey);
                settings.ModAlerts[divineKey].Should().BeTrue();
                context.Services.AlertService!.CurrentAlertSoundPath.Should().Be(alertPath);
                context.Runtime.LastRenderTimer.IsRunning.Should().BeTrue();
                context.Runtime.LastTickTimer.IsRunning.Should().BeTrue();
                context.Runtime.Timer.IsRunning.Should().BeTrue();
                context.Runtime.SecondTimer.IsRunning.Should().BeTrue();
                performanceMonitor.ShouldTriggerMainTimerAction(0).Should().BeTrue();
            }
            finally
            {
                try
                {
                    Directory.Delete(configDir, true);
                }
                catch
                {
                }
            }
        }

        private static void InvokeApplyPorts(
            PluginContext context,
            CoreDomainServices core,
            RenderingDomainServices rendering,
            ClickAutomationPort clickAutomationPort,
            UltimatumRenderer ultimatumRenderer,
            AlertService alertService)
        {
            MethodInfo method = typeof(PluginCompositionBootstrapper).GetMethod("ApplyPorts", BindingFlags.NonPublic | BindingFlags.Static)!;
            method.Invoke(null, [context, core, rendering, clickAutomationPort, ultimatumRenderer, alertService]);
        }

        private static CoreDomainServices CreateCoreDomainServices(ClickItSettings settings)
        {
            var performanceMonitor = new PerformanceMonitor(settings);
            var errorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });

            return new CoreDomainServices(
                PerformanceMonitor: performanceMonitor,
                ErrorHandler: errorHandler,
                AreaService: new AreaService(),
                LabelReadModelService: CreateOpaque<LabelReadModelService>(),
                CachedLabels: new TimeCache<List<LabelOnGround>>(() => [], 50),
                Camera: CreateOpaque<Camera>(),
                AltarService: CreateOpaque<AltarService>(),
                LabelFilterPort: CreateOpaque<LabelFilterPort>(),
                LabelDebugService: CreateOpaque<LabelDebugService>(),
                LazyModeBlockerService: CreateOpaque<LazyModeBlockerService>(),
                InventoryProbeService: CreateOpaque<InventoryProbeService>(),
                InventoryInteractionPolicy: CreateOpaque<InventoryInteractionPolicy>(),
                ShrineService: CreateOpaque<ShrineService>(),
                InputHandler: new InputHandler(settings),
                PathfindingService: CreateOpaque<PathfindingService>(),
                WeightCalculator: CreateOpaque<WeightCalculator>(),
                DeferredTextQueue: new DeferredTextQueue(),
                DeferredFrameQueue: new DeferredFrameQueue());
        }

        private static RenderingDomainServices CreateRenderingDomainServices()
        {
            return new RenderingDomainServices(
                DebugRenderer: CreateOpaque<DebugRenderer>(),
                StrongboxRenderer: CreateOpaque<StrongboxRenderer>(),
                LazyModeRenderer: CreateOpaque<LazyModeRenderer>(),
                ClickHotkeyToggleRenderer: CreateOpaque<ClickHotkeyToggleRenderer>(),
                InventoryFullWarningRenderer: CreateOpaque<InventoryFullWarningRenderer>(),
                PathfindingRenderer: CreateOpaque<PathfindingRenderer>(),
                AltarChoiceEvaluator: CreateOpaque<AltarChoiceEvaluator>(),
                AltarDisplayRenderer: CreateOpaque<AltarDisplayRenderer>());
        }

        private static ClickAutomationPort CreateClickAutomationPort(ClickItSettings settings, CoreDomainServices core)
        {
            return new ClickAutomationPort(
                settings,
                ExileCoreOpaqueFactory.CreateOpaqueGameController(),
                core.ErrorHandler,
                core.AltarService,
                new WeightCalculator(settings),
                CreateOpaque<AltarChoiceEvaluator>(),
                static (_, _) => true,
                static (_, _) => true,
                core.InputHandler,
                ClickTestServiceFactory.CreateNoOpLabelInteractionPort(),
                CreateOpaque<ShrineService>(),
                CreateOpaque<PathfindingService>(),
                static () => false,
                core.CachedLabels,
                core.PerformanceMonitor,
                freezeDebugTelemetrySnapshot: null);
        }

        private static T CreateOpaque<T>() where T : class
            => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
}