namespace ClickIt.Shared.Game
{
    internal static class LabelGeometry
    {
        internal static bool TryGetLabelRect(LabelOnGround? label, out RectangleF rect)
        {
            rect = default;

            Element? element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            object? maybeRect = element.GetClientRect();
            if (maybeRect is not RectangleF resolvedRect)
                return false;

            if (resolvedRect.Width <= 0 || resolvedRect.Height <= 0)
                return false;

            rect = resolvedRect;
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

            if (count <= 50)
            {
                InsertionSortByDistance(labels, count);
                return;
            }

            QuickSortByDistance(labels, 0, count - 1);
        }

        internal static void InsertionSortByDistance(List<LabelOnGround> labels, int count)
        {
            for (int i = 1; i < count; i++)
            {
                LabelOnGround key = labels[i];
                int j = i - 1;

                while (j >= 0 && labels[j].ItemOnGround.DistancePlayer > key.ItemOnGround.DistancePlayer)
                {
                    labels[j + 1] = labels[j];
                    j--;
                }

                labels[j + 1] = key;
            }
        }

        internal static void QuickSortByDistance(List<LabelOnGround> labels, int low, int high)
        {
            if (low < high)
            {
                int pivotIndex = PartitionByDistance(labels, low, high);
                QuickSortByDistance(labels, low, pivotIndex - 1);
                QuickSortByDistance(labels, pivotIndex + 1, high);
            }
        }

        internal static int PartitionByDistance(List<LabelOnGround> labels, int low, int high)
        {
            Entity pivot = labels[high].ItemOnGround;
            int i = low - 1;

            for (int j = low; j < high; j++)
                if (labels[j].ItemOnGround.DistancePlayer <= pivot.DistancePlayer)
                {
                    i++;
                    SwapLabels(labels, i, j);
                }


            SwapLabels(labels, i + 1, high);
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
    }
}