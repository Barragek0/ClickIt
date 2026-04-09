namespace ClickIt.Features.Click.Runtime
{
    internal static class MovementSkillCastPointResolver
    {
        internal static bool TryResolveCastPoint(RectangleF window, Vector2 targetScreen, string targetPath, Func<Vector2, string, bool> pointIsInClickableArea, out Vector2 castPoint)
        {
            castPoint = default;
            if (window.Width <= 0 || window.Height <= 0)
                return false;

            float insetX = SystemMath.Max(24f, window.Width * 0.12f);
            float insetY = SystemMath.Max(24f, window.Height * 0.12f);
            float safeLeft = window.Left + insetX;
            float safeRight = window.Right - insetX;
            float safeTop = window.Top + insetY;
            float safeBottom = window.Bottom - insetY;

            Vector2 center = new(window.X + (window.Width * 0.5f), window.Y + (window.Height * 0.5f));
            Vector2 direction = targetScreen - center;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return false;

            for (float t = 1.65f; t >= 0.70f; t -= 0.1f)
            {
                Vector2 candidate = center + (direction * t);
                if (!OffscreenTargetResolver.IsInsideWindow(window, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!pointIsInClickableArea(candidate, targetPath))
                    continue;

                castPoint = candidate;
                return true;
            }

            Vector2 clamped = new(
                SystemMath.Clamp(targetScreen.X, safeLeft, safeRight),
                SystemMath.Clamp(targetScreen.Y, safeTop, safeBottom));

            if (!pointIsInClickableArea(clamped, targetPath))
                return false;

            castPoint = clamped;
            return true;
        }
    }
}