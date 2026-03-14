using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Utils
{
    public static partial class EntityHelpers
    {
        /// <summary>
        /// Returns true if a RitualBlocker is present in the current entity list.
        /// Centralised helper so multiple classes can share the same implementation.
        /// </summary>
        public static bool IsRitualActive(GameController? gameController)
        {
            if (gameController?.EntityListWrapper?.OnlyValidEntities == null)
                return false;
            var paths = new List<string?>();
            foreach (var entity in gameController.EntityListWrapper.OnlyValidEntities)
            {
                try
                {
                    paths.Add(entity?.Path);
                }
                catch
                {
                }
            }

            return IsRitualActive(paths);
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
