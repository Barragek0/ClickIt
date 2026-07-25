

namespace ClickIt.Features.Pathfinding.Grid
{
    internal static class PathGridSearch
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

        internal static List<PathfindingService.GridPoint>? FindPathAStar(bool[][] walkable, PathfindingService.GridPoint start, PathfindingService.GridPoint goal, int maxExpandedNodes, out int expandedNodes)
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
            PriorityQueue<int, float> open = new();
            bool[] closed = new bool[nodeCount];
            float[] gScore = new float[nodeCount];
            int[] parent = new int[nodeCount];

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

        internal static bool TryResolveWalkableGoal(bool[][] walkable, PathfindingService.GridPoint desiredGoal, int maxRadius, out PathfindingService.GridPoint resolvedGoal)
        {
            resolvedGoal = desiredGoal;

            if (!TryGetGridDimensions(walkable, out int width, out int height))
                return false;
            if (desiredGoal.X < 0 || desiredGoal.Y < 0 || desiredGoal.X >= width || desiredGoal.Y >= height)
                return false;
            if (walkable[desiredGoal.Y][desiredGoal.X])
                return true;

            int radiusLimit = SystemMath.Max(1, maxRadius);
            int bestDistanceSquared = int.MaxValue;

            for (int radius = 1; radius <= radiusLimit; radius++)
            {
                int minX = SystemMath.Max(0, desiredGoal.X - radius);
                int maxX = SystemMath.Min(width - 1, desiredGoal.X + radius);
                int minY = SystemMath.Max(0, desiredGoal.Y - radius);
                int maxY = SystemMath.Min(height - 1, desiredGoal.Y + radius);
                bool foundAnyInRing = false;

                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (SystemMath.Max(SystemMath.Abs(x - desiredGoal.X), SystemMath.Abs(y - desiredGoal.Y)) != radius || !walkable[y][x])
                            continue;

                        foundAnyInRing = true;
                        int dx = x - desiredGoal.X;
                        int dy = y - desiredGoal.Y;
                        int distanceSquared = (dx * dx) + (dy * dy);
                        if (distanceSquared >= bestDistanceSquared)
                            continue;

                        bestDistanceSquared = distanceSquared;
                        resolvedGoal = new PathfindingService.GridPoint(x, y);
                    }


                if (foundAnyInRing)
                    return true;
            }

            return false;
        }

        internal static bool TryResolveBestEffortGoal(
            bool[][] walkable,
            PathfindingService.GridPoint start,
            PathfindingService.GridPoint desiredGoal,
            out PathfindingService.GridPoint resolvedGoal,
            out bool usedFallback,
            out string failureReason)
        {
            resolvedGoal = default;
            usedFallback = false;
            failureReason = string.Empty;

            if (!TryGetGridDimensions(walkable, out int width, out int height))
            {
                failureReason = "Terrain/pathfinding data unavailable.";
                return false;
            }

            PathfindingService.GridPoint dims = new(width, height);
            if (!IsInside(start, dims))
            {
                failureReason = "Player grid position is outside walkable grid bounds.";
                return false;
            }

            if (IsInside(desiredGoal, dims) && TryResolveWalkableGoal(walkable, desiredGoal, 18, out PathfindingService.GridPoint directGoal))
            {
                resolvedGoal = directGoal;
                return true;
            }

            PathfindingService.GridPoint clampedGoal = ClampToGrid(desiredGoal, width, height);
            if (TryResolveWalkableGoal(walkable, clampedGoal, 24, out PathfindingService.GridPoint clampedResolvedGoal))
            {
                resolvedGoal = clampedResolvedGoal;
                usedFallback = true;
                return true;
            }

            if (TryFindFurthestReachableGoalTowardTarget(walkable, start, clampedGoal, out PathfindingService.GridPoint steppedGoal))
            {
                resolvedGoal = steppedGoal;
                usedFallback = true;
                return true;
            }

            failureReason = "No reachable walkable tile found toward target within current terrain coverage.";
            return false;
        }

        internal static bool TryGetGridPos(Entity? entity, out PathfindingService.GridPoint point)
        {
            point = default;
            if (entity == null)
                return false;

            NumVector2 grid = entity.GridPosNum;
            point = new PathfindingService.GridPoint((int)grid.X, (int)grid.Y);
            return true;
        }

        private static PathfindingService.GridPoint ClampToGrid(PathfindingService.GridPoint point, int width, int height)
            => new(SystemMath.Clamp(point.X, 0, width - 1), SystemMath.Clamp(point.Y, 0, height - 1));

        private static bool TryFindFurthestReachableGoalTowardTarget(bool[][] walkable, PathfindingService.GridPoint start, PathfindingService.GridPoint clampedGoal, out PathfindingService.GridPoint resolvedGoal)
        {
            resolvedGoal = start;
            bool hasBest = false;
            int bestProgress = 0;

            foreach (PathfindingService.GridPoint sample in EnumerateLinePoints(start, clampedGoal))
            {
                if (!TryResolveWalkableSample(walkable, sample, out PathfindingService.GridPoint candidate))
                    continue;

                int progress = SystemMath.Abs(candidate.X - start.X) + SystemMath.Abs(candidate.Y - start.Y);
                if (progress <= bestProgress)
                    continue;

                bestProgress = progress;
                resolvedGoal = candidate;
                hasBest = true;
            }

            return hasBest;
        }

        private static bool TryResolveWalkableSample(bool[][] walkable, PathfindingService.GridPoint sample, out PathfindingService.GridPoint resolved)
        {
            resolved = sample;
            if (!TryGetGridDimensions(walkable, out int width, out int height))
                return false;
            if (sample.X < 0 || sample.Y < 0 || sample.X >= width || sample.Y >= height)
                return false;
            if (walkable[sample.Y][sample.X])
                return true;

            return TryResolveWalkableGoal(walkable, sample, 6, out resolved);
        }

        private static IEnumerable<PathfindingService.GridPoint> EnumerateLinePoints(PathfindingService.GridPoint start, PathfindingService.GridPoint goal)
        {
            int dx = goal.X - start.X;
            int dy = goal.Y - start.Y;
            int steps = SystemMath.Max(SystemMath.Abs(dx), SystemMath.Abs(dy));

            if (steps <= 0)
            {
                yield return start;
                yield break;
            }

            int previousX = int.MinValue;
            int previousY = int.MinValue;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = (int)MathF.Round(start.X + (dx * t));
                int y = (int)MathF.Round(start.Y + (dy * t));
                if (x == previousX && y == previousY)
                    continue;

                previousX = x;
                previousY = y;
                yield return new PathfindingService.GridPoint(x, y);
            }
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