using SharpDX;
using RectangleF = SharpDX.RectangleF;
using Color = SharpDX.Color;

namespace ClickIt.Utils
{
    // Small helper to centralize deferred DrawFrame rendering so other classes can enqueue frames
    // and a single Flush call will draw them with a provided Graphics instance.
    public class DeferredFrameQueue
    {
        private readonly List<(RectangleF Rectangle, Color Color, int Thickness)> _items = new List<(RectangleF, Color, int)>();

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

        // Internal helper used by tests to inspect queued frames
        internal (RectangleF Rectangle, Color Color, int Thickness)[] GetSnapshotForTests()
        {
            return _items.ToArray();
        }

        public void Flush(ExileCore.Graphics graphics, Action<string, int> logMessage)
        {
            if (graphics == null) return;
            if (_items.Count == 0) return;

            // Create a snapshot to avoid issues if Enqueue is called during Flush
            var itemsSnapshot = _items.ToArray();
            _items.Clear();

            // Silently handle errors to prevent logging during render loop
            // which can cause recursive calls and freeze/crash ExileCore
            try
            {
                foreach (var entry in itemsSnapshot)
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
        }
    }
}