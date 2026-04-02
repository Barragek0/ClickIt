using ClickIt.Composition;
using ClickIt.Services.Observability;

namespace ClickIt.Core.Runtime
{
    internal static class PluginLifecycleHost
    {
        internal static void InitializeCompositionRoot(PluginContext context, ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            context.ServiceRegistry.Reset();
            context.IsShuttingDown = false;
            context.DebugTelemetryFreezeState.Clear();

            ComposedServices services = ServiceCompositionRoot.Compose(owner, settings);

            PluginContextServiceStateInitializer.InitializeFromComposedServices(context, services);

            SettingsDomainAssembler.WireActions(settings, services.EffectiveSettings, services.AlertService, context.ServiceRegistry);
            context.ServiceRegistry.Register(() => context.ErrorHandler?.UnregisterGlobalExceptionHandlers());
            context.ServiceRegistry.Register(() => context.PerformanceMonitor?.ShutdownForHotReload());
            context.ServiceRegistry.Register(() => PluginRuntimeTimerCoordinator.StopAll(context.LastRenderTimer, context.LastTickTimer, context.Timer, context.SecondTimer));
        }

        internal static void FinalizeCompositionRootForStartup(PluginContext context, ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            settings.EnsureAllModsHaveWeights();

            context.AlertService?.ReloadAlertSound();
            context.PerformanceMonitor?.Start();

            PluginRuntimeTimerCoordinator.StartAll(context.LastRenderTimer, context.LastTickTimer, context.Timer, context.SecondTimer);
        }

        internal static void DisposeCompositionRoot(PluginContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.ServiceRegistry.DisposeAll();
            context.DebugTelemetryFreezeState.Clear();
            PluginContextServiceStateResetter.Reset(context);
        }
    }
}