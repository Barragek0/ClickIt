namespace ClickIt.Features.Area
{
    internal static class BlockedAreaGeometryEngine
    {
        internal static bool RectsDiffer(RectangleF a, RectangleF b, float eps)
        {
            return Math.Abs(a.Width - b.Width) > eps
                || Math.Abs(a.Height - b.Height) > eps
                || Math.Abs(a.X - b.X) > eps
                || Math.Abs(a.Y - b.Y) > eps;
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangle(
            RectangleF source,
            float secondaryHeightRatio,
            bool anchorLeft,
            float secondaryWidthRatio)
        {
            float left = source.X;
            float top = source.Y;
            float right = source.Width;
            float bottom = source.Height;

            float totalWidth = right - left;
            float totalHeight = bottom - top;
            if (totalWidth <= 0f || totalHeight <= 0f)
                return (RectangleF.Empty, RectangleF.Empty);

            float squareSize = Math.Min(totalHeight, totalWidth);
            float companionWidth = Math.Max(0f, totalWidth - squareSize) * secondaryWidthRatio;
            float companionHeight = totalHeight * Math.Clamp(secondaryHeightRatio, 0f, 1f);

            RectangleF primarySquare = anchorLeft
                ? new RectangleF(left, bottom - squareSize, left + squareSize, bottom)
                : new RectangleF(right - squareSize, bottom - squareSize, right, bottom);

            if (companionWidth <= 0f || companionHeight <= 0f)
                return (primarySquare, RectangleF.Empty);

            RectangleF secondary = anchorLeft
                ? new RectangleF(primarySquare.Width, bottom - companionHeight, primarySquare.Width + companionWidth, bottom)
                : new RectangleF(primarySquare.X - companionWidth, bottom - companionHeight, primarySquare.X, bottom);

            return (primarySquare, secondary);
        }

        internal static RectangleF BuildLinkedBottomRectangle(
            RectangleF source,
            float heightRatio,
            float widthRatio,
            bool anchorLeft)
        {
            float left = source.X;
            float top = source.Y;
            float right = source.Width;
            float bottom = source.Height;

            float sourceWidth = right - left;
            float sourceHeight = bottom - top;
            if (sourceWidth <= 0f || sourceHeight <= 0f)
                return RectangleF.Empty;

            float linkedWidth = sourceWidth * Math.Max(0f, widthRatio);
            float linkedHeight = sourceHeight * Math.Clamp(heightRatio, 0f, 1f);
            if (linkedWidth <= 0f || linkedHeight <= 0f)
                return RectangleF.Empty;

            return anchorLeft
                ? new RectangleF(right, bottom - linkedHeight, right + linkedWidth, bottom)
                : new RectangleF(left - linkedWidth, bottom - linkedHeight, left, bottom);
        }

        internal static bool PointInUiRectangle(Vector2 point, RectangleF rect)
        {
            if (rect.Width <= 0f || rect.Height <= 0f)
                return false;

            float right = rect.X + rect.Width;
            float bottom = rect.Y + rect.Height;
            return point.X >= rect.X
                && point.X <= right
                && point.Y >= rect.Y
                && point.Y <= bottom;
        }

        internal static bool PointInUiRectangleAnyRepresentation(Vector2 point, RectangleF rect)
        {
            return PointInUiRectangle(point, rect);
        }
    }
}