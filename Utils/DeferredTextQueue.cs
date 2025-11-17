using SharpDX;
using System;
using System.Collections.Generic;

namespace ClickIt.Utils
{
    // Small helper to centralize deferred DrawText rendering so other classes can enqueue texts
    // and a single Flush call will draw them with a provided Graphics instance.
    public class DeferredTextQueue
    {
        private readonly List<(string Text, Vector2 Position, SharpDX.Color Color, int Size)> _items = new();

        public void Enqueue(string text, Vector2 pos, SharpDX.Color color, int size)
        {
            try
            {
                _items.Add((text, pos, color, size));
            }
            catch { }
        }

        public void Flush(ExileCore.Graphics graphics, Action<string, int> logMessage)
        {
            if (graphics == null) return;
            if (_items.Count == 0) return;
            try
            {
                foreach (var entry in _items)
                {
                    try
                    {
                        graphics.DrawText(entry.Text, entry.Position, entry.Color, entry.Size);
                    }
                    catch (Exception ex)
                    {
                        logMessage?.Invoke($"[DeferredTextQueue] DrawText failed: {ex.Message}", 10);
                    }
                }
            }
            finally
            {
                _items.Clear();
            }
        }
    }
}
