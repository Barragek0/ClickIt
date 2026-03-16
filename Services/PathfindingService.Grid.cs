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

            GridPoint dims = new GridPoint(width, height);
            if (!IsInside(start, dims) || !IsInside(goal, dims))
                return null;
            if (!walkable[start.Y][start.X] || !walkable[goal.Y][goal.X])
                return null;

            int nodeCount = width * height;
            int budget = Math.Max(1, maxExpandedNodes);

            var open = new PriorityQueue<int, float>();
            var closed = new bool[nodeCount];
            var gScore = new float[nodeCount];
            var parent = new int[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                gScore[i] = float.PositiveInfinity;
                parent[i] = -1;
            }

            int startKey = ToKey(start.X, start.Y, width);
            int goalKey = ToKey(goal.X, goal.Y, width);
            gScore[startKey] = 0f;
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

                GridPoint current = FromKey(currentKey, width);
                float currentCost = gScore[currentKey];

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
                    if (closed[neighborKey])
                        continue;

                    float moveCost = offset.X != 0 && offset.Y != 0 ? DiagonalCost : StraightCost;
                    float tentative = currentCost + moveCost;
                    if (tentative >= gScore[neighborKey])
                        continue;

                    parent[neighborKey] = currentKey;
                    gScore[neighborKey] = tentative;

                    GridPoint neighbor = new GridPoint(nx, ny);
                    open.Enqueue(neighborKey, tentative + EstimateCost(neighbor, goal));
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

            for (int radius = 1; radius <= radiusLimit; radius++)
            {
                int minX = Math.Max(0, desiredGoal.X - radius);
                int maxX = Math.Min(width - 1, desiredGoal.X + radius);
                int minY = Math.Max(0, desiredGoal.Y - radius);
                int maxY = Math.Min(height - 1, desiredGoal.Y + radius);

                bool foundAnyInRing = false;

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (Math.Max(Math.Abs(x - desiredGoal.X), Math.Abs(y - desiredGoal.Y)) != radius)
                            continue;
                        if (!walkable[y][x])
                            continue;

                        foundAnyInRing = true;
                        int dx = x - desiredGoal.X;
                        int dy = y - desiredGoal.Y;
                        int distanceSquared = (dx * dx) + (dy * dy);
                        if (distanceSquared >= bestDistanceSquared)
                            continue;

                        bestDistanceSquared = distanceSquared;
                        resolvedGoal = new GridPoint(x, y);
                    }
                }

                if (foundAnyInRing)
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
            => point.X >= 0 && point.Y >= 0 && point.X < dims.X && point.Y < dims.Y;

        private static int ToKey(int x, int y, int width) => (y * width) + x;

        private static GridPoint FromKey(int key, int width)
            => new GridPoint(key % width, key / width);

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
            for (int trace = goalKey; trace >= 0; trace = parent[trace])
            {
                result.Add(FromKey(trace, width));
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