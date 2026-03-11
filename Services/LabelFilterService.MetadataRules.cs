using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;

#nullable enable

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private static bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item)
        {
            string metadata = GetWorldItemMetadataPath(item);
            string itemName = GetWorldItemBaseName(item);
            IReadOnlyList<string> whitelist = settings.ItemTypeWhitelistMetadata ?? Array.Empty<string>();
            IReadOnlyList<string> blacklist = settings.ItemTypeBlacklistMetadata ?? Array.Empty<string>();

            bool whitelistPass = whitelist.Count == 0 || ContainsAnyMetadataIdentifier(metadata, itemName, item, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && ContainsAnyMetadataIdentifier(metadata, itemName, item, blacklist);
            return !blacklistMatch;
        }

        private static bool ContainsAnyMetadataIdentifier(string metadataPath, string itemName, IReadOnlyList<string> identifiers)
        {
            return ContainsAnyMetadataIdentifier(metadataPath, itemName, item: null, identifiers);
        }

        private static bool ContainsAnyMetadataIdentifier(string metadataPath, string itemName, Entity? item, IReadOnlyList<string> identifiers)
        {
            if (identifiers == null || identifiers.Count == 0)
                return false;

            metadataPath ??= string.Empty;
            itemName ??= string.Empty;

            for (int i = 0; i < identifiers.Count; i++)
            {
                string identifier = identifiers[i] ?? string.Empty;
                if (identifier.Length == 0)
                    continue;

                if (TryGetSpecialRule(identifier, out string specialRule))
                {
                    if (MatchesSpecialRule(specialRule, metadataPath, itemName, item))
                        return true;

                    continue;
                }

                if (MetadataIdentifierMatcher.ContainsSingle(metadataPath, itemName, identifier))
                    return true;
            }

            return false;
        }

        private static bool TryGetSpecialRule(string identifier, out string specialRule)
        {
            specialRule = string.Empty;
            if (!identifier.StartsWith("special:", StringComparison.OrdinalIgnoreCase))
                return false;

            specialRule = identifier.Substring("special:".Length).Trim();
            return specialRule.Length > 0;
        }

        private static bool MatchesSpecialRule(string specialRule, string metadataPath, string itemName, Entity? item)
        {
            if (specialRule.Equals("unique-items", StringComparison.OrdinalIgnoreCase))
                return item != null && IsUniqueItem(item);

            if (specialRule.Equals("heist-quest-contract", StringComparison.OrdinalIgnoreCase))
                return IsHeistQuestContract(itemName);

            if (specialRule.Equals("heist-non-quest-contract", StringComparison.OrdinalIgnoreCase))
                return IsHeistNonQuestContract(itemName);

            if (specialRule.Equals("inscribed-ultimatum", StringComparison.OrdinalIgnoreCase))
            {
                return (item != null && IsInscribedUltimatum(item))
                    || metadataPath.IndexOf("ItemisedTrial", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (specialRule.Equals("jewels-regular", StringComparison.OrdinalIgnoreCase))
                return IsRegularJewelsMetadataPath(metadataPath);

            return false;
        }

        private static bool IsRegularJewelsMetadataPath(string metadataPath)
        {
            return metadataPath.IndexOf("Items/Jewels/", StringComparison.OrdinalIgnoreCase) >= 0
                && metadataPath.IndexOf("Items/Jewels/JewelAbyss", StringComparison.OrdinalIgnoreCase) < 0
                && metadataPath.IndexOf("Items/Jewels/JewelPassiveTreeExpansion", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsUniqueItem(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                Mods? mods = itemEntity?.GetComponent<Mods>();
                return mods?.ItemRarity == ItemRarity.Unique
                    && !(itemEntity?.Path?.StartsWith("Metadata/Items/Metamorphosis/", StringComparison.OrdinalIgnoreCase) ?? false);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsHeistQuestContract(string itemName)
        {
            return !string.IsNullOrWhiteSpace(itemName)
                && Constants.HeistQuestContractNames.Contains(itemName);
        }

        private static bool IsHeistNonQuestContract(string itemName)
        {
            return !string.IsNullOrWhiteSpace(itemName)
                && itemName.StartsWith("Contract:", StringComparison.OrdinalIgnoreCase)
                && !Constants.HeistQuestContractNames.Contains(itemName);
        }

        private static bool IsInscribedUltimatum(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                return itemEntity?.Path?.Contains("ItemisedTrial", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetWorldItemMetadataPath(Entity item)
        {
            try
            {
                return EntityHelpers.ResolveWorldItemMetadataPath(item);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetWorldItemBaseName(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                string? itemName = itemEntity?.GetComponent<Base>()?.Name;
                return itemName ?? string.Empty;
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
                if (gameController == null)
                    return false;
                if (itemEntity == null)
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

        public static bool IsStackableCurrency(Entity item, GameController? gameController)
        {
            try
            {
                WorldItem? world = item?.GetComponent<WorldItem>();
                return IsStackableCurrencyCore(world?.ItemEntity, gameController);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsStackableCurrency(LabelOnGround label, GameController? gameController)
        {
            try
            {
                WorldItem? world = label.ItemOnGround?.GetComponent<WorldItem>();
                return IsStackableCurrencyCore(world?.ItemEntity, gameController);
            }
            catch
            {
                return false;
            }
        }
    }
}