namespace ClickIt.Shared.Game
{
    public static class EntityHelpers
    {
        internal static bool IsRitualActive(IEnumerable<string?>? paths)
        {
            if (paths == null)
                return false;


            foreach (string? p in paths)
                if (p?.Contains("RitualBlocker", StringComparison.Ordinal) == true)
                    return true;



            return false;
        }

        /// <summary>
        /// Returns true if a RitualBlocker is present in the current entity list.
        /// Centralised helper so multiple classes can share the same implementation.
        /// </summary>
        public static bool IsRitualActive(GameController? gameController)
        {
            if (gameController?.EntityListWrapper?.OnlyValidEntities == null)
                return false;


            foreach (Entity entity in gameController.EntityListWrapper.OnlyValidEntities)
            {
                string path = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string resolvedPath)
                    ? resolvedPath
                    : string.Empty;

                if (path.Contains("RitualBlocker", StringComparison.Ordinal))
                    return true;

            }

            return false;
        }

        public static string ResolveWorldItemMetadataPath(
            Entity? item,
            string missingItemFallback = "",
            string missingItemEntityFallback = "",
            string missingMetadataFallback = "")
        {
            if (item == null)
                return missingItemFallback;


            Entity? itemEntity = TryGetWorldItemEntity(item);
            if (itemEntity == null)
                return missingItemEntityFallback;


            if (DynamicAccess.TryReadString(itemEntity, DynamicAccessProfiles.Metadata, out string metadata))
                return metadata;


            if (TryResolveMapKeyMetadata(item, out string mapKeyMetadata) && !string.IsNullOrWhiteSpace(mapKeyMetadata))
                return mapKeyMetadata;


            return DynamicAccess.TryReadString(itemEntity, DynamicAccessProfiles.Path, out string path)
                ? path
                : missingMetadataFallback;
        }

        public static bool TryResolveMapKeyMetadata(Entity? item, out string metadata)
        {
            metadata = string.Empty;

            if (item == null)
                return false;


            Entity? itemEntity = TryGetWorldItemEntity(item);
            if (itemEntity == null)
                return false;


            if (DynamicAccess.TryReadString(itemEntity, DynamicAccessProfiles.Metadata, out metadata))
                return true;


            if (DynamicAccess.TryReadString(itemEntity, DynamicAccessProfiles.Path, out metadata))
                return true;


            return false;
        }

        private static Entity? TryGetWorldItemEntity(Entity item)
        {
            if (!DynamicAccess.TryGetComponent<WorldItem>(item, out WorldItem? world)
                || world == null)
                return null;


            return DynamicAccess.TryGetDynamicValue(world, DynamicAccessProfiles.ItemEntity, out object? rawItemEntity)
                ? rawItemEntity as Entity
                : null;
        }

        public static bool TryResolveMapKeyDisplayName(Entity? _, out string displayName)
        {
            displayName = string.Empty;
            return false;
        }

    }
}
