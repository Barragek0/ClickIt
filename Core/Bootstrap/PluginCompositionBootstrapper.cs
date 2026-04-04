namespace ClickIt.Core.Bootstrap
{
    internal static class PluginCompositionBootstrapper
    {
        internal static void InitializeCompositionRoot(PluginContext context, ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            context.ServiceRegistry.Reset();
            context.Runtime.IsShuttingDown = false;
            context.DebugTelemetry.Clear();
            context.SetGameControllerProvider(() => owner.GameController);
            context.SetSettingsProvider(owner.GetEffectiveSettingsForLifecycle);

            CoreDomainServices core = CoreDomainAssembler.Assemble(owner, settings, owner.GameController
                ?? throw new InvalidOperationException("GameController is null during plugin initialization."));
            RenderingDomainServices rendering = RenderingDomainAssembler.Assemble(owner, settings, owner.GameController, core);
            ClickAutomationPort clickAutomationPort = ClickDomainAssembler.Assemble(owner, settings, owner.GameController, core, rendering.AltarChoiceEvaluator);
            UltimatumRenderer ultimatumRenderer = RenderingDomainAssembler.CreateUltimatumRenderer(settings, clickAutomationPort, core.DeferredFrameQueue);
            SettingsDomainServices settingsDomain = SettingsDomainAssembler.Assemble(owner);

            ApplyPorts(context, core, rendering, clickAutomationPort, ultimatumRenderer, settingsDomain.AlertService);

            SettingsDomainAssembler.WireActions(settings, settingsDomain.EffectiveSettings, settingsDomain.AlertService, context.ServiceRegistry);
            context.ServiceRegistry.Register(() => context.Services.ErrorHandler?.UnregisterGlobalExceptionHandlers());
            context.ServiceRegistry.Register(() => context.Services.PerformanceMonitor?.ShutdownForHotReload());
            context.ServiceRegistry.Register(() => PluginRuntimeTimerCoordinator.StopAll(
                context.Runtime.LastRenderTimer,
                context.Runtime.LastTickTimer,
                context.Runtime.Timer,
                context.Runtime.SecondTimer));
        }

        internal static void FinalizeCompositionRootForStartup(PluginContext context, ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            settings.EnsureAllModsHaveWeights();

            context.Services.AlertService?.ReloadAlertSound();
            context.Services.PerformanceMonitor?.Start();

            PluginRuntimeTimerCoordinator.StartAll(
                context.Runtime.LastRenderTimer,
                context.Runtime.LastTickTimer,
                context.Runtime.Timer,
                context.Runtime.SecondTimer);
        }

        internal static void DisposeCompositionRoot(PluginContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.ServiceRegistry.DisposeAll();
            context.DebugTelemetry.Clear();
            context.SetGameControllerProvider(null);
            context.SetSettingsProvider(null);
            context.Services.Clear();
            context.Rendering.Clear();
        }

        private static void ApplyPorts(
            PluginContext context,
            CoreDomainServices core,
            RenderingDomainServices rendering,
            ClickAutomationPort clickAutomationPort,
            UltimatumRenderer ultimatumRenderer,
            AlertService alertService)
        {
            PluginServices services = context.Services;
            PluginRenderingState renderingState = context.Rendering;

            services.PerformanceMonitor = core.PerformanceMonitor;
            services.ErrorHandler = core.ErrorHandler;
            services.AreaService = core.AreaService;
            services.CachedLabels = core.CachedLabels;
            services.Camera = core.Camera;
            services.AltarService = core.AltarService;
            services.LabelFilterPort = core.LabelFilterPort;
            services.ShrineService = core.ShrineService;
            services.InputHandler = core.InputHandler;
            services.PathfindingService = core.PathfindingService;
            services.ClickAutomationPort = clickAutomationPort;
            services.AlertService = alertService;
            services.WeightCalculator = core.WeightCalculator;

            renderingState.DeferredTextQueue = core.DeferredTextQueue;
            renderingState.DeferredFrameQueue = core.DeferredFrameQueue;
            renderingState.DebugRenderer = rendering.DebugRenderer;
            renderingState.StrongboxRenderer = rendering.StrongboxRenderer;
            renderingState.LazyModeRenderer = rendering.LazyModeRenderer;
            renderingState.ClickHotkeyToggleRenderer = rendering.ClickHotkeyToggleRenderer;
            renderingState.InventoryFullWarningRenderer = rendering.InventoryFullWarningRenderer;
            renderingState.PathfindingRenderer = rendering.PathfindingRenderer;
            renderingState.AltarDisplayRenderer = rendering.AltarDisplayRenderer;
            renderingState.ClickRuntimeHost = new ClickRuntimeHost(() => services.ClickAutomationPort);
            renderingState.UltimatumRenderer = ultimatumRenderer;
        }
    }
}