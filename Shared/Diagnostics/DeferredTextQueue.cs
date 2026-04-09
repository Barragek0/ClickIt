namespace ClickIt.Shared.Diagnostics
{
    public class DeferredTextQueue
    {
        private const int MaxBufferedItems = 8192;
        private readonly Lock _queueLock = new();
        private List<(string Text, Vector2 Position, Color Color, int Size, FontAlign Align)> _items = [];
        private List<(string Text, Vector2 Position, Color Color, int Size, FontAlign Align)> _spare = [];
        private int _pendingCount;

        public void Enqueue(string text, Vector2 pos, Color color, int size, FontAlign align = FontAlign.Left)
        {
            if (string.IsNullOrEmpty(text) || size <= 0)
                return;

            // Silently ignore errors to prevent logging during render
            try
            {
                lock (_queueLock)
                {
                    if (_items.Count >= MaxBufferedItems)
                    {
                        // Keep recent entries and shed older buffered lines to cap retained memory.
                        int removeCount = SystemMath.Max(1, _items.Count / 2);
                        _items.RemoveRange(0, removeCount);
                    }

                    _items.Add((text, pos, color, size, align));
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
                (string Text, Vector2 Position, Color Color, int Size, FontAlign Align) entry = _spare[i];
                try
                {
                    graphics.DrawText(entry.Text, new NumVector2(entry.Position.X, entry.Position.Y), entry.Color, entry.Align);
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

        public void ClearPending()
        {
            lock (_queueLock)
            {
                _items.Clear();
                _spare.Clear();
                _pendingCount = 0;
            }
        }

        public string[] GetPendingTextSnapshot(int startIndex = 0)
        {
            lock (_queueLock)
            {
                if (_items.Count == 0)
                    return [];

                int from = SystemMath.Clamp(startIndex, 0, _items.Count);
                int count = _items.Count - from;
                if (count <= 0)
                    return [];

                string[] result = new string[count];
                for (int i = 0; i < count; i++)
                    result[i] = _items[from + i].Text;


                return result;
            }
        }
    }
}
