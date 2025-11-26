using SharpDX;
using Color = SharpDX.Color;

namespace ClickIt.Rendering
{
    public partial class StrongboxRenderer
    {
        // Internal helper used by tests to inspect enqueued frames
        internal (SharpDX.RectangleF Rectangle, Color Color, int Thickness)[] GetEnqueuedFramesForTests()
        {
            return _deferredFrameQueue.GetSnapshotForTests();
        }

        // Test seam: allows tests to exercise the private TryGetVisibleLabelRect logic
        // without constructing real ExileCore LabelOnGround/Element instances that
        // attempt to access process memory.
        internal static bool TryGetVisibleLabelRect_ForTests(string? itemPathRawCandidate, bool elementIsValid, object? maybeRectObj, SharpDX.RectangleF windowArea, out SharpDX.RectangleF rect, out string? itemPathRaw)
        {
            rect = new SharpDX.RectangleF();
            itemPathRaw = itemPathRawCandidate;
            if (string.IsNullOrEmpty(itemPathRaw)) return false;
            if (itemPathRaw.IndexOf("strongbox", System.StringComparison.OrdinalIgnoreCase) < 0) return false;

            if (!elementIsValid) return false;

            if (maybeRectObj == null) return false;

            if (maybeRectObj is SharpDX.RectangleF rf)
            {
                rect = rf;
            }
            else
            {
                return false;
            }

            if (rect.Width <= 0 || rect.Height <= 0) return false;

            var rectAbs = new SharpDX.RectangleF(rect.X + windowArea.X, rect.Y + windowArea.Y, rect.Width, rect.Height);
            if (!rectAbs.Intersects(windowArea)) return false;

            return true;
        }
    }
}
