namespace ClickIt.Shared.Game
{
    // Projects an entity's world position to raw screen coordinates through the game camera, fail-closed on any DLR read or projection failure. Single home for the projection chain that was hand-duplicated at ~8 sites.
    internal static class EntityScreenProjection
    {
        internal static bool TryProjectEntityScreen(GameController? gameController, Entity? entity, out Vector2 screenPosition)
        {
            screenPosition = default;
            if (gameController == null || entity == null)
                return false;

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
