using SharpDX;
using ExileCore.Shared.Enums;

namespace ClickIt.Utils
{
    // Small helper to centralize deferred DrawText rendering so other classes can enqueue texts
    // and a single Flush call will draw them with a provided Graphics instance.
    public class DeferredTextQueue
    {
        private readonly List<(string Text, Vector2 Position, SharpDX.Color Color, int Size, FontAlign Align)> _items = [];
        // Spare list reused during Flush to avoid allocating a snapshot array every frame.
        private readonly List<(string Text, Vector2 Position, SharpDX.Color Color, int Size, FontAlign Align)> _spare = [];

        public void Enqueue(string text, Vector2 pos, SharpDX.Color color, int size, FontAlign align = FontAlign.Left)
        {
            // Silently ignore errors to prevent logging during render
            try
            {
                _items.Add((text, pos, color, size, align));
            }
            catch
            {
                // Intentionally empty - do not log during render operations
            }
        }

        public void Flush(ExileCore.Graphics graphics, Action<string, int> logMessage)
        {
            if (graphics == null) return;
            if (_items.Count == 0) return;

            // Move items into the spare list and clear the main list. This avoids allocating
            // a new array each frame while preserving safety if Enqueue is called during Flush
            // (new items will go into _items).
            _spare.Clear();
            _spare.AddRange(_items);
            _items.Clear();

            // Silently handle errors to prevent logging during render loop
            // which can cause recursive calls and freeze/crash ExileCore
            try
            {
                foreach (var entry in _spare)
                {
                    try
                    {
                        graphics.DrawText(entry.Text, entry.Position, entry.Color, entry.Size, entry.Align);
                    }
                    catch
                    {
                        // Intentionally empty - logging here causes recursive issues
                    }
                }
            }
            catch
            {
                // Intentionally empty - logging here causes recursive issues
            }

            // Clear spare after drawing; keep capacity for reuse.
            _spare.Clear();
        }
    }
}
