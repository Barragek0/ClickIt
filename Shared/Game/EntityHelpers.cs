using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Shared.Game
{
    public static class EntityHelpers
    {
        internal static bool IsRitualActive(IEnumerable<string?>? paths)
        {
            if (paths == null)
                return false;

            foreach (var p in paths)
            {
                if (p?.Contains("RitualBlocker") == true)
                    return true;
            }

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

            foreach (var entity in gameController.EntityListWrapper.OnlyValidEntities)
            {
                string? path = null;
                try
                {
                    path = entity?.Path;
                }
                catch
                {
                }

                if (path?.Contains("RitualBlocker") == true)
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

            WorldItem? world = item.GetComponent<WorldItem>();
            Entity? itemEntity = world?.ItemEntity;
            if (itemEntity == null)
                return missingItemEntityFallback;

            try
            {
                if (!string.IsNullOrWhiteSpace(itemEntity.Metadata))
                    return itemEntity.Metadata;
            }
            catch
            {
                // Some memory-backed entities can throw transiently; fall through to safer fallbacks.
            }

            if (TryResolveMapKeyMetadata(item, out string mapKeyMetadata) && !string.IsNullOrWhiteSpace(mapKeyMetadata))
                return mapKeyMetadata;

            try
            {
                return itemEntity.Path ?? missingMetadataFallback;
            }
            catch
            {
                return missingMetadataFallback;
            }
        }

        public static bool TryResolveMapKeyMetadata(Entity? item, out string metadata)
        {
            metadata = string.Empty;

            if (item == null)
                return false;

            WorldItem? world = item.GetComponent<WorldItem>();
            Entity? itemEntity = world?.ItemEntity;
            if (itemEntity == null)
                return false;

            try
            {
                if (!string.IsNullOrWhiteSpace(itemEntity.Metadata))
                {
                    metadata = itemEntity.Metadata;
                    return true;
                }
            }
            catch
            {
                // Fall through to path fallback below.
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(itemEntity.Path))
                {
                    metadata = itemEntity.Path;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static bool TryResolveMapKeyDisplayName(Entity? item, out string displayName)
        {
            displayName = string.Empty;
            return false;
        }

    }
}
