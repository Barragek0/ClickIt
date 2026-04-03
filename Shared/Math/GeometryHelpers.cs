using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Shared.Math
{
    public static class GeometryHelpers
    {
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
