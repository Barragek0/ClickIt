using RectangleF = SharpDX.RectangleF;
using Color = SharpDX.Color;
using Graphics = ExileCore.Graphics;

namespace ClickIt.Utils
{
    // Small helper to centralize deferred DrawFrame rendering so other classes can enqueue frames
    // and a single Flush call will draw them with a provided Graphics instance.
    public partial class DeferredFrameQueue
    {
        private readonly List<(RectangleF Rectangle, Color Color, int Thickness)> _items = [];
        // Spare list reused during Flush to avoid allocating a snapshot array every frame.
        // We AddRange -> Clear the main list, iterate the spare and Clear it afterwards. This keeps
        // capacity across frames and avoids frequent array allocations while still being safe
        // if callers Enqueue during a Flush (they'll add into _items after we've cleared it).
        private readonly List<(RectangleF Rectangle, Color Color, int Thickness)> _spare = [];

        public void Enqueue(RectangleF rectangle, Color color, int thickness)
        {
            // Silently ignore errors to prevent logging during render
            try
            {
                _items.Add((rectangle, color, thickness));
            }
            catch
            {
                // Intentionally empty - do not log during render operations
            }
        }


        public void Flush(Graphics graphics, Action<string, int> logMessage)
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
                        graphics.DrawFrame(entry.Rectangle, entry.Color, entry.Thickness);
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