using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public sealed partial class PathfindingService
    {
        private const float StraightCost = 1f;
        private const float DiagonalCost = 1.4142135f;
        private const float DiagonalHeuristicWeight = 0.41421357f;

        private static readonly GridPoint[] NeighborOffsets =
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

        internal static List<GridPoint>? FindPathAStar(bool[][] walkable, GridPoint start, GridPoint goal, int maxExpandedNodes, out int expandedNodes)
        {
            expandedNodes = 0;
            if (!TryGetGridDimensions(walkable, out int width, out int height))
                return null;

            if (!IsInside(start, new GridPoint(width, height)) || !IsInside(goal, new GridPoint(width, height)))
                return null;

            if (!walkable[start.Y][start.X] || !walkable[goal.Y][goal.X])
                return null;

            int maxNodes = width * height;
            int safeBudget = Math.Max(1, maxExpandedNodes);

            var openSet = new PriorityQueue<int, float>();
            var inOpenSet = new bool[maxNodes];
            var closedSet = new bool[maxNodes];
            var gScore = new float[maxNodes];
            var parent = new int[maxNodes];

            for (int i = 0; i < maxNodes; i++)
            {
                gScore[i] = float.PositiveInfinity;
                parent[i] = -1;
            }

            int startKey = ToKey(start.X, start.Y, width);
            int goalKey = ToKey(goal.X, goal.Y, width);

            gScore[startKey] = 0f;
            openSet.Enqueue(startKey, EstimateCost(start, goal));
            inOpenSet[startKey] = true;

            while (openSet.Count > 0 && expandedNodes < safeBudget)
            {
                int currentKey = openSet.Dequeue();
                if (closedSet[currentKey])
                    continue;

                closedSet[currentKey] = true;
                expandedNodes++;

                if (currentKey == goalKey)
                    return BuildPathFromParents(parent, currentKey, width);

                var current = FromKey(currentKey, width);
                float currentG = gScore[currentKey];

                for (int i = 0; i < NeighborOffsets.Length; i++)
                {
                    GridPoint offset = NeighborOffsets[i];
                    int nx = current.X + offset.X;
                    int ny = current.Y + offset.Y;

                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;

                    if (!walkable[ny][nx])
                        continue;

                    int neighborKey = ToKey(nx, ny, width);
                    if (closedSet[neighborKey])
                        continue;

                    bool isDiagonal = offset.X != 0 && offset.Y != 0;
                    float tentative = currentG + (isDiagonal ? DiagonalCost : StraightCost);
                    if (tentative >= gScore[neighborKey])
                        continue;

                    parent[neighborKey] = currentKey;
                    gScore[neighborKey] = tentative;

                    var neighbor = new GridPoint(nx, ny);
                    float fScore = tentative + EstimateCost(neighbor, goal);
                    openSet.Enqueue(neighborKey, fScore);
                    inOpenSet[neighborKey] = true;
                }
            }

            return null;
        }

        internal static bool TryResolveWalkableGoal(bool[][] walkable, GridPoint desiredGoal, int maxRadius, out GridPoint resolvedGoal)
        {
            resolvedGoal = desiredGoal;
            if (!TryGetGridDimensions(walkable, out int width, out int height))
                return false;

            if (desiredGoal.X < 0 || desiredGoal.Y < 0 || desiredGoal.X >= width || desiredGoal.Y >= height)
                return false;

            if (walkable[desiredGoal.Y][desiredGoal.X])
                return true;

            int radiusLimit = Math.Max(1, maxRadius);
            int bestDistanceSquared = int.MaxValue;
            bool found = false;

            for (int r = 1; r <= radiusLimit; r++)
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

                        int dx = x - desiredGoal.X;
                        int dy = y - desiredGoal.Y;
                        int distanceSquared = (dx * dx) + (dy * dy);
                        if (distanceSquared >= bestDistanceSquared)
                            continue;

                        resolvedGoal = new GridPoint(x, y);
                        bestDistanceSquared = distanceSquared;
                        found = true;
                    }
                }

                if (found)
                    return true;
            }

            return false;
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

        private static bool IsInside(GridPoint point, GridPoint dims)
        {
            return point.X >= 0 && point.Y >= 0 && point.X < dims.X && point.Y < dims.Y;
        }

        private static int ToKey(int x, int y, int width)
        {
            return (y * width) + x;
        }

        private static GridPoint FromKey(int key, int width)
        {
            int x = key % width;
            int y = key / width;
            return new GridPoint(x, y);
        }

        private static float EstimateCost(GridPoint a, GridPoint b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            int diagonal = Math.Min(dx, dy);
            int straight = Math.Max(dx, dy) - diagonal;
            return (diagonal * (StraightCost + DiagonalHeuristicWeight)) + (straight * StraightCost);
        }

        private static List<GridPoint> BuildPathFromParents(int[] parent, int goalKey, int width)
        {
            var result = new List<GridPoint>(128);
            int trace = goalKey;
            while (trace >= 0)
            {
                result.Add(FromKey(trace, width));
                trace = parent[trace];
            }

            result.Reverse();
            return result;
        }

        private static bool TryGetGridPos(Entity? entity, out GridPoint point)
        {
            point = default;
            if (entity == null)
                return false;

            var grid = entity.GridPosNum;
            point = new GridPoint((int)grid.X, (int)grid.Y);
            return true;
        }
    }
}