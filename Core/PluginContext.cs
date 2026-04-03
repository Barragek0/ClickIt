namespace ClickIt
{
    public class PluginContext
    {
        private readonly PluginServiceRegistry _serviceRegistry = new();
        private readonly PluginFeaturePorts _featurePorts = new();
        private readonly PluginServices _services;
        private readonly PluginRuntimeState _runtime = new();
        private readonly PluginOverlayPorts _overlayPorts = new();
        private readonly PluginRenderingState _rendering;
        private readonly PluginDebugTelemetryService _debugTelemetry;

        public PluginContext()
        {
            _services = new PluginServices(_featurePorts);
            _rendering = new PluginRenderingState(_overlayPorts);
            _debugTelemetry = new PluginDebugTelemetryService(
                () => _services.ClickService,
                () => _services.LabelFilterService,
                () => _services.PathfindingService);
        }

        internal PluginServiceRegistry ServiceRegistry => _serviceRegistry;
        internal PluginDebugTelemetryService DebugTelemetry => _debugTelemetry;
        internal PluginFeaturePorts FeaturePorts => _featurePorts;
        internal PluginOverlayPorts OverlayPorts => _overlayPorts;

        public PluginServices Services => _services;
        public PluginRuntimeState Runtime => _runtime;
        public PluginRenderingState Rendering => _rendering;

        public ErrorHandler? ErrorHandler { get => _services.ErrorHandler; set => _services.ErrorHandler = value; }
        public Random Random { get; } = new Random();
        public double CurrentFPS => _services.PerformanceMonitor?.CurrentFPS ?? 0;

        public Queue<long> RenderTimings => _services.PerformanceMonitor?.GetRenderTimingsSnapshot() ?? new Queue<long>();

        public IReadOnlyList<string> RecentErrors => ErrorHandler?.RecentErrors ?? [];

        internal DebugTelemetrySnapshot GetDebugTelemetrySnapshot()
            => _debugTelemetry.GetSnapshot();

        internal void FreezeDebugTelemetrySnapshot(string reason, int holdDurationMs)
            => _debugTelemetry.FreezeSnapshot(reason, holdDurationMs);

        internal bool TryGetDebugTelemetryFreezeState(out long remainingMs, out string reason)
            => _debugTelemetry.TryGetFreezeState(out remainingMs, out reason);

        public void InitializeCompositionRoot(ClickIt owner, ClickItSettings settings)
            => PluginCompositionBootstrapper.InitializeCompositionRoot(this, owner, settings);

        public void FinalizeCompositionRootForStartup(ClickIt owner, ClickItSettings settings)
            => PluginCompositionBootstrapper.FinalizeCompositionRootForStartup(this, owner, settings);

        public void DisposeCompositionRoot()
            => PluginCompositionBootstrapper.DisposeCompositionRoot(this);
    }
}