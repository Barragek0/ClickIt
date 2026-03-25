namespace ClickIt.Utils
{
    internal static partial class LabelUtils
    {
        internal static void SortByDistanceForTests<T>(List<T> items, Func<T, float> getDistance)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (getDistance == null) throw new ArgumentNullException(nameof(getDistance));

            int n = items.Count;
            if (n <= 1) return;

            if (n <= 50)
            {
                for (int i = 1; i < n; i++)
                {
                    T key = items[i];
                    float keyDist = getDistance(key);
                    int j = i - 1;
                    while (j >= 0 && getDistance(items[j]) > keyDist)
                    {
                        items[j + 1] = items[j];
                        j--;
                    }
                    items[j + 1] = key;
                }
            }
            else
            {
                QuickSortGeneric(items, 0, n - 1, getDistance);
            }
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
            {
                if (getDistance(items[j]) <= pivotValue)
                {
                    i++;
                    T tmp = items[i];
                    items[i] = items[j];
                    items[j] = tmp;
                }
            }
            T tmp2 = items[i + 1];
            items[i + 1] = items[high];
            items[high] = tmp2;
            return i + 1;
        }

        // without constructing complex runtime ExileCore objects.
        internal static bool IsValidEntityTypeForTests(ExileCore.Shared.Enums.EntityType type, string? path, bool chestOpenOnDamage)
        {
            string p = path ?? string.Empty;
            if (type == ExileCore.Shared.Enums.EntityType.WorldItem)
                return true;
            if (type == ExileCore.Shared.Enums.EntityType.AreaTransition)
                return true;
            if (p.Contains("AreaTransition"))
                return true;
            if (type == ExileCore.Shared.Enums.EntityType.Chest && !chestOpenOnDamage)
                return true;
            return false;
        }

        internal static bool IsValidClickableLabelForTests(bool labelNotNull, bool itemNotNull, bool isVisible, bool labelElementValid, bool inClickableArea, ExileCore.Shared.Enums.EntityType type, string? path, bool chestOpenOnDamage, bool hasEssenceImprisonment, bool harvestRootElementVisible)
        {
            if (!labelNotNull || !itemNotNull || !isVisible || !labelElementValid)
                return false;

            var p = path ?? string.Empty;
            if ((p.Contains("Harvest/Irrigator") || p.Contains("Harvest/Extractor")) && !harvestRootElementVisible)
                return false;

            if (!inClickableArea)
                return false;

            if (IsValidEntityTypeForTests(type, path, chestOpenOnDamage)) return true;
            if (!string.IsNullOrEmpty(path) && IsPathForClickableObject(path)) return true;
            if (hasEssenceImprisonment) return true;
            return false;
        }

        internal static int GetThreadLocalElementsCountForTests()
        {
            return _threadLocalElementsList.Value?.Count ?? 0;
        }

        internal static void AddNullElementToThreadLocalForTests()
        {
            _threadLocalElementsList.Value?.Add(null!);
        }
    }
}
