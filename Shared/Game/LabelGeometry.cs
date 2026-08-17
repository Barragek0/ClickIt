namespace ClickIt.Shared.Game
{
    internal static class LabelGeometry
    {
        internal static bool TryGetLabelRect(LabelOnGround? label, out RectangleF rect)
        {
            rect = default;

            Element? element = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawElement)
                ? rawElement as Element
                : null;
            bool isValid = element != null
                && (DynamicAccess.TryReadBool(element, DynamicAccessProfiles.IsValid, out bool resolvedIsValid)
                    ? resolvedIsValid
                    : element.IsValid);
            if (element == null || !isValid)
                return false;

            return TryGetElementRect(element, out rect);
        }

        // Rect of an arbitrary element (e.g. a label's Child[0] frame), safe for render-thread use.
        internal static bool TryGetElementRect(Element? element, out RectangleF rect)
        {
            rect = default;
            if (element == null)
                return false;

            try
            {
                object? maybeRect = element.GetClientRect();
                if (maybeRect is not RectangleF resolvedRect)
                    return false;

                if (resolvedRect.Width <= 0 || resolvedRect.Height <= 0)
                    return false;

                rect = resolvedRect;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGetLabelRectOnScreen(LabelOnGround? label, RectangleF windowArea, out RectangleF rect)
        {
            if (!TryGetLabelRect(label, out RectangleF labelRect))
            {
                rect = default;
                return false;
            }

            return TryProjectOnScreen(labelRect, windowArea, out rect);
        }

        internal static bool TryGetElementRectOnScreen(Element? element, RectangleF windowArea, out RectangleF rect)
        {
            if (!TryGetElementRect(element, out RectangleF elementRect))
            {
                rect = default;
                return false;
            }

            return TryProjectOnScreen(elementRect, windowArea, out rect);
        }

        // Culls offscreen labels/rects: an element that left the window reports a stale client rect
        // that would render as a box near a screen corner, so overlays must skip it until it is visible again.
        internal static bool IsRectOnScreen(RectangleF rect, RectangleF windowArea)
            => TryProjectOnScreen(rect, windowArea, out _);

        private static bool TryProjectOnScreen(RectangleF rect, RectangleF windowArea, out RectangleF result)
        {
            if (windowArea.Width > 0 && windowArea.Height > 0)
            {
                RectangleF rectAbs = new(rect.X + windowArea.X, rect.Y + windowArea.Y, rect.Width, rect.Height);
                if (!rectAbs.Intersects(windowArea))
                {
                    result = default;
                    return false;
                }
            }

            result = rect;
            return true;
        }

        // Single distance-sort algorithm: distances are precomputed once into a span (stackalloc for typical counts) and the list is sorted against the cached values with parallel swaps. Insertion sort up to 50 items, quicksort above.
        internal static void SortByDistance<T>(List<T> items, Func<T, float> getDistance)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(getDistance);

            int count = items.Count;
            if (count <= 1)
                return;

            Span<float> distances = count <= 256 ? stackalloc float[count] : new float[count];
            for (int i = 0; i < count; i++)
                distances[i] = getDistance(items[i]);

            if (count <= 50)
            {
                InsertionSortByDistance(items, distances, count);
                return;
            }

            QuickSortByDistance(items, distances, 0, count - 1);
        }

        internal static void InsertionSortByDistance<T>(List<T> items, Span<float> distances, int count)
        {
            for (int i = 1; i < count; i++)
            {
                T key = items[i];
                float keyDistance = distances[i];
                int j = i - 1;

                while (j >= 0 && distances[j] > keyDistance)
                {
                    items[j + 1] = items[j];
                    distances[j + 1] = distances[j];
                    j--;
                }

                items[j + 1] = key;
                distances[j + 1] = keyDistance;
            }
        }

        internal static void QuickSortByDistance<T>(List<T> items, Span<float> distances, int low, int high)
        {
            if (low < high)
            {
                int pivotIndex = PartitionByDistance(items, distances, low, high);
                QuickSortByDistance(items, distances, low, pivotIndex - 1);
                QuickSortByDistance(items, distances, pivotIndex + 1, high);
            }
        }

        internal static int PartitionByDistance<T>(List<T> items, Span<float> distances, int low, int high)
        {
            float pivotDistance = distances[high];
            int i = low - 1;

            for (int j = low; j < high; j++)
                if (distances[j] <= pivotDistance)
                {
                    i++;
                    (items[i], items[j]) = (items[j], items[i]);
                    (distances[i], distances[j]) = (distances[j], distances[i]);
                }


            (items[i + 1], items[high]) = (items[high], items[i + 1]);
            (distances[i + 1], distances[high]) = (distances[high], distances[i + 1]);
            return i + 1;
        }
    }
}
