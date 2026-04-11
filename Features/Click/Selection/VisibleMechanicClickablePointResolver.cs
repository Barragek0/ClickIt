namespace ClickIt.Features.Click.Selection
{
    internal static class VisibleMechanicClickablePointResolver
    {
        internal static bool TryResolveEntityClickablePoint(
            GameController gameController,
            Entity entity,
            string path,
            Vector2 windowTopLeft,
            Func<Vector2, bool> isInsideWindowInEitherSpace,
            Func<Vector2, string, bool> isClickableInEitherSpace,
            out Vector2 clickPos,
            out Vector2 worldScreenRaw,
            out Vector2 worldScreenAbsolute)
        {
            clickPos = default;
            worldScreenRaw = default;
            worldScreenAbsolute = default;

            if (gameController == null || entity == null)
                return false;

            try
            {
                if (!TryProjectEntityScreenPosition(gameController, entity, out worldScreenRaw))
                {
                    return false;
                }

                worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);

                return ClickableProbeResolver.TryResolveNearbyClickablePoint(
                    worldScreenAbsolute,
                    path,
                    isInsideWindowInEitherSpace,
                    isClickableInEitherSpace,
                    out clickPos);
            }
            catch
            {
                clickPos = default;
                worldScreenRaw = default;
                worldScreenAbsolute = default;
                return false;
            }
        }

        private static bool TryProjectEntityScreenPosition(GameController gameController, Entity entity, out Vector2 screenPosition)
        {
            screenPosition = default;

            if (!DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.PosNum, out object? rawPosition)
                || rawPosition is not System.Numerics.Vector3 position
                || !DynamicAccess.TryGetDynamicValue(gameController, DynamicAccessProfiles.Game, out object? rawGame)
                || !DynamicAccess.TryGetDynamicValue(rawGame, DynamicAccessProfiles.IngameState, out object? rawIngameState)
                || !DynamicAccess.TryGetDynamicValue(rawIngameState, DynamicAccessProfiles.Camera, out object? rawCamera)
                || !DynamicAccess.TryProjectWorldToScreen(rawCamera, position, out object? rawProjected)
                || !DynamicAccess.TryReadFloat(rawProjected, DynamicAccessProfiles.X, out float projectedX)
                || !DynamicAccess.TryReadFloat(rawProjected, DynamicAccessProfiles.Y, out float projectedY))
            {
                return false;
            }

            screenPosition = new(projectedX, projectedY);
            return true;
        }
    }
}