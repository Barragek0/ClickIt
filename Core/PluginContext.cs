namespace ClickIt
{
    public class PluginContext
    {
        private static readonly Func<GameController?> EmptyGameControllerProvider = static () => null;
        private static readonly Func<ClickItSettings?> EmptySettingsProvider = static () => null;

        private readonly PluginServiceRegistry _serviceRegistry = new();
        private readonly PluginServices _services;
        private readonly PluginRuntimeState _runtime = new();
        private readonly PluginRenderingState _rendering;
        private readonly PluginDebugTelemetryService _debugTelemetry;
        private Func<GameController?> _getGameController = EmptyGameControllerProvider;
        private Func<ClickItSettings?> _getSettings = EmptySettingsProvider;

        public PluginContext()
        {
            _services = new PluginServices();
            _rendering = new PluginRenderingState();
            _debugTelemetry = CreateDebugTelemetryService();
        }

        internal PluginServiceRegistry ServiceRegistry => _serviceRegistry;
        internal PluginDebugTelemetryService DebugTelemetry => _debugTelemetry;

        public PluginServices Services => _services;
        public PluginRuntimeState Runtime => _runtime;
        public PluginRenderingState Rendering => _rendering;

        public Random Random { get; } = new Random();

        internal DebugTelemetrySnapshot GetDebugTelemetrySnapshot()
            => _debugTelemetry.GetSnapshot();

        internal void FreezeDebugTelemetrySnapshot(string reason, int holdDurationMs)
            => _debugTelemetry.FreezeSnapshot(reason, holdDurationMs);

        internal bool TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason)
            => _debugTelemetry.TryGetFreezeState(out remainingMs, out reason);

        internal void SetGameControllerProvider(Func<GameController?>? provider)
            => _getGameController = provider ?? EmptyGameControllerProvider;

        internal void SetSettingsProvider(Func<ClickItSettings?>? provider)
            => _getSettings = provider ?? EmptySettingsProvider;

        internal void PrepareForComposition(Func<GameController?> gameControllerProvider, Func<ClickItSettings?> settingsProvider)
        {
            ResetWarmCompositionState();
            SetGameControllerProvider(gameControllerProvider);
            SetSettingsProvider(settingsProvider);
        }

        internal void ClearPublishedCompositionState()
        {
            ClearPublishedRuntimeState();
            ClearCompositionProviders();
            ClearPublishedServiceState();
        }

        private void ResetWarmCompositionState()
        {
            _serviceRegistry.Reset();
            _runtime.IsShuttingDown = false;
            _debugTelemetry.Clear();
        }

        private void ClearPublishedRuntimeState()
            => _debugTelemetry.Clear();

        private void ClearCompositionProviders()
        {
            SetGameControllerProvider(null);
            SetSettingsProvider(null);
        }

        private void ClearPublishedServiceState()
        {
            _services.Clear();
            _rendering.Clear();
        }

        private PluginDebugTelemetryService CreateDebugTelemetryService()
            => new(
                () => _services.ClickAutomationPort,
                () => _services.ClickAutomationSupport,
                () => _services.LabelDebugService,
                () => _services.LazyModeBlockerService,
                () => _services.InventoryProbeService,
                () => _services.PathfindingService,
                () => _services.AltarService,
                () => _services.WeightCalculator,
                () => _rendering,
                () => _getGameController(),
                () => _services.InputHandler,
                () => _getSettings(),
                () => _services.CachedLabels,
                () => _services.ErrorHandler);

        internal void InitializeCompositionRoot(ClickIt owner, ClickItSettings settings)
            => PluginCompositionBootstrapper.InitializeCompositionRoot(this, owner, settings);

        internal void FinalizeCompositionRootForStartup(ClickIt owner, ClickItSettings settings)
            => PluginCompositionBootstrapper.FinalizeCompositionRootForStartup(this, owner, settings);

        internal void DisposeCompositionRoot()
            => PluginCompositionBootstrapper.DisposeCompositionRoot(this);
    }
}