namespace ClickIt.Features.Labels.Inventory
{
    internal sealed class InventoryLayoutCache(int cacheWindowMs)
    {
        private readonly TimedValueCache<InventoryLayoutCacheKey, InventoryLayoutSnapshot> _cache
            = new(
                cacheWindowMs,
                keyComparer: InventoryLayoutCacheKeyComparer.Instance,
                settings: new TimedValueCacheSettings(
                    RequireNonNegativeAge: true,
                    RequirePositiveCachedTimestamp: true));

        public bool TryGet(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            out InventoryLayoutSnapshot snapshot)
        {
            bool hit = _cache.TryGetValue(new InventoryLayoutCacheKey(primaryInventory, inventoryWidth, inventoryHeight), now, out snapshot);
            if (hit)
                return true;

            snapshot = InventoryLayoutSnapshot.Empty;
            return false;
        }

        public void Set(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            InventoryLayoutSnapshot snapshot)
        {
            InventoryLayoutSnapshot normalized = new(
                Entries: snapshot.Entries ?? [],
                Source: snapshot.Source ?? string.Empty,
                DebugDetails: snapshot.DebugDetails ?? string.Empty,
                IsReliable: snapshot.IsReliable,
                RawEntryCount: snapshot.RawEntryCount);

            _cache.SetValue(new InventoryLayoutCacheKey(primaryInventory, inventoryWidth, inventoryHeight), now, normalized);
        }

        public void Clear()
        {
            _cache.Invalidate();
        }

        private readonly record struct InventoryLayoutCacheKey(
            object PrimaryInventory,
            int Width,
            int Height);

        private sealed class InventoryLayoutCacheKeyComparer : IEqualityComparer<InventoryLayoutCacheKey>
        {
            public static readonly InventoryLayoutCacheKeyComparer Instance = new();

            public bool Equals(InventoryLayoutCacheKey x, InventoryLayoutCacheKey y)
                => ReferenceEquals(x.PrimaryInventory, y.PrimaryInventory)
                    && x.Width == y.Width
                    && x.Height == y.Height;

            public int GetHashCode(InventoryLayoutCacheKey obj)
                => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.PrimaryInventory), obj.Width, obj.Height);
        }
    }
}