using ClickIt.Services;
using ExileCore;
using SharpDX;
using Color = SharpDX.Color;
using Graphics = ExileCore.Graphics;

namespace ClickIt.Rendering
{
    public sealed partial class PathfindingRenderer
    {
        private void RenderFallbackScreenPath(Graphics graphics)
        {
            IReadOnlyList<Vector2> points = _pathfindingService.GetLatestScreenPath();
            if (points.Count < 2)
                return;

            for (int i = 1; i < points.Count; i++)
            {
                DrawLine(graphics, points[i - 1], points[i], 2, Color.Red);
            }
        }

        private static bool TryGetPlayerGrid(GameController gameController, out PathfindingService.GridPoint playerGrid)
        {
            playerGrid = default;
            var player = gameController.Player ?? gameController.Game?.IngameState?.Data?.LocalPlayer;
            if (player == null)
                return false;

            var grid = player.GridPosNum;
            playerGrid = new PathfindingService.GridPoint((int)grid.X, (int)grid.Y);
            return true;
        }

        private static int FindClosestGridPathIndex(IReadOnlyList<PathfindingService.GridPoint> path, PathfindingService.GridPoint player)
        {
            int bestIndex = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < path.Count; i++)
            {
                int distance = Math.Abs(path[i].X - player.X) + Math.Abs(path[i].Y - player.Y);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static void DrawLine(Graphics graphics, Vector2 start, Vector2 end, int thickness, Color color)
        {
            graphics.DrawLine(start, end, thickness, color);
        }
    }
}