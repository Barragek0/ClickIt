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
                !label.IsVisible || !IsLabelElementValid(label))
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
                   path.Contains("MiscellaneousObjects/Lights") ||
                   path.Contains("MiscellaneousObjects/Door") ||
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

        public static bool HasEssenceImprisonmentText(LabelOnGround label)
        {
            return GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

    }
}
