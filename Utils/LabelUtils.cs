using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.Shared.Enums;
using ExileCore.PoEMemory.Components;

namespace ClickIt.Utils
{
    internal static partial class LabelUtils
    {
        internal static bool TryGetLabelRect(LabelOnGround? label, out RectangleF rect)
        {
            rect = default;

            Element? element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            object? maybeRect = element.GetClientRect();
            if (maybeRect is not RectangleF r)
                return false;

            if (r.Width <= 0 || r.Height <= 0)
                return false;

            rect = r;
            return true;
        }

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

        public static void SortLabelsByDistance(List<LabelOnGround> labels)
        {
            int n = labels.Count;
            if (n <= 1) return;

            if (n <= 50)
            {
                InsertionSortByDistance(labels, n);
            }
            else
            {
                QuickSortByDistance(labels, 0, n - 1);
            }
        }

        public static void InsertionSortByDistance(List<LabelOnGround> labels, int n)
        {
            for (int i = 1; i < n; i++)
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

        public static void QuickSortByDistance(List<LabelOnGround> labels, int low, int high)
        {
            if (low < high)
            {
                int pivotIndex = PartitionByDistance(labels, low, high);
                QuickSortByDistance(labels, low, pivotIndex - 1);
                QuickSortByDistance(labels, pivotIndex + 1, high);
            }
        }

        public static int PartitionByDistance(List<LabelOnGround> labels, int low, int high)
        {
            Entity pivot = labels[high].ItemOnGround;
            int i = low - 1;

            for (int j = low; j < high; j++)
            {
                if (labels[j].ItemOnGround.DistancePlayer <= pivot.DistancePlayer)
                {
                    i++;
                    SwapLabels(labels, i, j);
                }
            }
            SwapLabels(labels, i + 1, high);
            return i + 1;
        }

        public static void SwapLabels(List<LabelOnGround> labels, int i, int j)
        {
            if (i != j)
            {
                LabelOnGround temp = labels[i];
                labels[i] = labels[j];
                labels[j] = temp;
            }
        }

        internal static bool IsValidEntityTypeForTests(EntityType type, string? path, bool chestOpenOnDamage)
        {
            string p = path ?? string.Empty;
            if (type == EntityType.WorldItem)
                return true;
            if (type == EntityType.AreaTransition)
                return true;
            if (p.Contains("AreaTransition"))
                return true;
            if (type == EntityType.Chest && !chestOpenOnDamage)
                return true;
            return false;
        }

        internal static bool IsValidClickableLabelForTests(bool labelNotNull, bool itemNotNull, bool isVisible, bool labelElementValid, bool inClickableArea, EntityType type, string? path, bool chestOpenOnDamage, bool hasEssenceImprisonment, bool harvestRootElementVisible)
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

        public static bool IsValidClickableLabel(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
        {
            if (label == null || label.ItemOnGround == null ||
                !label.IsVisible || !IsLabelElementValid(label))
            {
                return false;
            }

            string path = label.ItemOnGround.Path ?? string.Empty;
            if (IsHarvestPath(path) && !IsHarvestRootElementVisible(label))
            {
                return false;
            }

            if (!IsLabelInClickableArea(label, pointIsInClickableArea))
            {
                return false;
            }

            return IsValidEntityType(label.ItemOnGround)
                || IsValidEntityPath(label.ItemOnGround)
                || HasEssenceImprisonmentText(label);
        }

        public static bool IsLabelElementValid(LabelOnGround label)
        {
            RectangleF? labelRect = label.Label?.GetClientRect();
            return labelRect is RectangleF &&
                   label.Label?.IsValid == true &&
                   label.Label?.IsVisible == true;
        }

        public static bool IsLabelInClickableArea(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
        {
            RectangleF? labelRect = label.Label?.GetClientRect();
            return labelRect is RectangleF rect && HasClickablePoint(rect, pointIsInClickableArea);
        }

        private static bool HasClickablePoint(RectangleF rect, Func<Vector2, bool> pointIsInClickableArea)
        {
            if (pointIsInClickableArea(rect.Center))
                return true;

            const int cols = 7;
            const int rows = 5;
            float stepX = rect.Width / cols;
            float stepY = rect.Height / rows;

            for (int y = 0; y < rows; y++)
            {
                float sampleY = rect.Top + ((y + 0.5f) * stepY);
                for (int x = 0; x < cols; x++)
                {
                    float sampleX = rect.Left + ((x + 0.5f) * stepX);
                    if (pointIsInClickableArea(new Vector2(sampleX, sampleY)))
                        return true;
                }
            }

            return false;
        }

        public static bool IsValidEntityType(Entity item)
        {
            EntityType type = item.Type;
            string path = item.Path ?? "";
            return type == EntityType.WorldItem ||
                   type == EntityType.AreaTransition || path.Contains("AreaTransition") ||
                   (type == EntityType.Chest && !item.GetComponent<Chest>().OpenOnDamage);
        }

        public static bool IsValidEntityPath(Entity item)
        {
            string path = item.Path ?? "";
            if (string.IsNullOrEmpty(path)) return false;
            return IsPathForClickableObject(path);
        }

        public static bool IsPathForClickableObject(string path)
        {
            return path.Contains("DelveMineral") ||
                   path.Contains("Delve/Objects/Encounter") ||
                   path.Contains("AzuriteEncounterController") ||
                   IsHarvestPath(path) ||
                   path.Contains("CleansingFireAltar") ||
                   path.Contains("TangleAltar") ||
                   path.Contains("CraftingUnlocks") ||
                   path.Contains("Brequel") ||
                   path.Contains("CrimsonIron") ||
                   path.Contains("copper_altar") ||
                   path.Contains("PetrifiedWood") ||
                   path.Contains("Bismuth") ||
                   path.Contains("MiscellaneousObjects/Lights") ||
                   path.Contains("MiscellaneousObjects/Door") ||
                   path.Contains("Heist/Objects/Level/Door_Basic") ||
                   path.Contains("ClosedDoorPast") ||
                   path.Contains("LegionInitiator") ||
                   path.Contains("DarkShrine") ||
                   path.Contains("Sanctum") ||
                   path.Contains("BetrayalMakeChoice") ||
                   path.Contains("BlightPump") ||
                   path.Contains("Leagues/Ultimatum/Objects/UltimatumChallengeInteractable", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("Leagues/Ritual");
        }

        private static bool IsHarvestPath(string path)
        {
            return path.Contains("Harvest/Irrigator") ||
                   path.Contains("Harvest/Extractor");
        }

        private static bool IsHarvestRootElementVisible(LabelOnGround label)
        {
            return label.Label?.GetChildAtIndex(0)?.IsVisible == true;
        }

        public static bool HasEssenceImprisonmentText(LabelOnGround label)
        {
            return GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
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

        internal static bool ElementContainsAnyStringsForTests(Services.IElementAdapter? root, IEnumerable<string> patterns)
        {
            if (root == null)
                return false;

            string[] patList = patterns as string[] ?? patterns.ToArray();
            if (patList.Length == 0)
                return false;

            Stack<Services.IElementAdapter> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Services.IElementAdapter element = stack.Pop();
                string text = element.GetText(512);
                if (!string.IsNullOrEmpty(text))
                {
                    for (int i = 0; i < patList.Length; i++)
                    {
                        if (text.Contains(patList[i]))
                            return true;
                    }
                }

                foreach (Services.IElementAdapter child in EnumerateAdapterChildren(element))
                {
                    stack.Push(child);
                }
            }

            return false;
        }

        internal static List<Services.IElementAdapter> GetElementsByStringContainsForTests(Services.IElementAdapter? label, string str)
        {
            List<Services.IElementAdapter> list = [];
            if (label == null)
                return list;

            string rootText = label.GetText(512);
            if (!string.IsNullOrEmpty(rootText) && rootText.Contains(str))
                list.Add(label);

            foreach (Services.IElementAdapter child in EnumerateAdapterChildren(label))
            {
                string childText = child.GetText(512);
                if (!string.IsNullOrEmpty(childText) && childText.Contains(str))
                    list.Add(child);
            }

            return list;
        }

        internal static Services.IElementAdapter? GetElementByStringForTests(Services.IElementAdapter? root, string str)
        {
            if (root == null)
                return null;

            Stack<Services.IElementAdapter> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Services.IElementAdapter element = stack.Pop();
                string text = element.GetText(512);
                if (text != null && text.Equals(str))
                    return element;

                foreach (Services.IElementAdapter child in EnumerateAdapterChildren(element))
                {
                    stack.Push(child);
                }
            }

            return null;
        }

        private static IEnumerable<Services.IElementAdapter> EnumerateAdapterChildren(Services.IElementAdapter parent)
        {
            HashSet<Services.IElementAdapter> seen = [];
            for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
            {
                int childIndex = 0;
                while (true)
                {
                    Services.IElementAdapter? child = parent.GetChildFromIndices(containerIndex, childIndex);
                    if (child == null)
                        break;

                    if (seen.Add(child))
                        yield return child;

                    childIndex++;
                }
            }
        }

    }
}
