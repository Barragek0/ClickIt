namespace ClickIt.Core.Runtime
{
    internal readonly record struct PluginDebugTelemetryProjectionSources(
        Func<ClickAutomationPort?> GetClickAutomationPort,
        Func<ClickAutomationSupport?> GetClickAutomationSupport,
        Func<LabelDebugService?> GetLabelDebugService,
        Func<LazyModeBlockerService?> GetLazyModeBlockerService,
        Func<InventoryProbeService?> GetInventoryProbeService,
        Func<PathfindingService?> GetPathfindingService,
        Func<AltarService?> GetAltarService,
        Func<WeightCalculator?> GetWeightCalculator,
        Func<PluginRenderingState?> GetRenderingState,
        Func<GameController?> GetGameController,
        Func<InputHandler?> GetInputHandler,
        Func<ClickItSettings?> GetSettings,
        Func<TimeCache<List<LabelOnGround>>?> GetCachedLabels,
        Func<ErrorHandler?> GetErrorHandler);

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
        private readonly PluginDebugTelemetryProjectionSources _projectionSources = new(
            getClickAutomationPort,
            getClickAutomationSupport,
            getLabelDebugService,
            getLazyModeBlockerService,
            getInventoryProbeService,
            getPathfindingService,
            getAltarService,
            getWeightCalculator,
            getRenderingState,
            getGameController,
            getInputHandler,
            getSettings,
            getCachedLabels,
            getErrorHandler);
        private readonly DebugTelemetryFreezeState _freezeState = new();
        private DebugTelemetrySnapshot? _cachedSnapshot;
        private long _lastSnapshotBuildTimestampMs;

        // The debug overlay reads the snapshot every frame, but the projection scans live sources (hovered item, altar, inventory). Rebuilding it at most every 200ms cuts that per-frame main-thread cost ~5x while keeping the debug display plenty fresh.
        private const long SnapshotBuildIntervalMs = 200;

        internal DebugTelemetrySnapshot GetSnapshot()
        {
            if (_freezeState.TryGetFrozenSnapshot(Environment.TickCount64, out DebugTelemetrySnapshot frozenSnapshot))
                return frozenSnapshot;

            long now = Environment.TickCount64;
            if (_cachedSnapshot != null && now - _lastSnapshotBuildTimestampMs < SnapshotBuildIntervalMs)
                return _cachedSnapshot;

            _lastSnapshotBuildTimestampMs = now;
            _cachedSnapshot = BuildCurrentSnapshot();
            return _cachedSnapshot;
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
                _projectionSources.GetClickAutomationPort(),
                _projectionSources.GetClickAutomationSupport(),
                _projectionSources.GetLabelDebugService(),
                _projectionSources.GetLazyModeBlockerService(),
                _projectionSources.GetInventoryProbeService(),
                _projectionSources.GetPathfindingService(),
                _projectionSources.GetAltarService(),
                _projectionSources.GetWeightCalculator(),
                _projectionSources.GetRenderingState(),
                _projectionSources.GetGameController(),
                _projectionSources.GetInputHandler(),
                _projectionSources.GetSettings(),
                _projectionSources.GetCachedLabels(),
                _projectionSources.GetErrorHandler());
    }
}
