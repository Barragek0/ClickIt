using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    public sealed class PathfindingService(ClickItSettings settings, Utils.ErrorHandler? errorHandler = null)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Utils.ErrorHandler? _errorHandler = errorHandler;

        private readonly object _stateLock = new();
        private bool[][]? _walkableGrid;
        private GridPoint _areaDimensions;
        private string _lastFailureReason = string.Empty;
        private int _lastExpandedNodes;
        private int _lastPathLength;
        private long _lastComputeMs;
        private long _lastPathBuildAttemptTickMs;
        private IReadOnlyList<GridPoint> _lastGridPath = new List<GridPoint>();
        private IReadOnlyList<Vector2> _lastScreenPath = new List<Vector2>();
        private string _lastTargetPath = string.Empty;
        private OffscreenMovementDebugSnapshot _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;

        public readonly record struct GridPoint(int X, int Y);

        public sealed record PathfindingDebugSnapshot(
            bool TerrainLoaded,
            int AreaWidth,
            int AreaHeight,
            int LastExpandedNodes,
            int LastPathLength,
            long LastComputeMs,
            string LastFailureReason,
            string LastTargetPath);

        public sealed record OffscreenMovementDebugSnapshot(
            bool HasData,
            string Stage,
            string TargetPath,
            bool BuiltPath,
            bool ResolvedFromPath,
            bool ResolvedClickPoint,
            Vector2 WindowCenter,
            Vector2 TargetScreen,
            Vector2 ClickScreen,
            Vector2 PlayerGrid,
            Vector2 TargetGrid,
            string MovementSkillDebug,
            long TimestampMs)
        {
            public static readonly OffscreenMovementDebugSnapshot Empty = new(
                HasData: false,
                Stage: string.Empty,
                TargetPath: string.Empty,
                BuiltPath: false,
                ResolvedFromPath: false,
                ResolvedClickPoint: false,
                WindowCenter: default,
                TargetScreen: default,
                ClickScreen: default,
                PlayerGrid: default,
                TargetGrid: default,
                MovementSkillDebug: string.Empty,
                TimestampMs: 0);
        }

        public PathfindingDebugSnapshot GetDebugSnapshot()
        {
            lock (_stateLock)
            {
                return new PathfindingDebugSnapshot(
                    TerrainLoaded: _walkableGrid != null,
                    AreaWidth: _areaDimensions.X,
                    AreaHeight: _areaDimensions.Y,
                    LastExpandedNodes: _lastExpandedNodes,
                    LastPathLength: _lastPathLength,
                    LastComputeMs: _lastComputeMs,
                    LastFailureReason: _lastFailureReason,
                    LastTargetPath: _lastTargetPath);
            }
        }

        public IReadOnlyList<Vector2> GetLatestScreenPath()
        {
            lock (_stateLock)
            {
                return _lastScreenPath;
            }
        }

        public IReadOnlyList<GridPoint> GetLatestGridPath()
        {
            lock (_stateLock)
            {
                return _lastGridPath;
            }
        }

        public OffscreenMovementDebugSnapshot GetLatestOffscreenMovementDebug()
        {
            lock (_stateLock)
            {
                return _lastOffscreenMovementDebug;
            }
        }

        public void SetLatestOffscreenMovementDebug(OffscreenMovementDebugSnapshot snapshot)
        {
            lock (_stateLock)
            {
                _lastOffscreenMovementDebug = snapshot;
            }
        }

        public void ClearLatestPath()
        {
            lock (_stateLock)
            {
                _lastGridPath = new List<GridPoint>();
                _lastScreenPath = new List<Vector2>();
                _lastPathLength = 0;
                _lastTargetPath = string.Empty;
                _lastFailureReason = string.Empty;
                _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;
            }
        }

        internal bool ClearPathIfStale(int staleTimeoutMs)
        {
            int timeoutMs = Math.Max(250, staleTimeoutMs);
            lock (_stateLock)
            {
                if (_lastGridPath.Count == 0 && _lastScreenPath.Count == 0)
                    return false;

                long elapsedMs = Environment.TickCount64 - _lastPathBuildAttemptTickMs;
                if (elapsedMs < timeoutMs)
                    return false;

                _lastGridPath = new List<GridPoint>();
                _lastScreenPath = new List<Vector2>();
                _lastPathLength = 0;
                _lastTargetPath = string.Empty;
                _lastFailureReason = string.Empty;
                _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;
                return true;
            }
        }

        public bool TryBuildPathToTarget(GameController? gameController, Entity? target, int maxExpandedNodes)
        {
            lock (_stateLock)
            {
                _lastPathBuildAttemptTickMs = Environment.TickCount64;
            }

            if (gameController == null || target == null)
                return Fail("GameController/target unavailable.");

            if (!TryRefreshTerrainData(gameController, out bool[][] walkable, out GridPoint dims))
                return Fail("Terrain/pathfinding data unavailable.");

            if (!TryGetGridPos(gameController.Player, out GridPoint start))
                return Fail("Unable to resolve player grid position.");

            if (!TryGetGridPos(target, out GridPoint goal))
                return Fail("Unable to resolve target grid position.");

            if (!IsInside(start, dims) || !IsInside(goal, dims))
                return Fail("Grid positions are outside area dimensions.");

            if (!TryResolveWalkableGoal(walkable, goal, maxRadius: 18, out GridPoint walkableGoal))
                return Fail("No walkable tile found near target grid position.");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            List<GridPoint>? gridPath = FindPathAStar(walkable, start, walkableGoal, Math.Max(100, maxExpandedNodes), out int expandedNodes);
            sw.Stop();

            if (gridPath == null || gridPath.Count == 0)
            {
                lock (_stateLock)
                {
                    _lastExpandedNodes = expandedNodes;
                    _lastComputeMs = sw.ElapsedMilliseconds;
                    _lastPathLength = 0;
                    _lastFailureReason = "A* did not find a route.";
                    _lastTargetPath = target.Path ?? string.Empty;
                    _lastGridPath = new List<GridPoint>();
                    _lastScreenPath = new List<Vector2>();
                }
                return false;
            }

            List<Vector2> screenPath = BuildScreenPathApproximation(gameController, gridPath, start, goal, target);

            lock (_stateLock)
            {
                _walkableGrid = walkable;
                _areaDimensions = dims;
                _lastExpandedNodes = expandedNodes;
                _lastComputeMs = sw.ElapsedMilliseconds;
                _lastPathLength = gridPath.Count;
                _lastFailureReason = string.Empty;
                _lastTargetPath = target.Path ?? string.Empty;
                _lastGridPath = gridPath;
                _lastScreenPath = screenPath;
            }

            return true;
        }

        internal static List<GridPoint>? FindPathAStar(bool[][] walkable, GridPoint start, GridPoint goal, int maxExpandedNodes, out int expandedNodes)
        {
            expandedNodes = 0;
            if (walkable == null || walkable.Length == 0)
                return null;

            int height = walkable.Length;
            int width = walkable[0].Length;

            if (start.X < 0 || start.Y < 0 || start.X >= width || start.Y >= height)
                return null;
            if (goal.X < 0 || goal.Y < 0 || goal.X >= width || goal.Y >= height)
                return null;

            if (!walkable[start.Y][start.X] || !walkable[goal.Y][goal.X])
                return null;

            static int Key(int x, int y, int w) => (y * w) + x;
            static float Heuristic(GridPoint a, GridPoint b)
            {
                int dx = Math.Abs(a.X - b.X);
                int dy = Math.Abs(a.Y - b.Y);
                return dx + dy;
            }

            var frontier = new PriorityQueue<GridPoint, float>();
            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, float>();

            int startKey = Key(start.X, start.Y, width);
            int goalKey = Key(goal.X, goal.Y, width);
            frontier.Enqueue(start, 0f);
            gScore[startKey] = 0f;

            GridPoint[] neighborOffsets =
            [
                new GridPoint(1, 0),
                new GridPoint(-1, 0),
                new GridPoint(0, 1),
                new GridPoint(0, -1),
                new GridPoint(1, 1),
                new GridPoint(1, -1),
                new GridPoint(-1, 1),
                new GridPoint(-1, -1)
            ];

            while (frontier.Count > 0 && expandedNodes < maxExpandedNodes)
            {
                GridPoint current = frontier.Dequeue();
                expandedNodes++;

                int currentKey = Key(current.X, current.Y, width);
                if (currentKey == goalKey)
                {
                    var path = new List<GridPoint>();
                    int trace = currentKey;
                    while (true)
                    {
                        int x = trace % width;
                        int y = trace / width;
                        path.Add(new GridPoint(x, y));
                        if (trace == startKey)
                            break;
                        if (!cameFrom.TryGetValue(trace, out int prev))
                            break;
                        trace = prev;
                    }
                    path.Reverse();
                    return path;
                }

                float currentScore = gScore.TryGetValue(currentKey, out float gs) ? gs : float.PositiveInfinity;
                for (int i = 0; i < neighborOffsets.Length; i++)
                {
                    GridPoint n = new(current.X + neighborOffsets[i].X, current.Y + neighborOffsets[i].Y);
                    if (n.X < 0 || n.Y < 0 || n.X >= width || n.Y >= height)
                        continue;
                    if (!walkable[n.Y][n.X])
                        continue;

                    bool diagonal = neighborOffsets[i].X != 0 && neighborOffsets[i].Y != 0;
                    float movementCost = diagonal ? 1.4142135f : 1f;
                    float tentative = currentScore + movementCost;
                    int nKey = Key(n.X, n.Y, width);
                    float existing = gScore.TryGetValue(nKey, out float e) ? e : float.PositiveInfinity;
                    if (tentative >= existing)
                        continue;

                    cameFrom[nKey] = currentKey;
                    gScore[nKey] = tentative;
                    float f = tentative + Heuristic(n, goal);
                    frontier.Enqueue(n, f);
                }
            }

            return null;
        }

        private static bool IsInside(GridPoint p, GridPoint dims)
        {
            return p.X >= 0 && p.Y >= 0 && p.X < dims.X && p.Y < dims.Y;
        }

        internal static bool TryResolveWalkableGoal(bool[][] walkable, GridPoint desiredGoal, int maxRadius, out GridPoint resolvedGoal)
        {
            resolvedGoal = desiredGoal;
            if (walkable == null || walkable.Length == 0 || walkable[0].Length == 0)
                return false;

            int height = walkable.Length;
            int width = walkable[0].Length;
            if (desiredGoal.X < 0 || desiredGoal.Y < 0 || desiredGoal.X >= width || desiredGoal.Y >= height)
                return false;

            if (walkable[desiredGoal.Y][desiredGoal.X])
                return true;

            int clampedRadius = Math.Max(1, maxRadius);
            for (int r = 1; r <= clampedRadius; r++)
            {
                int minX = Math.Max(0, desiredGoal.X - r);
                int maxX = Math.Min(width - 1, desiredGoal.X + r);
                int minY = Math.Max(0, desiredGoal.Y - r);
                int maxY = Math.Min(height - 1, desiredGoal.Y + r);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (Math.Max(Math.Abs(x - desiredGoal.X), Math.Abs(y - desiredGoal.Y)) != r)
                            continue;

                        if (!walkable[y][x])
                            continue;

                        resolvedGoal = new GridPoint(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryRefreshTerrainData(GameController gameController, out bool[][] walkable, out GridPoint dims)
        {
            walkable = [];
            dims = default;

            var data = gameController.IngameState?.Data ?? gameController.Game?.IngameState?.Data;
            if (data == null)
                return false;

            object? rawPathData = data.RawPathfindingData;
            if (!TryConvertPathfindingData(rawPathData, out int[][]? rawGrid) || rawGrid == null || rawGrid.Length == 0)
            {
                return false;
            }

            var areaDims = data.AreaDimensions;
            int w = areaDims.X;
            int h = areaDims.Y;
            if (w <= 0 || h <= 0)
            {
                w = rawGrid[0].Length;
                h = rawGrid.Length;
            }

            bool[][] walk = new bool[rawGrid.Length][];
            for (int y = 0; y < rawGrid.Length; y++)
            {
                int[] row = rawGrid[y];
                bool[] outRow = new bool[row.Length];
                for (int x = 0; x < row.Length; x++)
                {
                    outRow[x] = row[x] > 0;
                }
                walk[y] = outRow;
            }

            lock (_stateLock)
            {
                _walkableGrid = walk;
                _areaDimensions = new GridPoint(w, h);
            }

            walkable = walk;
            dims = new GridPoint(w, h);
            return true;
        }

        private static bool TryConvertPathfindingData(object? rawPathData, out int[][]? grid)
        {
            grid = null;
            if (rawPathData == null)
                return false;

            if (rawPathData is int[][] direct)
            {
                grid = direct;
                return true;
            }

            if (rawPathData is Array outer)
            {
                var rows = new List<int[]>(outer.Length);
                foreach (object? rowObj in outer)
                {
                    if (rowObj is int[] intRow)
                    {
                        rows.Add(intRow);
                        continue;
                    }

                    if (rowObj is Array inner)
                    {
                        int[] converted = new int[inner.Length];
                        for (int i = 0; i < inner.Length; i++)
                        {
                            converted[i] = Convert.ToInt32(inner.GetValue(i));
                        }
                        rows.Add(converted);
                    }
                }

                if (rows.Count > 0)
                {
                    grid = rows.ToArray();
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetGridPos(Entity? entity, out GridPoint point)
        {
            point = default;
            if (entity == null)
                return false;

            var gridPos = entity.GridPosNum;
            int x = (int)gridPos.X;
            int y = (int)gridPos.Y;
            point = new GridPoint(x, y);
            return true;
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
                || !TryGetScreenPointForEntity(gameController, target, out Vector2 targetScreen))
            {
                return [];
            }

            int dX = goal.X - start.X;
            int dY = goal.Y - start.Y;
            float sX = Math.Abs(dX) > 0 ? (targetScreen.X - startScreen.X) / dX : 0f;
            float sY = Math.Abs(dY) > 0 ? (targetScreen.Y - startScreen.Y) / dY : 0f;

            if (Math.Abs(sX) < 0.001f)
                sX = 2.5f;
            if (Math.Abs(sY) < 0.001f)
                sY = 2.5f;

            var points = new List<Vector2>(gridPath.Count);
            for (int i = 0; i < gridPath.Count; i++)
            {
                GridPoint p = gridPath[i];
                float x = startScreen.X + ((p.X - start.X) * sX);
                float y = startScreen.Y + ((p.Y - start.Y) * sY);
                if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
                    continue;
                points.Add(new Vector2(x, y));
            }

            return points;
        }

        private static bool TryGetScreenPointForEntity(GameController gameController, Entity? entity, out Vector2 screen)
        {
            screen = default;
            if (entity == null)
                return false;

            var camera = gameController.IngameState?.Camera ?? gameController.Game?.IngameState?.Camera;
            if (camera == null)
                return false;

            var screenRaw = camera.WorldToScreen(entity.PosNum);
            screen = new Vector2(screenRaw.X, screenRaw.Y);
            if (float.IsNaN(screen.X) || float.IsNaN(screen.Y) || float.IsInfinity(screen.X) || float.IsInfinity(screen.Y))
                return false;

            return true;
        }

        private bool Fail(string reason)
        {
            lock (_stateLock)
            {
                _lastFailureReason = reason;
                _lastPathLength = 0;
                _lastGridPath = new List<GridPoint>();
                _lastScreenPath = new List<Vector2>();
            }

            _errorHandler?.LogMessage(localDebug: true, message: $"PathfindingService: {reason}", frame: 10);
            return false;
        }
    }
}
