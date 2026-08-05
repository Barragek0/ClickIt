namespace ClickIt.Features.Click.Core
{
    internal static class OffscreenProjectionMath
    {
        internal static bool TryResolveDirectionalWalkClickPosition(
            RectangleF windowRect,
            Vector2 targetScreen,
            string targetPath,
            Func<Vector2, string, bool> pointIsInClickableArea,
            out Vector2 clickPos)
        {
            clickPos = default;
            if (windowRect.Width <= 0 || windowRect.Height <= 0)
                return false;

            float insetX = SystemMath.Max(28f, windowRect.Width * 0.10f);
            float insetY = SystemMath.Max(28f, windowRect.Height * 0.10f);
            float safeLeft = windowRect.Left + insetX;
            float safeRight = windowRect.Right - insetX;
            float safeTop = windowRect.Top + insetY;
            float safeBottom = windowRect.Bottom - insetY;

            Vector2 center = new(windowRect.X + (windowRect.Width * 0.5f), windowRect.Y + (windowRect.Height * 0.5f));
            Vector2 direction = targetScreen - center;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return false;

            // Search from the target back toward the screen center (t = 1.05 .. 0.35). The clamped
            // target is the primary fallback — it keeps the walk click as close to the target as the
            // window allows. Only when that too is unusable (target under the buff bar / minimap
            // strip) do we fall back to a point just off-center, which is still in the play area and
            // walks the same direction.
            for (int i = 0; i <= 7; i++)
            {
                float t = 1.05f - (i * 0.1f);
                Vector2 candidate = center + (direction * t);
                if (!OffscreenTargetResolver.IsInsideWindow(windowRect, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!pointIsInClickableArea(candidate, targetPath))
                    continue;

                clickPos = candidate;
                return true;
            }

            Vector2 clamped = new(
                SystemMath.Clamp(targetScreen.X, safeLeft, safeRight),
                SystemMath.Clamp(targetScreen.Y, safeTop, safeBottom));
            if (pointIsInClickableArea(clamped, targetPath))
            {
                clickPos = clamped;
                return true;
            }

            for (int i = 2; i >= 0; i--)
            {
                float t = 0.25f - ((2 - i) * 0.1f);
                Vector2 candidate = center + (direction * t);
                if (!OffscreenTargetResolver.IsInsideWindow(windowRect, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!pointIsInClickableArea(candidate, targetPath))
                    continue;

                clickPos = candidate;
                return true;
            }

            return false;
        }
    }
}