using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Label.Classification
{
    internal interface IWorldItemMetadataPolicy
    {
        string GetWorldItemMetadataPath(Entity item);
        string GetWorldItemBaseName(Entity item);
        bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label, Func<Entity, GameController?, bool> shouldAllowWhenInventoryFull);
    }

    internal sealed class WorldItemMetadataPolicy : IWorldItemMetadataPolicy
    {
        public string GetWorldItemMetadataPath(Entity item)
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

        public string GetWorldItemBaseName(Entity item)
        {
            return ResolveWorldItemBaseName(item);
        }

        public bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label, Func<Entity, GameController?, bool> shouldAllowWhenInventoryFull)
        {
            string metadata = GetWorldItemMetadataPath(item);
            string itemName = ResolveWorldItemBaseName(item);
            string labelText = GetWorldItemLabelText(label);

            IReadOnlyList<string> whitelist = settings.ItemTypeWhitelistMetadata ?? [];
            IReadOnlyList<string> blacklist = settings.ItemTypeBlacklistMetadata ?? [];

            bool whitelistPass = whitelist.Count == 0 || MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(metadata, itemName, item, labelText, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(metadata, itemName, item, labelText, blacklist);
            if (blacklistMatch)
                return false;

            return shouldAllowWhenInventoryFull(item, gameController);
        }

        internal static string SelectBestWorldItemMetadataPath(string resolvedMetadata, string componentMetadata)
        {
            if (string.IsNullOrWhiteSpace(componentMetadata))
                return resolvedMetadata ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedMetadata))
                return componentMetadata;

            if (resolvedMetadata.IndexOf("Metadata/MiscellaneousObjects/", StringComparison.OrdinalIgnoreCase) >= 0)
                return componentMetadata;

            return resolvedMetadata;
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

        private static string ResolveWorldItemBaseName(Entity item)
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

        private static string GetWorldItemLabelText(LabelOnGround? label)
        {
            try
            {
                return label?.Label?.GetText(512) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
