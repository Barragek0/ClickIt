namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryProbeServiceDependencies(
        int CacheWindowMs,
        int DebugTrailCapacity,
        Func<GameController?, (bool Success, InventorySnapshot Snapshot)> TryBuildInventorySnapshot,
        InventoryLayoutCache LayoutCache);

    internal sealed class InventoryProbeService
    {
        private readonly InventoryProbeServiceDependencies _dependencies;
        private readonly object _cacheLock = new();
        private InventoryDiagnosticsChannel _diagnosticsChannel;

        private long _inventoryProbeCacheTimestampMs;
        private GameController? _inventoryProbeCacheController;
        private InventoryFullProbe _inventoryProbeCacheValue = InventoryFullProbe.Empty;
        private bool _inventoryProbeCacheHasValue;

        public InventoryProbeService(InventoryProbeServiceDependencies dependencies)
        {
            _dependencies = dependencies;
            _diagnosticsChannel = new InventoryDiagnosticsChannel(dependencies.DebugTrailCapacity);
        }

        public InventoryDebugSnapshot GetLatestDebug() => _diagnosticsChannel.GetLatest();

        public IReadOnlyList<string> GetLatestDebugTrail() => _diagnosticsChannel.GetTrail();

        public void PublishDebug(InventoryDebugSnapshot snapshot) => _diagnosticsChannel.Publish(snapshot);

        public bool IsInventoryFull(GameController? gameController, out InventoryFullProbe probe)
        {
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryProbe(gameController, now, out InventoryFullProbe cachedProbe))
            {
                probe = cachedProbe;
                return probe.IsFull;
            }

            (bool success, InventorySnapshot snapshot) = _dependencies.TryBuildInventorySnapshot(gameController);
            if (!success)
            {
                probe = InventoryFullProbe.Empty with { Notes = "Primary server inventory missing" };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            probe = snapshot.FullProbe;
            SetCachedInventoryProbe(gameController, now, probe);
            return probe.IsFull;
        }

        public void ClearForShutdown()
        {
            lock (_cacheLock)
            {
                _inventoryProbeCacheTimestampMs = 0;
                _inventoryProbeCacheController = null;
                _inventoryProbeCacheValue = InventoryFullProbe.Empty;
                _inventoryProbeCacheHasValue = false;
            }

            _dependencies.LayoutCache.Clear();
        }

        private bool TryGetCachedInventoryProbe(GameController? gameController, long now, out InventoryFullProbe probe)
        {
            lock (_cacheLock)
            {
                if (_inventoryProbeCacheHasValue
                    && ReferenceEquals(_inventoryProbeCacheController, gameController)
                    && InventoryCacheWindowPolicy.IsFresh(now, _inventoryProbeCacheTimestampMs, _dependencies.CacheWindowMs))
                {
                    probe = _inventoryProbeCacheValue;
                    return true;
                }
            }

            probe = InventoryFullProbe.Empty;
            return false;
        }

        private void SetCachedInventoryProbe(GameController? gameController, long now, InventoryFullProbe probe)
        {
            lock (_cacheLock)
            {
                _inventoryProbeCacheController = gameController;
                _inventoryProbeCacheTimestampMs = now;
                _inventoryProbeCacheValue = probe;
                _inventoryProbeCacheHasValue = true;
            }
        }

    }
}

