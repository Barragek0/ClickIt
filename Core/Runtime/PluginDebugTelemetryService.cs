using ClickIt.Services;
using ClickIt.Services.Observability;
using ClickIt.Services.Observability.TelemetryProjection;

namespace ClickIt.Core.Runtime
{
    internal sealed class PluginDebugTelemetryService(
        Func<ClickService?> getClickService,
        Func<LabelFilterService?> getLabelFilterService,
        Func<PathfindingService?> getPathfindingService)
    {
        private readonly Func<ClickService?> _getClickService = getClickService;
        private readonly Func<LabelFilterService?> _getLabelFilterService = getLabelFilterService;
        private readonly Func<PathfindingService?> _getPathfindingService = getPathfindingService;
        private readonly DebugTelemetryFreezeState _freezeState = new();

        internal DebugTelemetrySnapshot GetSnapshot()
        {
            if (_freezeState.TryGetFrozenSnapshot(Environment.TickCount64, out DebugTelemetrySnapshot frozenSnapshot))
                return frozenSnapshot;

            return DebugTelemetryProjection.Build(_getClickService(), _getLabelFilterService(), _getPathfindingService());
        }

        internal void FreezeSnapshot(string reason, int holdDurationMs)
        {
            DebugTelemetrySnapshot snapshot = DebugTelemetryProjection.Build(_getClickService(), _getLabelFilterService(), _getPathfindingService());
            _freezeState.Freeze(snapshot, reason, holdDurationMs, Environment.TickCount64);
        }

        internal bool TryGetFreezeState(out long remainingMs, out string reason)
            => _freezeState.TryGetFreezeState(Environment.TickCount64, out remainingMs, out reason);

        internal void Clear()
            => _freezeState.Clear();
    }
}