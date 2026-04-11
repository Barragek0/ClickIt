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
                return [];


            float scaleX = ResolveScale(goalScreen.X - startScreen.X, goal.X - start.X);
            float scaleY = ResolveScale(goalScreen.Y - startScreen.Y, goal.Y - start.Y);

            List<Vector2> points = new(gridPath.Count);
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
            float scale = SystemMath.Abs(deltaGrid) >= 0.001f
                ? deltaScreen / deltaGrid
                : SystemMath.Sign(deltaScreen) * 2.5f;

            return SystemMath.Abs(scale) < 0.01f ? 2.5f : scale;
        }

        internal static bool IsFinitePoint(float x, float y)
            => !float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y);

        private static bool TryGetScreenPointForEntity(GameController gameController, Entity? entity, out Vector2 screen)
        {
            screen = default;
            if (entity == null)
                return false;

            object? rawCamera = null;
            if (!DynamicAccess.TryGetDynamicValue(gameController, DynamicAccessProfiles.IngameState, out object? rawIngameState)
                || !DynamicAccess.TryGetDynamicValue(rawIngameState, DynamicAccessProfiles.Camera, out rawCamera))
            {
                if (!DynamicAccess.TryGetDynamicValue(gameController, DynamicAccessProfiles.Game, out object? rawGame)
                    || !DynamicAccess.TryGetDynamicValue(rawGame, DynamicAccessProfiles.IngameState, out rawIngameState)
                    || !DynamicAccess.TryGetDynamicValue(rawIngameState, DynamicAccessProfiles.Camera, out rawCamera))
                {
                    return false;
                }
            }

            if (!DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.PosNum, out object? rawPosition)
                || rawPosition is not System.Numerics.Vector3 position
                || !DynamicAccess.TryProjectWorldToScreen(rawCamera, position, out object? rawProjected)
                || !DynamicAccess.TryReadFloat(rawProjected, DynamicAccessProfiles.X, out float projectedX)
                || !DynamicAccess.TryReadFloat(rawProjected, DynamicAccessProfiles.Y, out float projectedY))
            {
                return false;
            }

            if (!IsFinitePoint(projectedX, projectedY))
                return false;

            screen = new Vector2(projectedX, projectedY);
            return true;
        }
    }
}