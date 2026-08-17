namespace ClickIt.Shared.Game
{
    public static class EntityHelpers
    {
        // Ritual state is queried per frame (LazyModeOverlay) and per click tick (offscreen pathing + runtime state); the shared EntityEventHub retains RitualBlocker entities with ONE subscription and ONE path read per event. Streamed-out blockers fail the live IsValid check, so the result still means "a currently-valid RitualBlocker exists".
        public static bool IsRitualActive(GameController? gameController)
        {
            if (gameController == null)
                return false;

            EntityEventHub.Instance.EnsureSubscribed(gameController);
            return EntityEventHub.Instance.RitualBlockers.Any(static entity => IsEntityCurrentlyValid(entity));
        }

        private static bool IsEntityCurrentlyValid(Entity entity)
            => DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsValid, out bool isValid) && isValid;

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
            if (!DynamicAccess.TryGetComponent(item, out WorldItem? world)
                || world == null)
                return null;


            return DynamicAccess.TryGetDynamicValue(world, DynamicAccessProfiles.ItemEntity, out object? rawItemEntity)
                ? rawItemEntity as Entity
                : null;
        }

    }
}
