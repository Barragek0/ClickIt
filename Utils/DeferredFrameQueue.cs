using RectangleF = SharpDX.RectangleF;
using Color = SharpDX.Color;
using Graphics = ExileCore.Graphics;

namespace ClickIt.Utils
{
    // Small helper to centralize deferred DrawFrame rendering so other classes can enqueue frames
    // and a single Flush call will draw them with a provided Graphics instance.
    public partial class DeferredFrameQueue
    {
        private readonly object _queueLock = new();
        private List<(RectangleF Rectangle, Color Color, int Thickness)> _items = [];
        private List<(RectangleF Rectangle, Color Color, int Thickness)> _spare = [];

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
                }
            }
            catch
            {
                // Intentionally empty - do not log during render operations
            }
        }

        public void Flush(Graphics graphics, Action<string, int> logMessage)
        {
            if (graphics == null) return;

            // Swap pending and spare lists under lock so Flush can iterate without copying.
            lock (_queueLock)
            {
                if (_items.Count == 0)
                    return;

                List<(RectangleF Rectangle, Color Color, int Thickness)> pending = _items;
                _items = _spare;
                _spare = pending;
                _items.Clear();
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

            // Clear spare after drawing; keep capacity for reuse.
            _spare.Clear();
        }

        public int GetPendingCount()
        {
            lock (_queueLock)
            {
                return _items.Count;
            }
        }
    }
}
