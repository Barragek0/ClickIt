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

        // Ritual state is queried per frame (LazyModeOverlay) and per click tick (offscreen
        // pathing + runtime state); each call scanned every valid entity's Path via DLR. Cache the
        // result per thread, keyed on the entity-list reference (rebuilt when entities change) with
        // a short time window as a secondary bound.
        [ThreadStatic]
        private static object? s_ritualEntitiesOwner;
        [ThreadStatic]
        private static long s_ritualScanTimestampMs;
        [ThreadStatic]
        private static bool s_ritualActive;
        private const long RitualDetectionCacheMs = 200;

        public static bool IsRitualActive(GameController? gameController)
        {
            List<Entity>? entities = gameController?.EntityListWrapper?.OnlyValidEntities;
            if (entities == null)
                return false;

            long now = Environment.TickCount64;
            if (ReferenceEquals(entities, s_ritualEntitiesOwner) && now - s_ritualScanTimestampMs < RitualDetectionCacheMs)
                return s_ritualActive;

            bool active = false;
            for (int i = 0; i < entities.Count; i++)
            {
                string path = DynamicAccess.TryReadString(entities[i], DynamicAccessProfiles.Path, out string resolvedPath)
                    ? resolvedPath
                    : string.Empty;

                if (path.Contains("RitualBlocker", StringComparison.Ordinal))
                {
                    active = true;
                    break;
                }
            }

            s_ritualEntitiesOwner = entities;
            s_ritualActive = active;
            s_ritualScanTimestampMs = now;
            return active;
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
            if (!DynamicAccess.TryGetComponent(item, out WorldItem? world)
                || world == null)
                return null;


            return DynamicAccess.TryGetDynamicValue(world, DynamicAccessProfiles.ItemEntity, out object? rawItemEntity)
                ? rawItemEntity as Entity
                : null;
        }

    }
}
