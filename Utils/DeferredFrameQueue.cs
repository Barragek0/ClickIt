using SharpDX;
using System;
using System.Collections.Generic;

namespace ClickIt.Utils
{
    // Small helper to centralize deferred DrawFrame rendering so other classes can enqueue frames
    // and a single Flush call will draw them with a provided Graphics instance.
    public class DeferredFrameQueue
    {
        private readonly List<(SharpDX.RectangleF Rectangle, SharpDX.Color Color, int Thickness)> _items = new();

        public void Enqueue(SharpDX.RectangleF rectangle, SharpDX.Color color, int thickness)
        {
            try
            {
                _items.Add((rectangle, color, thickness));
            }
            catch { }
        }

        public void Flush(ExileCore.Graphics graphics, Action<string, int> logMessage)
        {
            if (graphics == null) return;
            if (_items.Count == 0) return;

            // Create a snapshot to avoid issues if Enqueue is called during Flush
            var itemsSnapshot = _items.ToArray();
            _items.Clear();

            try
            {
                foreach (var entry in itemsSnapshot)
                {
                    try
                    {
                        graphics.DrawFrame(entry.Rectangle, entry.Color, entry.Thickness);
                    }
                    catch (Exception ex)
                    {
                        logMessage?.Invoke($"[DeferredFrameQueue] DrawFrame failed: {ex.Message}", 10);
                    }
                }
            }
            catch (Exception ex)
            {
                logMessage?.Invoke($"[DeferredFrameQueue] Flush failed: {ex.Message}", 10);
            }
        }
    }
}