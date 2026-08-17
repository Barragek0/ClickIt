namespace ClickIt.Features.Blight.Planning;

// Single home for the blight geometry micro-helpers (squared distance for the coverage/fill radius comparisons, euclidean distance for lane merging).
internal static class BlightGeometry
{
    internal static float Sq(float v) => v * v;

    internal static float SqDist(NumVector2 a, NumVector2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    internal static float Distance(NumVector2 a, NumVector2 b)
        => MathF.Sqrt(SqDist(a, b));
}
