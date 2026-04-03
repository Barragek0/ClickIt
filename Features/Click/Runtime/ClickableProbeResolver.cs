using SharpDX;

namespace ClickIt.Features.Click.Runtime
{
    internal static class ClickableProbeResolver
    {
        private static readonly Vector2[] NearbyClickProbeOffsets =
        [
            new Vector2(0f, 0f),
            new Vector2(12f, 0f),
            new Vector2(-12f, 0f),
            new Vector2(0f, 12f),
            new Vector2(0f, -12f),
            new Vector2(24f, 0f),
            new Vector2(-24f, 0f),
            new Vector2(0f, 24f),
            new Vector2(0f, -24f)
        ];

        internal static bool TryResolveNearbyClickablePoint(
            Vector2 center,
            string path,
            Func<Vector2, bool> isInsideWindow,
            Func<Vector2, string, bool> isClickable,
            out Vector2 clickPos)
        {
            for (int i = 0; i < NearbyClickProbeOffsets.Length; i++)
            {
                Vector2 probe = center + NearbyClickProbeOffsets[i];
                if (!isInsideWindow(probe))
                    continue;
                if (!isClickable(probe, path))
                    continue;

                clickPos = probe;
                return true;
            }

            clickPos = default;
            return false;
        }
    }
}