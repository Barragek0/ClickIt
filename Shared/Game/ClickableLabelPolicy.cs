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
            if (label == null
                || !TryGetLabelItem(label, out object? item)
                || !DynamicAccess.TryReadBool(label, DynamicAccessProfiles.IsVisible, out bool isVisible)
                || !isVisible
                || !IsLabelElementValid(label))
                return false;

            string path = DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            if (IsHarvestPath(path) && !IsHarvestRootElementVisible(label))
                return false;

            if (!IsLabelInClickableArea(label, pointIsInClickableArea))
                return false;

            return IsValidEntityType(item)
                || IsValidEntityPath(item)
                || HasEssenceImprisonmentText(label);
        }

        public static bool IsValidEntityPath(Entity item)
            => IsValidEntityPath((object?)item);

        private static bool IsValidEntityPath(object? item)
            => DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
                && IsValidEntityPathCore(path);

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
            => TryGetLabelAdapter(label, out IElementAdapter? adapter)
                && LabelElementSearch.GetElementByStringCore(adapter, "The monster is imprisoned by powerful Essences.") != null;

        public static bool IsLabelElementValid(LabelOnGround label)
        {
            return LabelGeometry.TryGetLabelRect(label, out _)
                   && TryGetLabelRoot(label, out object? root)
                   && DynamicAccess.TryReadBool(root, DynamicAccessProfiles.IsVisible, out bool isVisible)
                   && isVisible;
        }

        public static bool IsLabelInClickableArea(LabelOnGround label, Func<Vector2, bool> pointIsInClickableArea)
        {
            return LabelGeometry.TryGetLabelRect(label, out RectangleF rect)
                && HasClickablePoint(rect, pointIsInClickableArea);
        }

        public static bool IsValidEntityType(Entity item)
        {
            return IsValidEntityType((object?)item);
        }

        private static bool IsValidEntityType(object? item)
        {
            EntityType type = ResolveEntityType(item);
            string path = DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            return type == EntityType.WorldItem
                   || type == EntityType.AreaTransition
                   || path.Contains("AreaTransition")
                   || (type == EntityType.Chest && !TryGetChestOpenOnDamage(item));
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
            => TryGetLabelRoot(label, out object? root)
                && DynamicAccess.TryGetChildAtIndex(root, 0, out object? rawChild)
                && rawChild != null
                && DynamicAccess.TryReadBool(rawChild, DynamicAccessProfiles.IsVisible, out bool isVisible)
                && isVisible;

        private static bool TryGetLabelItem(LabelOnGround? label, out object? item)
        {
            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out item)
                && item != null;
        }

        private static bool TryGetLabelRoot(LabelOnGround? label, out object? root)
        {
            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out root)
                && root != null;
        }

        private static bool TryGetLabelAdapter(LabelOnGround? label, out IElementAdapter? adapter)
        {
            adapter = null;
            if (!TryGetLabelRoot(label, out object? root) || root == null)
                return false;

            adapter = root switch
            {
                IElementAdapter existingAdapter => existingAdapter,
                Element element => new ElementAdapter(element),
                _ => null,
            };

            return adapter != null;
        }

        private static EntityType ResolveEntityType(object? item)
        {
            if (!DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Type, out object? rawType)
                || rawType == null)
                return EntityType.WorldItem;

            return rawType switch
            {
                EntityType entityType => entityType,
                int entityTypeValue => (EntityType)entityTypeValue,
                _ => EntityType.WorldItem,
            };
        }

        private static bool TryGetChestOpenOnDamage(object? item)
        {
            return DynamicAccess.TryGetComponent<Chest>(item, out Chest? rawChest)
                && rawChest != null
                && DynamicAccess.TryReadBool(rawChest, DynamicAccessProfiles.OpenOnDamage, out bool openOnDamage)
                && openOnDamage;
        }
    }
}