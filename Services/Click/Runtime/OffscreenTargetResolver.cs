using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services.Click.Runtime
{
    internal sealed class OffscreenTargetResolver(GameController gameController, PathfindingService pathfindingService)
    {
        private readonly GameController _gameController = gameController;
        private readonly PathfindingService _pathfindingService = pathfindingService;

        internal int GetRemainingOffscreenPathNodeCount()
        {
            var path = _pathfindingService.GetLatestGridPath();
            var player = _gameController.Player;
            if (player == null)
                return 0;

            int nearest = FindClosestPathIndexToPlayer(
                path,
                new PathfindingService.GridPoint((int)player.GridPosNum.X, (int)player.GridPosNum.Y));

            return CountRemainingPathNodes(path, nearest);
        }

        internal bool TryResolveOffscreenTargetScreenPointFromPath(out Vector2 targetScreen)
        {
            targetScreen = default;

            var player = _gameController.Player;
            if (player == null)
                return false;

            var path = _pathfindingService.GetLatestGridPath();
            if (path.Count < 2)
                return false;

            PathfindingService.GridPoint playerGrid = new((int)player.GridPosNum.X, (int)player.GridPosNum.Y);
            int nearestIndex = FindClosestPathIndexToPlayer(path, playerGrid);
            if (nearestIndex < 0)
                return false;

            if (!TryGetSmoothedPathDirection(path, playerGrid, nearestIndex, out float deltaX, out float deltaY))
                return false;

            RectangleF window = _gameController.Window.GetWindowRectangleTimeCache;
            Vector2 center = GetWindowCenter(window);
            float radius = Math.Min(window.Width, window.Height) * 0.30f;
            return TryComputeGridDirectionPoint(center, deltaX, deltaY, radius, out targetScreen);
        }

        internal bool TryResolveOffscreenTargetScreenPoint(Entity target, out Vector2 targetScreen)
        {
            targetScreen = default;

            RectangleF window = _gameController.Window.GetWindowRectangleTimeCache;
            Vector2 center = GetWindowCenter(window);
            float radius = Math.Min(window.Width, window.Height) * 0.30f;
            TryGetGridDelta(target, out float deltaX, out float deltaY);

            if (target.Type == ExileCore.Shared.Enums.EntityType.WorldItem
                && TryComputeGridDirectionPoint(center, deltaX, deltaY, radius, out targetScreen))
            {
                return true;
            }

            var raw = _gameController.Game.IngameState.Camera.WorldToScreen(target.PosNum);
            Vector2 projected = new(raw.X, raw.Y);
            if (IsFinite(projected) && !IsNearCorner(projected, window))
            {
                targetScreen = projected;
                return true;
            }

            return TryComputeGridDirectionPoint(center, deltaX, deltaY, radius, out targetScreen);
        }

        internal static int CountRemainingPathNodes(IReadOnlyList<PathfindingService.GridPoint>? path, int nearestIndex)
        {
            if (path == null || path.Count == 0 || nearestIndex < 0)
                return 0;

            int index = Math.Min(path.Count - 1, nearestIndex);
            return Math.Max(0, path.Count - (index + 1));
        }

        internal static bool TryGetSmoothedPathDirection(
            IReadOnlyList<PathfindingService.GridPoint> path,
            PathfindingService.GridPoint playerGrid,
            int nearestIndex,
            out float deltaX,
            out float deltaY)
        {
            deltaX = 0f;
            deltaY = 0f;

            if (path == null || path.Count < 2 || nearestIndex < 0)
                return false;

            int start = Math.Min(path.Count - 1, nearestIndex + 1);
            int end = Math.Min(path.Count - 1, nearestIndex + 8);
            if (end < start)
                return false;

            float weightedX = 0f;
            float weightedY = 0f;
            float weightTotal = 0f;

            for (int i = start; i <= end; i++)
            {
                PathfindingService.GridPoint node = path[i];
                float dx = node.X - playerGrid.X;
                float dy = node.Y - playerGrid.Y;
                if (Math.Abs(dx) + Math.Abs(dy) < 0.001f)
                    continue;

                float weight = (i - start) + 1f;
                weightedX += dx * weight;
                weightedY += dy * weight;
                weightTotal += weight;
            }

            if (weightTotal <= 0f)
                return false;

            deltaX = weightedX / weightTotal;
            deltaY = weightedY / weightTotal;
            return Math.Abs(deltaX) + Math.Abs(deltaY) >= 0.001f;
        }

        internal static bool TryComputeGridDirectionPoint(Vector2 center, float deltaGridX, float deltaGridY, float radius, out Vector2 point)
        {
            point = default;
            if (radius <= 0f)
                return false;

            Vector2 axis = new(deltaGridX - deltaGridY, -(deltaGridX + deltaGridY) * 0.65f);
            float lengthSquared = (axis.X * axis.X) + (axis.Y * axis.Y);
            if (lengthSquared < 0.001f)
                return false;

            float invLength = 1f / MathF.Sqrt(lengthSquared);
            point = center + new Vector2(axis.X * invLength * radius, axis.Y * invLength * radius);
            return true;
        }

        internal static int FindClosestPathIndexToPlayer(IReadOnlyList<PathfindingService.GridPoint> path, PathfindingService.GridPoint playerGrid)
        {
            if (path == null || path.Count == 0)
                return -1;

            int bestIndex = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                int distance = Math.Abs(path[i].X - playerGrid.X) + Math.Abs(path[i].Y - playerGrid.Y);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        internal static bool IsInsideWindow(RectangleF window, Vector2 point)
            => point.X >= window.Left && point.X <= window.Right && point.Y >= window.Top && point.Y <= window.Bottom;

        private static Vector2 GetWindowCenter(RectangleF window)
            => new(window.X + (window.Width * 0.5f), window.Y + (window.Height * 0.5f));

        private void TryGetGridDelta(Entity target, out float deltaX, out float deltaY)
        {
            var player = _gameController.Player;
            if (player == null)
            {
                deltaX = 0f;
                deltaY = 0f;
                return;
            }

            deltaX = target.GridPosNum.X - player.GridPosNum.X;
            deltaY = target.GridPosNum.Y - player.GridPosNum.Y;
        }

        private static bool IsFinite(Vector2 point)
            => !float.IsNaN(point.X) && !float.IsInfinity(point.X) && !float.IsNaN(point.Y) && !float.IsInfinity(point.Y);

        private static bool IsNearCorner(Vector2 point, RectangleF window)
        {
            float marginX = window.Width * 0.05f;
            float marginY = window.Height * 0.05f;

            bool nearHorizontal = point.X <= window.Left + marginX || point.X >= window.Right - marginX;
            bool nearVertical = point.Y <= window.Top + marginY || point.Y >= window.Bottom - marginY;
            return nearHorizontal && nearVertical;
        }
    }
}