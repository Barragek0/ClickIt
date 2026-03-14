using RectangleF = SharpDX.RectangleF;
using Color = SharpDX.Color;

namespace ClickIt.Utils
{
    public partial class DeferredFrameQueue
    {
        internal (RectangleF Rectangle, Color Color, int Thickness)[] GetSnapshotForTests()
        {
            lock (_queueLock)
            {
                return _items.ToArray();
            }
        }
    }
}
