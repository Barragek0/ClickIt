namespace ClickIt
{
    public class PluginContext
    {
        private static readonly Func<GameController?> EmptyGameControllerProvider = static () => null;
        private static readonly Func<ClickItSettings?> EmptySettingsProvider = static () => null;
        private Func<GameController?> _getGameController = EmptyGameControllerProvider;
        private Func<ClickItSettings?> _getSettings = EmptySettingsProvider;

        public PluginContext()
        {
            Services = new PluginServices();
            Rendering = new PluginRenderingState();
            DebugTelemetry = CreateDebugTelemetryService();
        }

        internal PluginServiceRegistry ServiceRegistry { get; } = new();
        internal PluginDebugTelemetryService DebugTelemetry { get; }

        public PluginServices Services { get; }
        public PluginRuntimeState Runtime { get; } = new();
        public PluginRenderingState Rendering { get; }

        public Random Random { get; } = new Random();

        internal DebugTelemetrySnapshot GetDebugTelemetrySnapshot()
            => DebugTelemetry.GetSnapshot();

        internal void FreezeDebugTelemetrySnapshot(string reason, int holdDurationMs)
            => DebugTelemetry.FreezeSnapshot(reason, holdDurationMs);

        internal bool TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason)
            => DebugTelemetry.TryGetFreezeState(out remainingMs, out reason);

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
            ServiceRegistry.Reset();
            Runtime.IsShuttingDown = false;
            DebugTelemetry.Clear();
        }

        private void ClearPublishedRuntimeState()
            => DebugTelemetry.Clear();

        private void ClearCompositionProviders()
        {
            SetGameControllerProvider(null);
            SetSettingsProvider(null);
        }

        private void ClearPublishedServiceState()
        {
            Services.Clear();
            Rendering.Clear();
        }

        private PluginDebugTelemetryService CreateDebugTelemetryService()
            => new(
                () => Services.ClickAutomationPort,
                () => Services.ClickAutomationSupport,
                () => Services.LabelDebugService,
                () => Services.LazyModeBlockerService,
                () => Services.InventoryProbeService,
                () => Services.PathfindingService,
                () => Services.AltarService,
                () => Services.WeightCalculator,
                () => Rendering,
                () => _getGameController(),
                () => Services.InputHandler,
                () => _getSettings(),
                () => Services.CachedLabels,
                () => Services.ErrorHandler);

        internal void InitializeCompositionRoot(ClickIt owner, ClickItSettings settings)
            => PluginCompositionBootstrapper.InitializeCompositionRoot(this, owner, settings);

        internal void FinalizeCompositionRootForStartup(ClickIt owner, ClickItSettings settings)
            => PluginCompositionBootstrapper.FinalizeCompositionRootForStartup(this, owner, settings);

        internal void DisposeCompositionRoot()
            => PluginCompositionBootstrapper.DisposeCompositionRoot(this);
    }
}