namespace ClickIt.Features.Click.Safety
{
    internal sealed class ClickSafetyPolicy
    {
        internal static bool IsWithinSearchRadius(ClickItSettings settings, Entity? entity)
            => !settings.ClickDistanceEnabled.Value
                || (DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out float distance)
                    && distance >= 0f && distance <= settings.ClickDistance.Value);

        public static bool IsPointClickableInEitherSpace(Vector2 clientPoint, Vector2 windowTopLeft, Func<Vector2, string, bool> clickabilityCheck, string path)
        {
            return clickabilityCheck(clientPoint, path)
                || clickabilityCheck(clientPoint + windowTopLeft, path);
        }

        public static bool IsCursorInsideWindow(RectangleF windowArea, Vector2 cursorAbsolute)
        {
            return cursorAbsolute.X >= windowArea.X
                && cursorAbsolute.Y >= windowArea.Y
                && cursorAbsolute.X <= windowArea.X + windowArea.Width
                && cursorAbsolute.Y <= windowArea.Y + windowArea.Height;
        }
    }
}
