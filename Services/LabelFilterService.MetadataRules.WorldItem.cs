using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private static string GetWorldItemMetadataPath(Entity item)
        {
            try
            {
                string resolvedMetadata = EntityHelpers.ResolveWorldItemMetadataPath(item);
                if (TryGetWorldItemComponentMetadata(item, out string componentMetadata))
                    return SelectBestWorldItemMetadataPath(resolvedMetadata, componentMetadata);

                return resolvedMetadata;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryGetWorldItemComponentMetadata(Entity? item, out string metadata)
        {
            metadata = string.Empty;
            if (item == null)
                return false;

            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                string candidate = itemEntity?.Metadata ?? string.Empty;
                if (string.IsNullOrWhiteSpace(candidate))
                    return false;

                metadata = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string SelectBestWorldItemMetadataPath(string resolvedMetadata, string componentMetadata)
        {
            if (string.IsNullOrWhiteSpace(componentMetadata))
                return resolvedMetadata ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedMetadata))
                return componentMetadata;

            if (resolvedMetadata.IndexOf("Metadata/MiscellaneousObjects/", StringComparison.OrdinalIgnoreCase) >= 0)
                return componentMetadata;

            return resolvedMetadata;
        }

        private static string GetWorldItemBaseName(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                return itemEntity?.GetComponent<Base>()?.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsStackableCurrencyCore(Entity? itemEntity, GameController? gameController)
        {
            try
            {
                if (gameController == null || itemEntity == null)
                    return false;

                var baseItemType = gameController.Files.BaseItemTypes.Translate(itemEntity.Path ?? string.Empty);
                if (baseItemType == null)
                    return false;

                return string.Equals(baseItemType.ClassName, "StackableCurrency", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}