namespace ClickIt.Tests.Harness
{
    // Baseline dense A* used only as a route and expansion-count oracle.
    internal sealed class DensePathGridOracle
    {
        private const float StraightCost = 1f;
        private const float DiagonalCost = 1.4142135f;
        private const float DiagonalHeuristicWeight = 0.41421357f;

        private static readonly PathfindingService.GridPoint[] NeighborOffsets =
        [
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
            new(1, 1),
            new(1, -1),
            new(-1, 1),
            new(-1, -1)
        ];

        private bool[]? _closed;
        private float[]? _gScore;
        private int[]? _parent;
        private int[]? _version;
        private int _bufferWidth;
        private int _bufferHeight;
        private int _generation;
        private PriorityQueue<int, float>? _open;

        // Reuse per-service buffers sized to the area grid, lazily reset via a generation stamp.
        private void EnsureSearchBuffers(int width, int height, int nodeCount)
        {
            if (_closed != null && _gScore != null && _parent != null && _version != null
                && _bufferWidth == width && _bufferHeight == height)
                return;

            _bufferWidth = width;
            _bufferHeight = height;
            _closed = new bool[nodeCount];
            _gScore = new float[nodeCount];
            _parent = new int[nodeCount];
            _version = new int[nodeCount];
        }

        internal List<PathfindingService.GridPoint>? FindPathAStar(bool[][] walkable, PathfindingService.GridPoint start, PathfindingService.GridPoint goal, int maxExpandedNodes, out int expandedNodes)
        {
            expandedNodes = 0;

            if (!TryGetGridDimensions(walkable, out int width, out int height))
                return null;

            PathfindingService.GridPoint dims = new(width, height);
            if (!IsInside(start, dims) || !IsInside(goal, dims))
                return null;
            if (!walkable[start.Y][start.X] || !walkable[goal.Y][goal.X])
                return null;

            int nodeCount = width * height;
            int budget = SystemMath.Max(1, maxExpandedNodes);

            EnsureSearchBuffers(width, height, nodeCount);
            int generation = _generation = _generation == int.MaxValue ? 1 : _generation + 1;
            bool[] closed = _closed!;
            float[] gScore = _gScore!;
            int[] parent = _parent!;
            int[] version = _version!;

            PriorityQueue<int, float> open = _open ??= new PriorityQueue<int, float>();
            open.Clear();

            int startKey = ToKey(start.X, start.Y, width);
            int goalKey = ToKey(goal.X, goal.Y, width);

            version[startKey] = generation;
            closed[startKey] = false;
            gScore[startKey] = 0f;
            parent[startKey] = -1;
            open.Enqueue(startKey, EstimateCost(start, goal));

            while (open.Count > 0 && expandedNodes < budget)
            {
                int currentKey = open.Dequeue();
                if (closed[currentKey])
                    continue;

                closed[currentKey] = true;
                expandedNodes++;
                if (currentKey == goalKey)
                    return BuildPathFromParents(parent, currentKey, width);

                PathfindingService.GridPoint current = FromKey(currentKey, width);
                float currentCost = gScore[currentKey];

                for (int i = 0; i < NeighborOffsets.Length; i++)
                {
                    PathfindingService.GridPoint offset = NeighborOffsets[i];
                    int nx = current.X + offset.X;
                    int ny = current.Y + offset.Y;

                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || !walkable[ny][nx])
                        continue;

                    int neighborKey = ToKey(nx, ny, width);
                    if (version[neighborKey] != generation)
                    {
                        version[neighborKey] = generation;
                        closed[neighborKey] = false;
                        gScore[neighborKey] = float.PositiveInfinity;
                        parent[neighborKey] = -1;
                    }
                    if (closed[neighborKey])
                        continue;

                    float moveCost = offset.X != 0 && offset.Y != 0 ? DiagonalCost : StraightCost;
                    float tentative = currentCost + moveCost;
                    if (tentative >= gScore[neighborKey])
                        continue;

                    parent[neighborKey] = currentKey;
                    gScore[neighborKey] = tentative;
                    PathfindingService.GridPoint neighbor = new(nx, ny);
                    open.Enqueue(neighborKey, tentative + EstimateCost(neighbor, goal));
                }
            }

            return null;
        }

        private static bool TryGetGridDimensions(bool[][]? walkable, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (walkable == null || walkable.Length == 0 || walkable[0] == null || walkable[0].Length == 0)
                return false;

            height = walkable.Length;
            width = walkable[0].Length;
            return true;
        }

        private static bool IsInside(PathfindingService.GridPoint point, PathfindingService.GridPoint dims)
            => point.X >= 0 && point.Y >= 0 && point.X < dims.X && point.Y < dims.Y;

        private static int ToKey(int x, int y, int width) => (y * width) + x;

        private static PathfindingService.GridPoint FromKey(int key, int width)
            => new(key % width, key / width);

        private static float EstimateCost(PathfindingService.GridPoint a, PathfindingService.GridPoint b)
        {
            int dx = SystemMath.Abs(a.X - b.X);
            int dy = SystemMath.Abs(a.Y - b.Y);
            int diagonal = SystemMath.Min(dx, dy);
            int straight = SystemMath.Max(dx, dy) - diagonal;
            return (diagonal * (StraightCost + DiagonalHeuristicWeight)) + (straight * StraightCost);
        }

        private static List<PathfindingService.GridPoint> BuildPathFromParents(int[] parent, int goalKey, int width)
        {
            List<PathfindingService.GridPoint> result = new(128);
            for (int trace = goalKey; trace >= 0; trace = parent[trace])
                result.Add(FromKey(trace, width));

            result.Reverse();
            return result;
        }
    }
}
