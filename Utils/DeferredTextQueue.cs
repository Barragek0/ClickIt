using SharpDX;
using System;
using System.Collections.Generic;
using ExileCore;
using ExileCore.Shared.Enums;

namespace ClickIt.Utils
{
    // Small helper to centralize deferred DrawText rendering so other classes can enqueue texts
    // and a single Flush call will draw them with a provided Graphics instance.
    public class DeferredTextQueue
    {
        private readonly List<(string Text, Vector2 Position, SharpDX.Color Color, int Size, FontAlign Align)> _items = [];

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
        }
    }
}
