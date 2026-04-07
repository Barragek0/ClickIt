namespace ClickIt.Core.Runtime
{
    internal sealed class PluginDebugTelemetryService(
        Func<ClickAutomationPort?> getClickAutomationPort,
        Func<ClickAutomationSupport?> getClickAutomationSupport,
        Func<LabelDebugService?> getLabelDebugService,
        Func<LazyModeBlockerService?> getLazyModeBlockerService,
        Func<InventoryProbeService?> getInventoryProbeService,
        Func<PathfindingService?> getPathfindingService,
        Func<AltarService?> getAltarService,
        Func<WeightCalculator?> getWeightCalculator,
        Func<PluginRenderingState?> getRenderingState,
        Func<GameController?> getGameController,
        Func<InputHandler?> getInputHandler,
        Func<ClickItSettings?> getSettings,
        Func<TimeCache<List<LabelOnGround>>?> getCachedLabels,
        Func<ErrorHandler?> getErrorHandler)
    {
        private readonly Func<ClickAutomationPort?> _getClickAutomationPort = getClickAutomationPort;
        private readonly Func<ClickAutomationSupport?> _getClickAutomationSupport = getClickAutomationSupport;
        private readonly Func<LabelDebugService?> _getLabelDebugService = getLabelDebugService;
        private readonly Func<LazyModeBlockerService?> _getLazyModeBlockerService = getLazyModeBlockerService;
        private readonly Func<InventoryProbeService?> _getInventoryProbeService = getInventoryProbeService;
        private readonly Func<PathfindingService?> _getPathfindingService = getPathfindingService;
        private readonly Func<AltarService?> _getAltarService = getAltarService;
        private readonly Func<WeightCalculator?> _getWeightCalculator = getWeightCalculator;
        private readonly Func<PluginRenderingState?> _getRenderingState = getRenderingState;
        private readonly Func<GameController?> _getGameController = getGameController;
        private readonly Func<InputHandler?> _getInputHandler = getInputHandler;
        private readonly Func<ClickItSettings?> _getSettings = getSettings;
        private readonly Func<TimeCache<List<LabelOnGround>>?> _getCachedLabels = getCachedLabels;
        private readonly Func<ErrorHandler?> _getErrorHandler = getErrorHandler;
        private readonly DebugTelemetryFreezeState _freezeState = new();

        internal DebugTelemetrySnapshot GetSnapshot()
        {
            if (_freezeState.TryGetFrozenSnapshot(Environment.TickCount64, out DebugTelemetrySnapshot frozenSnapshot))
                return frozenSnapshot;

            return BuildCurrentSnapshot();
        }

        internal void FreezeSnapshot(string reason, int holdDurationMs)
        {
            DebugTelemetrySnapshot snapshot = BuildCurrentSnapshot();
            _freezeState.Freeze(snapshot, reason, holdDurationMs, Environment.TickCount64);
        }

        internal bool TryGetFreezeState(out long remainingMs, out string reason)
            => _freezeState.TryGetFreezeState(Environment.TickCount64, out remainingMs, out reason);

        internal void Clear()
            => _freezeState.Clear();

        private DebugTelemetrySnapshot BuildCurrentSnapshot()
            => DebugTelemetryProjection.Build(
                _getClickAutomationPort(),
                _getClickAutomationSupport(),
                _getLabelDebugService(),
                _getLazyModeBlockerService(),
                _getInventoryProbeService(),
                _getPathfindingService(),
                _getAltarService(),
                _getWeightCalculator(),
                _getRenderingState(),
                _getGameController(),
                _getInputHandler(),
                _getSettings(),
                _getCachedLabels(),
                _getErrorHandler());
    }
}