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

            CoreDomainServices core = CoreDomainAssembler.Assemble(owner, settings, owner.GameController
                ?? throw new InvalidOperationException("GameController is null during plugin initialization."));
            RenderingDomainServices rendering = RenderingDomainAssembler.Assemble(owner, settings, owner.GameController, core);
            ClickService clickAutomationPort = ClickDomainAssembler.Assemble(owner, settings, owner.GameController, core, rendering.AltarDisplayRenderer);
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
            context.FeaturePorts.Clear();
            context.OverlayPorts.Clear();
        }

        internal static void ApplyPorts(
            PluginContext context,
            CoreDomainServices core,
            RenderingDomainServices rendering,
            ClickService clickAutomationPort,
            UltimatumRenderer ultimatumRenderer,
            AlertService alertService)
        {
            PluginFeaturePorts featurePorts = context.FeaturePorts;
            PluginOverlayPorts overlayPorts = context.OverlayPorts;

            featurePorts.PerformanceMonitor = core.PerformanceMonitor;
            featurePorts.ErrorHandler = core.ErrorHandler;
            featurePorts.AreaService = core.AreaService;
            featurePorts.LabelService = core.LabelService;
            featurePorts.CachedLabels = core.CachedLabels;
            featurePorts.Camera = core.Camera;
            featurePorts.AltarService = core.AltarService;
            featurePorts.LabelFilterPort = core.LabelFilterPort;
            featurePorts.ShrineService = core.ShrineService;
            featurePorts.InputHandler = core.InputHandler;
            featurePorts.PathfindingService = core.PathfindingService;
            featurePorts.ClickAutomationPort = clickAutomationPort;
            featurePorts.AlertService = alertService;

            overlayPorts.DeferredTextQueue = core.DeferredTextQueue;
            overlayPorts.DeferredFrameQueue = core.DeferredFrameQueue;
            overlayPorts.DebugRenderer = rendering.DebugRenderer;
            overlayPorts.StrongboxRenderer = rendering.StrongboxRenderer;
            overlayPorts.LazyModeRenderer = rendering.LazyModeRenderer;
            overlayPorts.ClickHotkeyToggleRenderer = rendering.ClickHotkeyToggleRenderer;
            overlayPorts.InventoryFullWarningRenderer = rendering.InventoryFullWarningRenderer;
            overlayPorts.PathfindingRenderer = rendering.PathfindingRenderer;
            overlayPorts.AltarDisplayRenderer = rendering.AltarDisplayRenderer;
            overlayPorts.ClickRuntimeHost = new ClickRuntimeHost(() => featurePorts.ClickAutomationPort);
            overlayPorts.UltimatumRenderer = ultimatumRenderer;
        }
    }
}