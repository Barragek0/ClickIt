namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryProbeServiceDependencies(
        int CacheWindowMs,
        int DebugTrailCapacity,
        Func<GameController?, (bool Success, InventorySnapshot Snapshot)> TryBuildInventorySnapshot,
        InventoryLayoutCache LayoutCache);

    internal sealed class InventoryProbeService(InventoryProbeServiceDependencies dependencies)
    {
        private readonly InventoryProbeServiceDependencies _dependencies = dependencies;
        private readonly InventoryDiagnosticsChannel _diagnosticsChannel = new(dependencies.DebugTrailCapacity);
        private readonly TimedValueCache<GameController?, InventoryFullProbe> _inventoryProbeCache = new(
            dependencies.CacheWindowMs,
            settings: new TimedValueCacheSettings(
                RequireNonNegativeAge: true,
                RequirePositiveCachedTimestamp: true));

        public InventoryDebugSnapshot GetLatestDebug()
            => _diagnosticsChannel.GetLatest();

        public IReadOnlyList<string> GetLatestDebugTrail()
            => _diagnosticsChannel.GetTrail();

        public void PublishDebug(InventoryDebugSnapshot snapshot)
            => _diagnosticsChannel.Publish(snapshot);

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
            _inventoryProbeCache.Invalidate();

            _dependencies.LayoutCache.Clear();
        }

        private bool TryGetCachedInventoryProbe(GameController? gameController, long now, out InventoryFullProbe probe)
        {
            return _inventoryProbeCache.TryGetValue(gameController, now, out probe);
        }

        private void SetCachedInventoryProbe(GameController? gameController, long now, InventoryFullProbe probe)
        {
            _inventoryProbeCache.SetValue(gameController, now, probe);
        }

    }
}

