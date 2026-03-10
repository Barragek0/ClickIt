using System;
using System.Collections;
using System.Runtime.CompilerServices;
using ClickIt.Rendering;
using ClickIt.Utils;
using ExileCore.Shared.Enums;
using SharpDX;

namespace ClickIt.Tests.TestUtils
{
    internal static class AltarDisplayRendererTestHelper
    {
        public static (AltarDisplayRenderer renderer, DeferredTextQueue dtq, DeferredFrameQueue dfq) CreateRendererWithQueues(ClickItSettings settings)
        {
            var renderer = (AltarDisplayRenderer)RuntimeHelpers.GetUninitializedObject(typeof(AltarDisplayRenderer));
            var dtq = new DeferredTextQueue();
            var dfq = new DeferredFrameQueue();

            PrivateFieldAccessor.Set(renderer, "_settings", settings);
            PrivateFieldAccessor.Set(renderer, "_deferredTextQueue", dtq);
            PrivateFieldAccessor.Set(renderer, "_deferredFrameQueue", dfq);
            return (renderer, dtq, dfq);
        }

        public static bool FrameExists(DeferredFrameQueue queue, RectangleF rect, Color color, int? thickness = null)
        {
            var itemsObj = PrivateFieldAccessor.Get<object>(queue, "_items");
            foreach (var entry in (IEnumerable)itemsObj)
            {
                var tuple = (ValueTuple<RectangleF, Color, int>)entry;
                if (tuple.Item1.Equals(rect) && tuple.Item2 == color && (!thickness.HasValue || tuple.Item3 == thickness.Value))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool AnyTextContains(DeferredTextQueue queue, string fragment)
        {
            var itemsObj = PrivateFieldAccessor.Get<object>(queue, "_items");
            foreach (var entry in (IEnumerable)itemsObj)
            {
                var tuple = (ValueTuple<string, Vector2, Color, int, FontAlign>)entry;
                if (tuple.Item1.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}