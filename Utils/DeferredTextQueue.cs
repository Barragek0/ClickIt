using SharpDX;
using ExileCore.Shared.Enums;
using System.Threading;

namespace ClickIt.Utils
{
    // Small helper to centralize deferred DrawText rendering so other classes can enqueue texts
    // and a single Flush call will draw them with a provided Graphics instance.
    public class DeferredTextQueue
    {
        private readonly object _queueLock = new();
        private List<(string Text, Vector2 Position, SharpDX.Color Color, int Size, FontAlign Align)> _items = [];
        private List<(string Text, Vector2 Position, SharpDX.Color Color, int Size, FontAlign Align)> _spare = [];
        private int _pendingCount;

        public void Enqueue(string text, Vector2 pos, SharpDX.Color color, int size, FontAlign align = FontAlign.Left)
        {
            if (string.IsNullOrEmpty(text) || size <= 0)
                return;

            // Silently ignore errors to prevent logging during render
            try
            {
                lock (_queueLock)
                {
                    _items.Add((text, pos, color, size, align));
                    _pendingCount = _items.Count;
                }
            }
            catch
            {
                // Intentionally empty - do not log during render operations
            }
        }

        public void Flush(ExileCore.Graphics graphics, Action<string, int> logMessage)
        {
            if (graphics == null) return;

            // Swap pending and spare lists under lock so Flush can iterate without copying.
            lock (_queueLock)
            {
                if (_items.Count == 0)
                    return;

                List<(string Text, Vector2 Position, SharpDX.Color Color, int Size, FontAlign Align)> pending = _items;
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
                    graphics.DrawText(entry.Text, entry.Position, entry.Color, entry.Size, entry.Align);
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
            return Volatile.Read(ref _pendingCount);
        }
    }
}
