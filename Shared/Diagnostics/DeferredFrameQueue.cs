namespace ClickIt.Shared.Diagnostics
{
    public class DeferredFrameQueue
    {
        private const int MaxBufferedItems = 8192;
        private readonly Lock _queueLock = new();
        private List<(RectangleF Rectangle, Color Color, int Thickness, RenderSection Section)> _items = [];
        private List<(RectangleF Rectangle, Color Color, int Thickness, RenderSection Section)> _spare = [];
        private int _pendingCount;

        // Ambient render section set by the overlay host around each overlay's Draw, so flushed frames can be attributed back to the feature that enqueued them. Render-thread only.
        internal RenderSection CurrentSection { get; set; } = RenderSection.Unknown;

        private static bool IsValidRect(RectangleF rectangle)
        {
            return rectangle.Width > 0 && rectangle.Height > 0
                && !float.IsNaN(rectangle.X) && !float.IsNaN(rectangle.Y)
                && !float.IsInfinity(rectangle.X) && !float.IsInfinity(rectangle.Y);
        }

        private static bool IsSameFrame((RectangleF Rectangle, Color Color, int Thickness, RenderSection Section) left, (RectangleF Rectangle, Color Color, int Thickness, RenderSection Section) right)
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

                    (RectangleF rectangle, Color color, int thickness, RenderSection section) frame = (rectangle, color, thickness, CurrentSection);
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

        public void Flush(Graphics graphics, Action<RenderSection, double>? recordSectionFlush = null)
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

            double[]? sectionMs = recordSectionFlush != null ? new double[15] : null;

            for (int i = 0; i < _spare.Count; i++)
            {
                (RectangleF Rectangle, Color Color, int Thickness, RenderSection Section) entry = _spare[i];
                long start = Stopwatch.GetTimestamp();
                try
                {
                    graphics.DrawFrame(entry.Rectangle, entry.Color, entry.Thickness);
                }
                catch
                {
                    // Intentionally empty - logging here causes recursive issues
                }

                if (sectionMs != null)
                {
                    int sectionIndex = (int)entry.Section;
                    if (sectionIndex >= 0 && sectionIndex < sectionMs.Length)
                        sectionMs[sectionIndex] += GetElapsedMs(start);
                }
            }

            if (sectionMs != null)
            {
                for (int s = 0; s < sectionMs.Length; s++)
                {
                    if (sectionMs[s] > 0)
                        recordSectionFlush!((RenderSection)s, sectionMs[s]);
                }
            }

            _spare.Clear();
        }

        private static double GetElapsedMs(long startTimestamp)
            => StopwatchMath.ElapsedMs(startTimestamp);

        public int GetPendingCount()
        {
            return Volatile.Read(ref _pendingCount);
        }

        internal (RectangleF Rectangle, Color Color, int Thickness)[] GetPendingFrameSnapshot()
        {
            lock (_queueLock)
            {
                (RectangleF Rectangle, Color Color, int Thickness)[] result = new (RectangleF, Color, int)[_items.Count];
                for (int i = 0; i < _items.Count; i++)
                {
                    (RectangleF Rectangle, Color Color, int Thickness, _) = _items[i];
                    result[i] = (Rectangle, Color, Thickness);
                }
                return result;
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
