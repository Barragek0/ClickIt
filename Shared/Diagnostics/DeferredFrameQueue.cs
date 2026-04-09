namespace ClickIt.Shared.Diagnostics
{
    public class DeferredFrameQueue
    {
        private const int MaxBufferedItems = 8192;
        private readonly Lock _queueLock = new();
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
                    if (_items.Count >= MaxBufferedItems)
                    {
                        // Keep recent entries and shed older buffered frames to cap retained memory.
                        int removeCount = SystemMath.Max(1, _items.Count / 2);
                        _items.RemoveRange(0, removeCount);
                    }

                    (RectangleF rectangle, Color color, int thickness) frame = (rectangle, color, thickness);
                    if (_items.Count > 0 && IsSameFrame(_items[^1], frame))
                        return;

                    _items.Add(frame);
                    _pendingCount = _items.Count;
                }
            }
            catch
            {
            }
        }

        public void Flush(Graphics graphics)
        {
            if (graphics == null) return;

            lock (_queueLock)
            {
                if (_items.Count == 0)
                    return;

                (_items, _spare) = (_spare, _items);
                _items.Clear();
                _pendingCount = 0;
            }

            for (int i = 0; i < _spare.Count; i++)
            {
                (RectangleF Rectangle, Color Color, int Thickness) entry = _spare[i];
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

        internal (RectangleF Rectangle, Color Color, int Thickness)[] GetPendingFrameSnapshot()
        {
            lock (_queueLock)
            {
                return [.. _items];
            }
        }

        public void ClearPending()
        {
            lock (_queueLock)
            {
                _items.Clear();
                _spare.Clear();
                _pendingCount = 0;
            }
        }
    }
}
