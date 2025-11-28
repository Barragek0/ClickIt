using RectangleF = SharpDX.RectangleF;
using Color = SharpDX.Color;

namespace ClickIt.Utils
{
    public partial class DeferredFrameQueue
    {
        // Internal helper used by tests to inspect queued frames
        internal (RectangleF Rectangle, Color Color, int Thickness)[] GetSnapshotForTests()
        {
            return _items.ToArray();
        }
    }
}
