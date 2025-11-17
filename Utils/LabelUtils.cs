using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.Shared.Enums;
using ExileCore.PoEMemory.Components;

namespace ClickIt.Utils
{
#nullable enable
    internal static class LabelUtils
    {
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

        // The label validity helpers accept a delegate for clickable-area checks
        public static bool IsValidClickableLabel(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
        {
            if (label == null || label.ItemOnGround == null ||
                !label.IsVisible || !IsLabelElementValid(label, pointIsInClickableArea))
            {
                return false;
            }

            if (!IsLabelInClickableArea(label, pointIsInClickableArea))
            {
                return false;
            }

            return IsValidEntityType(label.ItemOnGround) || IsValidEntityPath(label.ItemOnGround) || HasEssenceImprisonmentText(label);
        }

        public static bool IsLabelElementValid(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
        {
            RectangleF? labelRect = label.Label?.GetClientRect();
            return labelRect is RectangleF rect &&
                   label.Label?.IsValid == true &&
                   label.Label?.IsVisible == true &&
                   pointIsInClickableArea(rect.Center);
        }

        public static bool IsLabelInClickableArea(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
        {
            RectangleF? labelRect = label.Label?.GetClientRect();
            return labelRect is RectangleF rect && pointIsInClickableArea(rect.Center);
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
                   path.Contains("AzuriteEncounterController") ||
                   path.Contains("Harvest/Irrigator") ||
                   path.Contains("Harvest/Extractor") ||
                   path.Contains("CleansingFireAltar") ||
                   path.Contains("TangleAltar") ||
                   path.Contains("CraftingUnlocks") ||
                   path.Contains("Brequel") ||
                   path.Contains("CrimsonIron") ||
                   path.Contains("copper_altar") ||
                   path.Contains("PetrifiedWood") ||
                   path.Contains("Bismuth") ||
                   path.Contains("Verisium") ||
                   path.Contains("ClosedDoorPast") ||
                   path.Contains("LegionInitiator") ||
                   path.Contains("DarkShrine") ||
                   path.Contains("Sanctum");
        }

        public static bool HasEssenceImprisonmentText(LabelOnGround label)
        {
            return GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

        [ThreadStatic]
        private static List<Element>? _threadLocalElementsList;

        private static List<Element> GetThreadLocalElementsList()
        {
            if (_threadLocalElementsList == null)
                _threadLocalElementsList = new List<Element>();
            return _threadLocalElementsList;
        }

        public static List<Element> GetElementsByStringContains(Element? label, string str)
        {
            var elementsList = GetThreadLocalElementsList();
            elementsList.Clear();
            if (label == null)
                return elementsList;
            string rootText = label.GetText(512);
            if (!string.IsNullOrEmpty(rootText) && rootText.Contains(str))
                elementsList.Add(label);
            for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
            {
                Element? container = label.GetChildAtIndex(containerIndex);
                if (container == null)
                    continue;
                IList<Element> children = container.Children;
                if (children == null)
                    continue;
                for (int i = 0; i < children.Count; i++)
                {
                    Element? child = children[i];
                    if (child == null)
                        continue;
                    string childText = child.GetText(512);
                    if (!string.IsNullOrEmpty(childText) && childText.Contains(str))
                        elementsList.Add(child);
                }
            }
            return elementsList;
        }

        public static Element? GetElementByString(Element? root, string str)
        {
            if (root == null)
                return null;
            Element? found = null;
            Stack<Element> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Element el = stack.Pop();
                string text = el.GetText(512);
                if (text != null && text.Equals(str))
                {
                    found = el;
                    break;
                }
                IList<Element> children = el.Children;
                if (children != null)
                {
                    foreach (Element c in children)
                    {
                        if (c != null) stack.Push(c);
                    }
                }
            }
            return found;
        }

        public static bool ElementContainsAnyStrings(Element? root, IEnumerable<string> patterns)
        {
            if (root == null)
                return false;
            string[] patList = patterns as string[] ?? patterns.ToArray();
            if (patList.Length == 0)
                return false;
            Stack<Element> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Element el = stack.Pop();
                string text = el.GetText(512);
                if (!string.IsNullOrEmpty(text))
                {
                    for (int i = 0; i < patList.Length; i++)
                    {
                        if (text.Contains(patList[i]))
                            return true;
                    }
                }
                IList<Element> children = el.Children;
                if (children != null)
                {
                    foreach (Element c in children)
                    {
                        if (c != null) stack.Push(c);
                    }
                }
            }
            return false;
        }
    }
}
