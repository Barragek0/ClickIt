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
            context.Runtime.IsShuttingDown = false;
            context.DebugTelemetry.Clear();

            ComposedServices services = ServiceCompositionRoot.Compose(owner, settings);

            PluginContextServiceStateInitializer.InitializeFromComposedServices(context, services);

            SettingsDomainAssembler.WireActions(settings, services.EffectiveSettings, services.AlertService, context.ServiceRegistry);
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
            PluginContextServiceStateResetter.Reset(context);
        }
    }
}