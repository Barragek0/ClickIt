namespace ClickIt.Core.Bootstrap
{
    internal static class PluginCompositionBootstrapper
    {
        private readonly record struct CompositionRootParts(
            CoreDomainServices Core,
            RenderingDomainServices Rendering,
            ClickAutomationPort ClickAutomationPort,
            SettingsDomainServices SettingsDomain);

        internal static void InitializeCompositionRoot(PluginContext context, ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            context.PrepareForComposition(() => owner.GameController, owner.GetEffectiveSettingsForLifecycle);

            CompositionRootParts parts = AssembleCompositionParts(owner, settings);
            PublishCompositionState(context, settings, parts);
            RegisterCompositionShutdownActions(context);
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
            context.ClearPublishedCompositionState();
        }

        private static CompositionRootParts AssembleCompositionParts(ClickIt owner, ClickItSettings settings)
        {
            GameController gameController = owner.GameController
                ?? throw new InvalidOperationException("GameController is null during plugin initialization.");
            CoreDomainServices core = CoreDomainAssembler.Assemble(owner, settings, gameController);
            RenderingDomainServices rendering = RenderingDomainAssembler.Assemble(owner, settings, core);
            ClickAutomationPort clickAutomationPort = ClickDomainAssembler.Assemble(owner, settings, gameController, core, rendering.AltarChoiceEvaluator);
            UltimatumOverlay ultimatumOverlay = RenderingDomainAssembler.CreateUltimatumOverlay(clickAutomationPort);
            rendering.OverlayRenderHost.Register(ultimatumOverlay);
            SettingsDomainServices settingsDomain = SettingsDomainAssembler.Assemble(owner);
            return new CompositionRootParts(core, rendering, clickAutomationPort, settingsDomain);
        }

        private static void PublishCompositionState(PluginContext context, ClickItSettings settings, CompositionRootParts parts)
        {
            ApplyPorts(
                context,
                parts.Core,
                parts.Rendering,
                parts.ClickAutomationPort,
                parts.SettingsDomain.AlertService);

            SettingsDomainAssembler.WireActions(
                settings,
                parts.SettingsDomain.EffectiveSettings,
                parts.SettingsDomain.AlertService,
                context.ServiceRegistry);
        }

        private static void RegisterCompositionShutdownActions(PluginContext context)
        {
            context.ServiceRegistry.Register(() => context.Services.ErrorHandler?.UnregisterGlobalExceptionHandlers());
            context.ServiceRegistry.Register(() => context.Services.PerformanceMonitor?.ShutdownForHotReload());
            context.ServiceRegistry.Register(() => PluginRuntimeTimerCoordinator.StopAll(
                context.Runtime.LastRenderTimer,
                context.Runtime.LastTickTimer,
                context.Runtime.Timer,
                context.Runtime.SecondTimer));
            // Unhook the live GameController entity events so the disposed blight cache's handler (a per-entity path read) stops running on every EntityAdded after a reload.
            context.ServiceRegistry.Register(() => context.Services.BlightService?.DisposeForShutdown());
        }

        private static void ApplyPorts(
            PluginContext context,
            CoreDomainServices core,
            RenderingDomainServices rendering,
            ClickAutomationPort clickAutomationPort,
            AlertService alertService)
        {
            PluginServices services = context.Services;

            PublishCoreServices(services, core);
            PublishClickServices(services, clickAutomationPort, alertService, core.WeightCalculator);
            PublishRenderingState(context.Rendering, core, rendering);
        }

        private static void PublishCoreServices(PluginServices services, CoreDomainServices core)
        {
            services.PerformanceMonitor = core.PerformanceMonitor;
            services.ErrorHandler = core.ErrorHandler;
            services.AreaService = core.AreaService;
            services.CachedLabels = core.CachedLabels;
            services.Camera = core.Camera;
            services.AltarService = core.AltarService;
            services.LabelFilterPort = core.LabelFilterPort;
            services.LabelDebugService = core.LabelDebugService;
            services.LazyModeBlockerService = core.LazyModeBlockerService;
            services.InventoryProbeService = core.InventoryProbeService;
            services.InventoryInteractionPolicy = core.InventoryInteractionPolicy;
            services.ShrineService = core.ShrineService;
            services.InputHandler = core.InputHandler;
            services.PathfindingService = core.PathfindingService;
            services.HarvestService = core.HarvestService;
            services.BlightService = core.BlightService;
        }

        private static void PublishClickServices(
            PluginServices services,
            ClickAutomationPort clickAutomationPort,
            AlertService alertService,
            WeightCalculator weightCalculator)
        {
            services.ClickAutomationPort = clickAutomationPort;
            services.ClickAutomationSupport = clickAutomationPort.ClickAutomationSupport;
            services.LockedInteractionDispatcher = clickAutomationPort.LockedInteractionDispatcher;
            services.AlertService = alertService;
            services.WeightCalculator = weightCalculator;

            // Wire the dedicated harvest click path (altar pattern).
            if (services.HarvestService != null)
                clickAutomationPort.GetHarvestLabelToClick
                    = () => services.HarvestService.GetLabelToClick();

            if (services.BlightService != null)
            {
                clickAutomationPort.TryProgressBlightBuilding
                    = () =>
                    {
                        BlightBuildAction action = services.BlightService.TryProgressBlightBuilding(
                            services.CachedLabels?.Value);

                        // Skip non-clickable positions without resetting progress; blight menu clicks fail-closed on a stale resolved position.
                        if (action is { Kind: BlightBuildActionKind.ClickPosition } blightClick
                            && (blightClick.IsMenuClick
                                ? !clickAutomationPort.IsBlightTowerUiAt(blightClick.ClickPosition)
                                : !clickAutomationPort.PointIsInClickableArea(
                                    blightClick.ClickPosition, "blight")))
                        {
                            return new BlightBuildAction(BlightBuildActionKind.None,
                                DebugMessage: "Click position not clickable - waiting");
                        }

                        return action;
                    };
                clickAutomationPort.GetBlightPathfindTarget
                    = () => services.BlightService.GetPathfindingTargetEntity();
                clickAutomationPort.IsBlightEncounterActive
                    = () => services.BlightService.IsEncounterActive
                        && (services.BlightService.KnownTowers.Count > 0
                            || services.BlightService.TowerEntities.Count > 0);
            }
        }

        private static void PublishRenderingState(
            PluginRenderingState renderingState,
            CoreDomainServices core,
            RenderingDomainServices rendering)
        {
            renderingState.DeferredTextQueue = core.DeferredTextQueue;
            renderingState.DeferredFrameQueue = core.DeferredFrameQueue;
            renderingState.DeferredDrawQueue = core.DeferredDrawQueue;
            renderingState.ImGuiDebugOverlay = rendering.ImGuiDebugOverlay;
            renderingState.UiRegionRectangleOverlay = rendering.UiRegionRectangleOverlay;
            renderingState.OverlayRenderHost = rendering.OverlayRenderHost;
        }
    }
}
