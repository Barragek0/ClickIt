namespace ClickIt.Features.Pathfinding.Terrain
{
    internal sealed class PathfindingTerrainCache
    {
        internal Vector2i AreaDims;
        internal bool[][]? Walkable;
    }

    internal static class PathTerrainSnapshotProvider
    {
        internal static bool TryRefreshTerrainData(GameController gameController, PathfindingTerrainCache cache, out bool[][] walkable, out PathfindingService.GridPoint dims)
        {
            walkable = [];
            dims = default;

            IngameData? data = gameController.IngameState?.Data ?? gameController.Game?.IngameState?.Data;
            if (data == null)
                return false;

            // The pathfinding grid is static per area, so cache the converted grid keyed by area
            // dimensions. This avoids re-reading the full multi-million-cell RawPathfindingData and
            // re-allocating the walkable grid on every A* call while pathing.
            Vector2i areaDims = data.AreaDimensions;
            bool cacheableArea = areaDims.X > 0 && areaDims.Y > 0;
            if (cacheableArea && cache.Walkable != null
                && cache.AreaDims.X == areaDims.X && cache.AreaDims.Y == areaDims.Y)
            {
                dims = new PathfindingService.GridPoint(
                    areaDims.X > 0 ? areaDims.X : cache.Walkable[0].Length,
                    areaDims.Y > 0 ? areaDims.Y : cache.Walkable.Length);
                walkable = cache.Walkable;
                return true;
            }

            if (!TryConvertPathfindingData(data.RawPathfindingData, out int[][]? rawGrid) || rawGrid == null || rawGrid.Length == 0)
                return false;

            int gridHeight = rawGrid.Length;
            int gridWidth = rawGrid[0]?.Length ?? 0;
            if (gridWidth <= 0)
                return false;

            bool[][] converted = ConvertRawGridToWalkable(rawGrid);
            dims = new PathfindingService.GridPoint(
                areaDims.X > 0 ? areaDims.X : gridWidth,
                areaDims.Y > 0 ? areaDims.Y : gridHeight);
            if (cacheableArea)
            {
                cache.AreaDims = areaDims;
                cache.Walkable = converted;
            }
            walkable = converted;
            return true;
        }

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
    }
}