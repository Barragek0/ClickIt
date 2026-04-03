using SharpDX;
using RectangleF = SharpDX.RectangleF;

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

            float insetX = Math.Max(28f, windowRect.Width * 0.10f);
            float insetY = Math.Max(28f, windowRect.Height * 0.10f);
            float safeLeft = windowRect.Left + insetX;
            float safeRight = windowRect.Right - insetX;
            float safeTop = windowRect.Top + insetY;
            float safeBottom = windowRect.Bottom - insetY;

            Vector2 center = new(windowRect.X + (windowRect.Width * 0.5f), windowRect.Y + (windowRect.Height * 0.5f));
            Vector2 direction = targetScreen - center;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return false;

            for (float t = 1.05f; t >= 0.30f; t -= 0.1f)
            {
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
                Math.Clamp(targetScreen.X, safeLeft, safeRight),
                Math.Clamp(targetScreen.Y, safeTop, safeBottom));

            if (!pointIsInClickableArea(clamped, targetPath))
                return false;

            clickPos = clamped;
            return true;
        }
    }
}