using SharpDX;

namespace ClickIt.Features.Pathfinding.Projection
{
    internal static class ScreenPathProjector
    {
        internal static List<Vector2> BuildScreenPathApproximation(
            GameController gameController,
            List<PathfindingService.GridPoint> gridPath,
            PathfindingService.GridPoint start,
            PathfindingService.GridPoint goal,
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
                PathfindingService.GridPoint node = gridPath[i];
                float x = startScreen.X + ((node.X - start.X) * scaleX);
                float y = startScreen.Y + ((node.Y - start.Y) * scaleY);
                if (IsFinitePoint(x, y))
                    points.Add(new Vector2(x, y));
            }

            return points;
        }

        internal static float ResolveScale(float deltaScreen, float deltaGrid)
        {
            float scale = Math.Abs(deltaGrid) >= 0.001f
                ? deltaScreen / deltaGrid
                : Math.Sign(deltaScreen) * 2.5f;

            return Math.Abs(scale) < 0.01f ? 2.5f : scale;
        }

        internal static bool IsFinitePoint(float x, float y)
            => !float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y);

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
    }
}