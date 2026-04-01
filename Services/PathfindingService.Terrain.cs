using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    public sealed partial class PathfindingService
    {
        private bool TryRefreshTerrainData(GameController gameController, out bool[][] walkable, out GridPoint dims)
        {
            walkable = [];
            dims = default;

            var data = gameController.IngameState?.Data ?? gameController.Game?.IngameState?.Data;
            if (data == null)
                return false;

            if (!TryConvertPathfindingData(data.RawPathfindingData, out int[][]? rawGrid) || rawGrid == null || rawGrid.Length == 0)
                return false;

            int gridHeight = rawGrid.Length;
            int gridWidth = rawGrid[0]?.Length ?? 0;
            if (gridWidth <= 0)
                return false;

            bool[][] converted = ConvertRawGridToWalkable(rawGrid);

            var areaDims = data.AreaDimensions;
            GridPoint dimensions = new GridPoint(
                areaDims.X > 0 ? areaDims.X : gridWidth,
                areaDims.Y > 0 ? areaDims.Y : gridHeight);

            lock (_stateLock)
            {
                _walkableGrid = converted;
                _areaDimensions = dimensions;
            }

            walkable = converted;
            dims = dimensions;
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

            var converted = new List<int[]>(rows.Length);
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
            {
                if (converted[i].Length != expectedWidth)
                    return false;
            }

            grid = converted.ToArray();
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
                {
                    row[x] = source[x] > 0;
                }

                walkable[y] = row;
            }

            return walkable;
        }

        private static List<Vector2> BuildScreenPathApproximation(
            GameController gameController,
            List<GridPoint> gridPath,
            GridPoint start,
            GridPoint goal,
            Entity target)
        {
            if (gridPath.Count == 0)
                return [];

            if (!TryGetScreenPointForEntity(gameController, gameController.Player, out Vector2 startScreen)
                || !TryGetScreenPointForEntity(gameController, target, out Vector2 goalScreen))
            {
                return [];
            }

            float scaleX = ResolveScale(goalScreen.X - startScreen.X, goal.X - start.X);
            float scaleY = ResolveScale(goalScreen.Y - startScreen.Y, goal.Y - start.Y);

            var points = new List<Vector2>(gridPath.Count);
            for (int i = 0; i < gridPath.Count; i++)
            {
                GridPoint node = gridPath[i];
                float x = startScreen.X + ((node.X - start.X) * scaleX);
                float y = startScreen.Y + ((node.Y - start.Y) * scaleY);
                if (IsFinitePoint(x, y))
                    points.Add(new Vector2(x, y));
            }

            return points;
        }

        internal static float ResolveScale(float deltaScreen, float deltaGrid)
        {
            float scale;
            if (Math.Abs(deltaGrid) >= 0.001f)
                scale = deltaScreen / deltaGrid;
            else
                scale = Math.Sign(deltaScreen) * 2.5f;

            if (Math.Abs(scale) < 0.01f)
                return 2.5f;

            return scale;
        }

        private static bool TryGetScreenPointForEntity(GameController gameController, Entity? entity, out Vector2 screen)
        {
            screen = default;
            if (entity == null)
                return false;

            var camera = gameController.IngameState?.Camera ?? gameController.Game?.IngameState?.Camera;
            if (camera == null)
                return false;

            var raw = camera.WorldToScreen(entity.PosNum);
            if (!IsFinitePoint(raw.X, raw.Y))
                return false;

            screen = new Vector2(raw.X, raw.Y);
            return true;
        }

        internal static bool IsFinitePoint(float x, float y)
            => !float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y);
    }
}