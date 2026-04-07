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
            var worldScreenRawVector = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            worldScreenRaw = new(worldScreenRawVector.X, worldScreenRawVector.Y);
            worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);

            return ClickableProbeResolver.TryResolveNearbyClickablePoint(
                worldScreenAbsolute,
                path,
                isInsideWindowInEitherSpace,
                isClickableInEitherSpace,
                out clickPos);
        }
    }
}