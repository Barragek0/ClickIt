using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Utils
{
    public static class GeometryHelpers
    {
        // Treat edge-touching rectangles as non-overlapping to match existing click/filter semantics.
        public static bool RectanglesOverlapExclusive(RectangleF a, RectangleF b)
        {
            if (a.Right <= b.Left) return false;
            if (a.Left >= b.Right) return false;
            if (a.Bottom <= b.Top) return false;
            if (a.Top >= b.Bottom) return false;
            return true;
        }
    }
}