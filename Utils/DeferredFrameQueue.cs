using RectangleF = SharpDX.RectangleF;
using Color = SharpDX.Color;
using Graphics = ExileCore.Graphics;
using System.Threading;

namespace ClickIt.Utils
{
    public partial class DeferredFrameQueue
    {
        private readonly object _queueLock = new();
        private List<(RectangleF Rectangle, Color Color, int Thickness)> _items = [];
        private List<(RectangleF Rectangle, Color Color, int Thickness)> _spare = [];
        private int _pendingCount;

        private static bool IsValidRect(RectangleF rectangle)
        {
            return rectangle.Width > 0 && rectangle.Height > 0
                && !float.IsNaN(rectangle.X) && !float.IsNaN(rectangle.Y)
                && !float.IsInfinity(rectangle.X) && !float.IsInfinity(rectangle.Y);
        }

        private static bool IsSameFrame((RectangleF Rectangle, Color Color, int Thickness) left, (RectangleF Rectangle, Color Color, int Thickness) right)
        {
            return left.Thickness == right.Thickness
                && left.Color.Equals(right.Color)
                && left.Rectangle.Equals(right.Rectangle);
        }

        public void Enqueue(RectangleF rectangle, Color color, int thickness)
        {
            if (thickness <= 0 || !IsValidRect(rectangle))
                return;

            // Silently ignore errors to prevent logging during render
            try
            {
                lock (_queueLock)
                {
                    var frame = (rectangle, color, thickness);
                    if (_items.Count > 0 && IsSameFrame(_items[_items.Count - 1], frame))
                        return;

                    _items.Add(frame);
                    _pendingCount = _items.Count;
                }
            }
            catch
            {
            }
        }

        public void Flush(Graphics graphics, Action<string, int> logMessage)
        {
            if (graphics == null) return;

            lock (_queueLock)
            {
                if (_items.Count == 0)
                    return;

                List<(RectangleF Rectangle, Color Color, int Thickness)> pending = _items;
                _items = _spare;
                _spare = pending;
                _items.Clear();
                _pendingCount = 0;
            }

            for (int i = 0; i < _spare.Count; i++)
            {
                var entry = _spare[i];
                try
                {
                    graphics.DrawFrame(entry.Rectangle, entry.Color, entry.Thickness);
                }
                catch
                {
                    // Intentionally empty - logging here causes recursive issues
                }
            }

            _spare.Clear();
        }

        public int GetPendingCount()
        {
            return Volatile.Read(ref _pendingCount);
        }
    }
}
