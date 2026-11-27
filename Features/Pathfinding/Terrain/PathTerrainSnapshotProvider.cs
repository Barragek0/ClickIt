namespace ClickIt.Features.Pathfinding.Terrain
{
    internal sealed class PathfindingTerrainCache
    {
        internal Vector2i AreaDims;
        internal long AreaHash;
        // The RAW pathfinding grid's dimensions — the authoritative, stable cache key. AreaDimensions is unreliable as a key (it reads differently for the same area), and the walkable grid is built from RawPathfindingData, so its real shape must drive the cache comparison.
        internal int GridWidth;
        internal int GridHeight;
        internal bool[][]? Walkable;
        // Identity is diagnostic only. A strong reference here pins the host's raw grid caches.
        internal WeakReference<IngameData>? DataOwner;
        // IngameData.Address is the authoritative per-area cache key (the wrapper churns every ~25ms and can flap in-map).
        internal long DataOwnerAddress;
        internal TerrainOwnerChangeConfirmation OwnerChange;
        // Rebuilds caused purely by wrapper recreation (same address, different reference).
        internal long ChurnRebuildCount;
    }

    internal struct TerrainOwnerChangeConfirmation
    {
        private const long ConfirmMs = 150;
        private long? _pendingAddress;
        private long _pendingSinceMs;

        internal bool ShouldWait(long currentAddress, long observedAddress, long now)
        {
            if (observedAddress == currentAddress)
            {
                // The candidate reverted. A later appearance must complete a new window.
                _pendingAddress = null;
                return false;
            }

            if (_pendingAddress != observedAddress)
            {
                _pendingAddress = observedAddress;
                _pendingSinceMs = now;
            }

            return now - _pendingSinceMs < ConfirmMs;
        }
    }

    internal static class PathTerrainSnapshotProvider
    {
        internal static bool TryRefreshTerrainData(GameController gameController, PathfindingTerrainCache cache, out bool[][] walkable, out PathfindingService.GridPoint dims, out bool fromCache)
        {
            IngameData? data = gameController.IngameState?.Data ?? gameController.Game?.IngameState?.Data;
            object? rawPathData = data?.RawPathfindingData;
            return TryRefreshTerrainData(cache, data, rawPathData, data?.Address ?? 0, Environment.TickCount64,
                out walkable, out dims, out fromCache);
        }

        internal static bool TryRefreshTerrainData(PathfindingTerrainCache cache, IngameData? data, object? rawPathData,
            long dataAddress, long now, out bool[][] walkable, out PathfindingService.GridPoint dims, out bool fromCache)
        {
            walkable = [];
            dims = default;
            fromCache = false;

            if (data == null || !TryGetRawGridDims(rawPathData, out int gridWidth, out int gridHeight) || gridHeight == 0)
                return false;

            if (cache.Walkable != null
                && cache.GridWidth == gridWidth
                && cache.GridHeight == gridHeight)
            {
                bool waitForOwner = cache.OwnerChange.ShouldWait(cache.DataOwnerAddress, dataAddress, now);
                if (dataAddress == cache.DataOwnerAddress || waitForOwner)
                {
                    dims = new PathfindingService.GridPoint(gridWidth, gridHeight);
                    walkable = cache.Walkable;
                    fromCache = true;
                    return true;
                }
            }

            // Keep the last known grid while the replacement is unconfirmed or invalid.
            // A valid replacement overwrites matching rows or replaces a differently sized grid.
            if (!TryBuildWalkableGrid(rawPathData, cache.Walkable, out bool[][] converted, out _, out _) || converted.Length == 0)
                return false;

            if (cache.DataOwner != null
                && cache.DataOwnerAddress == dataAddress
                && (!cache.DataOwner.TryGetTarget(out IngameData? previous) || !ReferenceEquals(data, previous)))
            {
                cache.ChurnRebuildCount++;
            }

            dims = new PathfindingService.GridPoint(gridWidth, gridHeight);
            cache.GridWidth = gridWidth;
            cache.GridHeight = gridHeight;
            if (cache.DataOwner == null)
                cache.DataOwner = new WeakReference<IngameData>(data);
            else
                cache.DataOwner.SetTarget(data);
            cache.DataOwnerAddress = dataAddress;
            cache.OwnerChange = default;
            cache.Walkable = converted;
            walkable = converted;
            return true;
        }

        // Cheap shape read of the raw pathfinding array (no per-cell conversion) so the cache key never pays the rebuild cost just to decide whether to rebuild.
        internal static bool TryGetRawGridDims(object? rawPathData, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (rawPathData is int[][] direct && direct.Length > 0)
            {
                height = direct.Length;
                width = direct[0]?.Length ?? 0;
                return width > 0;
            }

            if (rawPathData is Array rows && rows.Length > 0)
            {
                height = rows.Length;
                object? firstRow = rows.GetValue(0);
                if (firstRow is int[] intRow)
                    width = intRow.Length;
                else if (firstRow is Array arrayRow)
                    width = arrayRow.Length;
                return width > 0;
            }

            return false;
        }

        // Pure cache-key decision so the same-dims-different-area guard is unit-testable.
        internal static bool ShouldUseTerrainCache(
            PathfindingTerrainCache cache,
            Vector2i areaDims,
            long areaHash,
            bool cacheableArea,
            bool hasAreaHash)
            => cacheableArea
                && hasAreaHash
                && cache.Walkable != null
                && cache.AreaDims.X == areaDims.X
                && cache.AreaDims.Y == areaDims.Y
                && cache.AreaHash == areaHash;

        internal static bool TryConvertPathfindingData(object? rawPathData, out int[][]? grid)
        {
            grid = null;
            if (rawPathData == null)
                return false;

            if (rawPathData is int[][] direct && direct.Length > 0)
            {
                grid = direct;
                return true;
            }

            if (rawPathData is not Array rows || rows.Length == 0)
                return false;

            List<int[]> converted = new(rows.Length);
            foreach (object? row in rows)
            {
                if (!TryConvertRow(row, out int[]? parsed) || parsed == null || parsed.Length == 0)
                    continue;
                converted.Add(parsed);
            }

            if (converted.Count == 0)
                return false;

            int expectedWidth = converted[0].Length;
            for (int i = 1; i < converted.Count; i++)
                if (converted[i].Length != expectedWidth)
                    return false;


            grid = [.. converted];
            return true;
        }

        internal static bool TryConvertRow(object? row, out int[]? parsed)
        {
            parsed = null;
            if (row == null)
                return false;

            if (row is int[] intRow)
            {
                parsed = intRow;
                return true;
            }

            if (row is not Array arrayRow)
                return false;

            int[] values = new int[arrayRow.Length];
            for (int i = 0; i < arrayRow.Length; i++)
            {
                object? value = arrayRow.GetValue(i);
                if (value == null)
                    return false;

                try
                {
                    values[i] = Convert.ToInt32(value);
                }
                catch
                {
                    return false;
                }
            }

            parsed = values;
            return true;
        }

        internal static bool[][] ConvertRawGridToWalkable(int[][] rawGrid)
        {
            bool[][] walkable = new bool[rawGrid.Length][];
            for (int y = 0; y < rawGrid.Length; y++)
            {
                int[] source = rawGrid[y];
                bool[] row = new bool[source.Length];
                for (int x = 0; x < source.Length; x++)
                    row[x] = source[x] > 0;

                walkable[y] = row;
            }

            return walkable;
        }

        internal static bool TryBuildWalkableGrid(object? rawPathData, bool[][]? existing, out bool[][] walkable, out int width, out int height)
        {
            walkable = [];
            width = 0;
            height = 0;

            if (rawPathData == null)
                return false;

            if (rawPathData is int[][] direct && direct.Length > 0)
                return BuildWalkableGridFromIntRows(direct, existing, out walkable, out width, out height);

            if (rawPathData is not Array rows || rows.Length == 0)
                return false;

            return BuildWalkableGridFromArrayRows(rows, existing, out walkable, out width, out height);
        }

        private static bool BuildWalkableGridFromIntRows(int[][] rows, bool[][]? existing, out bool[][] walkable, out int width, out int height)
        {
            walkable = [];
            height = rows.Length;
            width = rows[0]?.Length ?? 0;
            if (width <= 0)
                return false;

            bool[][] target = AcquireGrid(height, width, existing);
            for (int y = 0; y < height; y++)
            {
                int[] source = rows[y];
                bool[] dest = target[y];
                for (int x = 0; x < width; x++)
                    dest[x] = source[x] > 0;
            }

            walkable = target;
            return true;
        }

        private static bool BuildWalkableGridFromArrayRows(Array rows, bool[][]? existing, out bool[][] walkable, out int width, out int height)
        {
            walkable = [];
            width = 0;
            height = rows.Length;

            // Validate shape and values first (no writes) so a failed rebuild never leaves a partially-updated cached grid behind.
            int expectedWidth = -1;
            for (int y = 0; y < height; y++)
            {
                object? row = rows.GetValue(y);
                int rowWidth;
                if (row is int[] intRow)
                    rowWidth = intRow.Length;
                else if (row is Array arrayRow)
                    rowWidth = arrayRow.Length;
                else
                    return false;

                if (rowWidth == 0)
                    return false;
                if (expectedWidth == -1)
                    expectedWidth = rowWidth;
                else if (rowWidth != expectedWidth)
                    return false;

                if (row is int[])
                    continue;

                Array valueRow = (Array)row!;
                for (int x = 0; x < rowWidth; x++)
                {
                    object? value = valueRow.GetValue(x);
                    if (value == null)
                        return false;

                    try
                    {
                        _ = Convert.ToInt32(value);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            if (expectedWidth <= 0)
                return false;

            width = expectedWidth;
            bool[][] target = AcquireGrid(height, width, existing);
            for (int y = 0; y < height; y++)
            {
                object? row = rows.GetValue(y);
                bool[] dest = target[y];
                if (row is int[] intRow)
                {
                    for (int x = 0; x < width; x++)
                        dest[x] = intRow[x] > 0;
                }
                else
                {
                    Array valueRow = (Array)row!;
                    for (int x = 0; x < width; x++)
                        dest[x] = Convert.ToInt32(valueRow.GetValue(x)) > 0;
                }
            }

            walkable = target;
            return true;
        }

        private static bool[][] AcquireGrid(int height, int width, bool[][]? existing)
        {
            if (existing != null && existing.Length == height && height > 0 && existing[0].Length == width)
                return existing;

            bool[][] fresh = new bool[height][];
            for (int y = 0; y < height; y++)
                fresh[y] = new bool[width];
            return fresh;
        }
    }
}
