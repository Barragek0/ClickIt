using SharpDX;

namespace ClickIt.Shared.Math
{
    internal static class CoordinateSpace
    {
        internal static Vector2 ToClient(Vector2 absolutePoint, Vector2 windowTopLeft)
            => absolutePoint - windowTopLeft;

        internal static float DistanceSquared(Vector2 a, Vector2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy);
        }

        internal static float DistanceSquaredInEitherSpace(Vector2 cursorAbsolute, Vector2 candidatePoint, Vector2 windowTopLeft)
        {
            float absoluteDistanceSq = DistanceSquared(cursorAbsolute, candidatePoint);
            Vector2 cursorClient = ToClient(cursorAbsolute, windowTopLeft);
            float clientDistanceSq = DistanceSquared(cursorClient, candidatePoint);
            return global::System.Math.Min(absoluteDistanceSq, clientDistanceSq);
        }
    }
}