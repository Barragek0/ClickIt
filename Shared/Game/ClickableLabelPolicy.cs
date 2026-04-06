namespace ClickIt.Shared.Game
{
    internal static class ClickableLabelPolicy
    {
        internal static bool IsValidEntityTypeCore(EntityType type, string? path, bool chestOpenOnDamage)
        {
            string resolvedPath = path ?? string.Empty;
            if (type == EntityType.WorldItem)
                return true;
            if (type == EntityType.AreaTransition)
                return true;
            if (resolvedPath.Contains("AreaTransition"))
                return true;
            if (type == EntityType.Chest && !chestOpenOnDamage)
                return true;
            return false;
        }

        internal static bool IsValidClickableLabelCore(bool labelNotNull, bool itemNotNull, bool isVisible, bool labelElementValid, bool inClickableArea, EntityType type, string? path, bool chestOpenOnDamage, bool hasEssenceImprisonment, bool harvestRootElementVisible)
        {
            if (!labelNotNull || !itemNotNull || !isVisible || !labelElementValid)
                return false;

            string resolvedPath = path ?? string.Empty;
            if (IsHarvestPath(resolvedPath) && !harvestRootElementVisible)
                return false;

            if (!inClickableArea)
                return false;

            if (IsValidEntityTypeCore(type, path, chestOpenOnDamage))
                return true;
            if (!string.IsNullOrEmpty(path) && IsPathForClickableObject(path))
                return true;
            if (hasEssenceImprisonment)
                return true;
            return false;
        }

        public static bool IsValidClickableLabel(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
        {
            if (label == null || label.ItemOnGround == null || !label.IsVisible || !IsLabelElementValid(label))
                return false;

            string path = label.ItemOnGround.Path ?? string.Empty;
            if (IsHarvestPath(path) && !IsHarvestRootElementVisible(label))
                return false;

            if (!IsLabelInClickableArea(label, pointIsInClickableArea))
                return false;

            return IsValidEntityType(label.ItemOnGround)
                || IsValidEntityPath(label.ItemOnGround)
                || HasEssenceImprisonmentText(label);
        }

        public static bool IsValidEntityPath(Entity item)
            => IsValidEntityPathCore(item.Path);

        internal static bool IsValidEntityPathCore(string? path)
        {
            string value = path ?? string.Empty;
            if (string.IsNullOrEmpty(value))
                return false;

            return IsPathForClickableObject(value);
        }

        public static bool IsPathForClickableObject(string path)
        {
             return path.Contains(Constants.DelveMineral)
                 || path.Contains(Constants.DelveEncounter)
                 || path.Contains(Constants.AzuriteEncounterController)
                   || IsHarvestPath(path)
                 || path.Contains(Constants.CleansingFireAltar)
                 || path.Contains(Constants.TangleAltar)
                 || path.Contains(Constants.CraftingUnlocks)
                 || path.Contains(Constants.Brequel)
                 || path.Contains(Constants.CrimsonIron)
                 || path.Contains(Constants.CopperAltar)
                 || path.Contains(Constants.PetrifiedWood)
                 || path.Contains(Constants.Bismuth)
                 || path.Contains(Constants.MiscellaneousObjectsLights)
                 || path.Contains(Constants.MiscellaneousObjectsDoor)
                   || IsHeistDoorPath(path)
                 || path.Contains(Constants.HeistDoorBasic)
                 || path.Contains(Constants.HeistHazards)
                 || path.Contains(Constants.ClosedDoorPast)
                 || path.Contains(Constants.LegionInitiator)
                 || path.Contains(Constants.DarkShrine)
                 || path.Contains(Constants.Sanctum)
                 || path.Contains(Constants.BetrayalMakeChoice)
                 || path.Contains(Constants.BlightPump)
                 || path.Contains(Constants.UltimatumChallengeInteractablePath)
                 || path.Contains(Constants.SwitchOnce)
                 || path.Contains(Constants.RitualPath);
        }

        public static bool HasEssenceImprisonmentText(LabelOnGround label)
            => LabelElementSearch.GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;

        public static bool IsLabelElementValid(LabelOnGround label)
        {
            RectangleF? labelRect = label.Label?.GetClientRect();
            return labelRect is RectangleF
                   && label.Label?.IsValid == true
                   && label.Label?.IsVisible == true;
        }

        public static bool IsLabelInClickableArea(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
        {
            RectangleF? labelRect = label.Label?.GetClientRect();
            return labelRect is RectangleF rect && HasClickablePoint(rect, pointIsInClickableArea);
        }

        public static bool IsValidEntityType(Entity item)
        {
            EntityType type = item.Type;
            string path = item.Path ?? string.Empty;
            return type == EntityType.WorldItem
                   || type == EntityType.AreaTransition
                   || path.Contains("AreaTransition")
                   || (type == EntityType.Chest && !item.GetComponent<Chest>().OpenOnDamage);
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

        private static bool IsHarvestPath(string path)
            => path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor");

        private static bool IsHeistDoorPath(string path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains("Heist/Objects/Level/Door", StringComparison.OrdinalIgnoreCase)
               && !path.Contains(Constants.HeistDoorBasic, StringComparison.OrdinalIgnoreCase);

        private static bool IsHarvestRootElementVisible(LabelOnGround label)
            => label.Label?.GetChildAtIndex(0)?.IsVisible == true;
    }
}