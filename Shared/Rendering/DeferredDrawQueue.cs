namespace ClickIt.Shared.Rendering
{
    internal enum DeferredDrawKind
    {
        Frame = 0,
        Line = 1,
        LineOnLargeMap = 2,
        LineInWorld = 3,
        CircleOnLargeMap = 4,
        CircleInWorld = 5,
        FilledCircleOnLargeMap = 6,
        FilledScreenDisc = 7,
        Text = 8
    }

    internal readonly record struct DeferredDrawItem(
        DeferredDrawKind Kind,
        RectangleF Rect,
        NumVector2 A,
        NumVector2 B,
        System.Numerics.Vector3 Center,
        float Radius,
        int Thickness,
        Color Color,
        int Segments,
        bool Filled,
        string Text,
        FontAlign Align);

    /// <summary>
    /// Unified deferred draw queue: frames, lines, circles, filled discs and text, including
    /// world-space and large-map variants. Overlays enqueue on the render thread; the host flushes
    /// once per frame. Double-buffered spare-list swap (zero steady-state allocation), 8192 cap
    /// with half-shed, and consecutive-duplicate suppression for frames. Filled discs generate
    /// their polygon points into a preallocated buffer at flush time (zero per-frame allocation).
    /// </summary>
    public sealed class DeferredDrawQueue
    {
        private const int MaxBufferedItems = 8192;
        private const int MaxPolySegments = 64;
        private readonly Lock _queueLock = new();
        private List<DeferredDrawItem> _items = [];
        private List<DeferredDrawItem> _spare = [];
        private readonly NumVector2[] _polyBuffer = new NumVector2[MaxPolySegments + 1];
        private int _pendingCount;

        private static bool IsValidRect(RectangleF rectangle)
        {
            return rectangle.Width > 0 && rectangle.Height > 0
                && !float.IsNaN(rectangle.X) && !float.IsNaN(rectangle.Y)
                && !float.IsInfinity(rectangle.X) && !float.IsInfinity(rectangle.Y);
        }

        private static bool IsSameFrame(DeferredDrawItem left, DeferredDrawItem right)
        {
            return left.Kind == DeferredDrawKind.Frame
                && left.Thickness == right.Thickness
                && left.Color.Equals(right.Color)
                && left.Rect.Equals(right.Rect);
        }

        private void Add(DeferredDrawItem item)
        {
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

                    if (_items.Count > 0 && IsSameFrame(_items[^1], item))
                        return;

                    _items.Add(item);
                    _pendingCount = _items.Count;
                }
            }
            catch
            {
            }
        }

        public void EnqueueFrame(RectangleF rectangle, Color color, int thickness)
        {
            if (thickness <= 0 || !IsValidRect(rectangle))
                return;

            Add(new DeferredDrawItem(DeferredDrawKind.Frame, rectangle, default, default, default, 0, thickness, color, 0, false, string.Empty, FontAlign.Left));
        }

        public void EnqueueLine(NumVector2 start, NumVector2 end, int thickness, Color color)
            => Add(new DeferredDrawItem(DeferredDrawKind.Line, default, start, end, default, 0, thickness, color, 0, false, string.Empty, FontAlign.Left));

        public void EnqueueLineOnLargeMap(NumVector2 start, NumVector2 end, int thickness, Color color)
            => Add(new DeferredDrawItem(DeferredDrawKind.LineOnLargeMap, default, start, end, default, 0, thickness, color, 0, false, string.Empty, FontAlign.Left));

        public void EnqueueLineInWorld(NumVector2 start, NumVector2 end, int thickness, Color color)
            => Add(new DeferredDrawItem(DeferredDrawKind.LineInWorld, default, start, end, default, 0, thickness, color, 0, false, string.Empty, FontAlign.Left));

        public void EnqueueCircleOnLargeMap(NumVector2 center, bool filled, float radius, Color color, int thickness)
            => Add(new DeferredDrawItem(DeferredDrawKind.CircleOnLargeMap, default, default, default, new System.Numerics.Vector3(center.X, center.Y, 0f), radius, thickness, color, 0, filled, string.Empty, FontAlign.Left));

        public void EnqueueCircleInWorld(System.Numerics.Vector3 center, float radius, Color color, int thickness, int segments, bool filled)
            => Add(new DeferredDrawItem(DeferredDrawKind.CircleInWorld, default, default, default, center, radius, thickness, color, segments, filled, string.Empty, FontAlign.Left));

        public void EnqueueFilledCircleOnLargeMap(NumVector2 center, bool filled, float radius, Color color, int segments)
            => Add(new DeferredDrawItem(DeferredDrawKind.FilledCircleOnLargeMap, default, default, default, new System.Numerics.Vector3(center.X, center.Y, 0f), radius, 0, color, segments, filled, string.Empty, FontAlign.Left));

        public void EnqueueFilledScreenDisc(NumVector2 center, float radius, Color color, int segments)
            => Add(new DeferredDrawItem(DeferredDrawKind.FilledScreenDisc, default, default, default, new System.Numerics.Vector3(center.X, center.Y, 0f), radius, 0, color, segments, false, string.Empty, FontAlign.Left));

        public void EnqueueText(string text, NumVector2 position, Color color, FontAlign align)
        {
            if (string.IsNullOrEmpty(text))
                return;

            Add(new DeferredDrawItem(DeferredDrawKind.Text, default, default, default, new System.Numerics.Vector3(position.X, position.Y, 0f), 0, 0, color, 0, false, text, align));
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
                DeferredDrawItem item = _spare[i];
                try
                {
                    switch (item.Kind)
                    {
                        case DeferredDrawKind.Frame:
                            graphics.DrawFrame(item.Rect, item.Color, item.Thickness);
                            break;
                        case DeferredDrawKind.Line:
                            graphics.DrawLine(item.A, item.B, item.Thickness, item.Color);
                            break;
                        case DeferredDrawKind.LineOnLargeMap:
                            graphics.DrawLineOnLargeMap(item.A, item.B, item.Thickness, item.Color);
                            break;
                        case DeferredDrawKind.LineInWorld:
                            graphics.DrawLineInWorld(item.A, item.B, item.Thickness, item.Color);
                            break;
                        case DeferredDrawKind.CircleOnLargeMap:
                            graphics.DrawCircleOnLargeMap(new NumVector2(item.Center.X, item.Center.Y), item.Filled, item.Radius, item.Color, item.Thickness);
                            break;
                        case DeferredDrawKind.CircleInWorld:
                            graphics.DrawCircleInWorld(item.Center, item.Radius, item.Color, item.Thickness, item.Segments, item.Filled);
                            break;
                        case DeferredDrawKind.FilledCircleOnLargeMap:
                            graphics.DrawFilledCircleOnLargeMap(new NumVector2(item.Center.X, item.Center.Y), item.Filled, item.Radius, item.Color, item.Segments);
                            break;
                        case DeferredDrawKind.FilledScreenDisc:
                            DrawFilledScreenDisc(graphics, new NumVector2(item.Center.X, item.Center.Y), item.Radius, item.Color, item.Segments);
                            break;
                        case DeferredDrawKind.Text:
                            graphics.DrawText(item.Text, new NumVector2(item.Center.X, item.Center.Y), item.Color, item.Align);
                            break;
                    }
                }
                catch
                {
                    // Intentionally empty - logging here causes recursive issues
                }
            }

            _spare.Clear();
        }

        private void DrawFilledScreenDisc(Graphics graphics, NumVector2 center, float radius, Color color, int segments)
        {
            int count = SystemMath.Min(SystemMath.Max(segments, 3), MaxPolySegments);
            NumVector2[] buffer = _polyBuffer;
            for (int i = 0; i <= count; i++)
            {
                float a = MathF.PI * 2f * i / count;
                buffer[i] = new NumVector2(
                    center.X + (MathF.Cos(a) * radius),
                    center.Y + (MathF.Sin(a) * radius));
            }
            // ExileCore's DrawConvexPolyFilled reads points.Length, so pass only the filled prefix —
            // passing the whole preallocated buffer feeds it stale (0,0) tail points and draws
            // spurious lines from the top-left of the screen to every disc.
            graphics.DrawConvexPolyFilled(buffer[..(count + 1)], color);
        }

        public int GetPendingCount()
        {
            return Volatile.Read(ref _pendingCount);
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
