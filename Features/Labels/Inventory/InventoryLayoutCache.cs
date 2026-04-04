namespace ClickIt.Features.Labels.Inventory
{
    internal sealed class InventoryLayoutCache(int cacheWindowMs)
    {
        private readonly object _cacheLock = new();
        private readonly int _cacheWindowMs = cacheWindowMs;

        private long _timestampMs;
        private object? _primaryInventory;
        private int _width;
        private int _height;
        private IReadOnlyList<InventoryLayoutEntry> _entries = Array.Empty<InventoryLayoutEntry>();
        private string _source = string.Empty;
        private string _debugDetails = string.Empty;
        private bool _isReliable;
        private int _rawEntryCount;
        private bool _hasValue;

        public bool TryGet(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            out InventoryLayoutSnapshot snapshot)
        {
            lock (_cacheLock)
            {
                if (_hasValue
                    && ReferenceEquals(_primaryInventory, primaryInventory)
                    && _width == inventoryWidth
                    && _height == inventoryHeight
                    && IsCacheFresh(now, _timestampMs, _cacheWindowMs))
                {
                    snapshot = new InventoryLayoutSnapshot(
                        Entries: _entries,
                        Source: _source,
                        DebugDetails: _debugDetails,
                        IsReliable: _isReliable,
                        RawEntryCount: _rawEntryCount);
                    return true;
                }
            }

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
            lock (_cacheLock)
            {
                _primaryInventory = primaryInventory;
                _timestampMs = now;
                _width = inventoryWidth;
                _height = inventoryHeight;
                _entries = snapshot.Entries ?? Array.Empty<InventoryLayoutEntry>();
                _source = snapshot.Source ?? string.Empty;
                _debugDetails = snapshot.DebugDetails ?? string.Empty;
                _isReliable = snapshot.IsReliable;
                _rawEntryCount = snapshot.RawEntryCount;
                _hasValue = true;
            }
        }

        public void Clear()
        {
            lock (_cacheLock)
            {
                _timestampMs = 0;
                _primaryInventory = null;
                _width = 0;
                _height = 0;
                _entries = Array.Empty<InventoryLayoutEntry>();
                _source = string.Empty;
                _debugDetails = string.Empty;
                _isReliable = false;
                _rawEntryCount = 0;
                _hasValue = false;
            }
        }

        private static bool IsCacheFresh(long now, long cachedAtMs, int windowMs)
        {
            if (cachedAtMs <= 0 || windowMs <= 0)
                return false;

            long age = now - cachedAtMs;
            return age >= 0 && age <= windowMs;
        }
    }
}