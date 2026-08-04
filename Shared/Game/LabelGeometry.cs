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

            object? maybeRect = element.GetClientRect();
            if (maybeRect is not RectangleF resolvedRect)
                return false;

            if (resolvedRect.Width <= 0 || resolvedRect.Height <= 0)
                return false;

            rect = resolvedRect;
            return true;
        }

        internal static bool TryGetLabelRectOnScreen(LabelOnGround? label, RectangleF windowArea, out RectangleF rect)
        {
            if (!TryGetLabelRect(label, out RectangleF labelRect))
            {
                rect = default;
                return false;
            }

            if (windowArea.Width > 0 && windowArea.Height > 0)
            {
                RectangleF rectAbs = new(labelRect.X + windowArea.X, labelRect.Y + windowArea.Y, labelRect.Width, labelRect.Height);
                if (!rectAbs.Intersects(windowArea))
                {
                    rect = default;
                    return false;
                }
            }

            rect = labelRect;
            return true;
        }

        internal static void SortByDistance<T>(List<T> items, Func<T, float> getDistance)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(getDistance);

            int count = items.Count;
            if (count <= 1)
                return;

            if (count <= 50)
            {
                for (int i = 1; i < count; i++)
                {
                    T key = items[i];
                    float keyDistance = getDistance(key);
                    int j = i - 1;
                    while (j >= 0 && getDistance(items[j]) > keyDistance)
                    {
                        items[j + 1] = items[j];
                        j--;
                    }

                    items[j + 1] = key;
                }

                return;
            }

            QuickSortGeneric(items, 0, count - 1, getDistance);
        }

        public static void SortLabelsByDistance(List<LabelOnGround> labels)
        {
            int count = labels.Count;
            if (count <= 1)
                return;

            // DistancePlayer is a dynamic game-memory read, so a per-comparison sort would multiply
            // the cost. Precompute the distances once (stack span for typical counts) and sort the
            // label list against the cached values instead.
            Span<float> distances = count <= 256 ? stackalloc float[count] : new float[count];
            for (int i = 0; i < count; i++)
                distances[i] = GetLabelDistance(labels[i]);

            if (count <= 50)
            {
                InsertionSortByDistance(labels, distances, count);
                return;
            }

            QuickSortByDistance(labels, distances, 0, count - 1);
        }

        internal static void InsertionSortByDistance(List<LabelOnGround> labels, Span<float> distances, int count)
        {
            for (int i = 1; i < count; i++)
            {
                LabelOnGround key = labels[i];
                float keyDistance = distances[i];
                int j = i - 1;

                while (j >= 0 && distances[j] > keyDistance)
                {
                    labels[j + 1] = labels[j];
                    distances[j + 1] = distances[j];
                    j--;
                }

                labels[j + 1] = key;
                distances[j + 1] = keyDistance;
            }
        }

        internal static void QuickSortByDistance(List<LabelOnGround> labels, Span<float> distances, int low, int high)
        {
            if (low < high)
            {
                int pivotIndex = PartitionByDistance(labels, distances, low, high);
                QuickSortByDistance(labels, distances, low, pivotIndex - 1);
                QuickSortByDistance(labels, distances, pivotIndex + 1, high);
            }
        }

        internal static int PartitionByDistance(List<LabelOnGround> labels, Span<float> distances, int low, int high)
        {
            float pivotDistance = distances[high];
            int i = low - 1;

            for (int j = low; j < high; j++)
                if (distances[j] <= pivotDistance)
                {
                    i++;
                    SwapLabels(labels, i, j);
                    (distances[i], distances[j]) = (distances[j], distances[i]);
                }


            SwapLabels(labels, i + 1, high);
            (distances[i + 1], distances[high]) = (distances[high], distances[i + 1]);
            return i + 1;
        }

        internal static void SwapLabels(List<LabelOnGround> labels, int i, int j)
        {
            if (i == j)
                return;

            (labels[i], labels[j]) = (labels[j], labels[i]);
        }

        private static void QuickSortGeneric<T>(List<T> items, int low, int high, Func<T, float> getDistance)
        {
            if (low < high)
            {
                int pivot = PartitionGeneric(items, low, high, getDistance);
                QuickSortGeneric(items, low, pivot - 1, getDistance);
                QuickSortGeneric(items, pivot + 1, high, getDistance);
            }
        }

        private static int PartitionGeneric<T>(List<T> items, int low, int high, Func<T, float> getDistance)
        {
            float pivotValue = getDistance(items[high]);
            int i = low - 1;
            for (int j = low; j < high; j++)
                if (getDistance(items[j]) <= pivotValue)
                {
                    i++;
                    (items[i], items[j]) = (items[j], items[i]);
                }


            (items[i + 1], items[high]) = (items[high], items[i + 1]);
            return i + 1;
        }

        private static float GetLabelDistance(LabelOnGround? label)
        {
            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                || !DynamicAccess.TryReadFloat(rawItem, DynamicAccessProfiles.DistancePlayer, out float distance))
                return float.MaxValue;

            return distance;
        }
    }
}