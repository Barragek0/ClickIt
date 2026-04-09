namespace ClickIt.Features.Labels.Classification
{
    internal static class MetadataIdentifierRuleSet
    {
        internal static bool ContainsAnyMetadataIdentifier(string metadataPath, string itemName, IReadOnlyList<string> identifiers)
            => ContainsAnyMetadataIdentifier(metadataPath, itemName, item: null, labelText: string.Empty, identifiers);

        internal static bool ContainsAnyMetadataIdentifier(string metadataPath, string itemName, Entity? item, string labelText, IReadOnlyList<string> identifiers)
        {
            if (identifiers == null || identifiers.Count == 0)
                return false;

            metadataPath ??= string.Empty;
            itemName ??= string.Empty;
            labelText ??= string.Empty;

            for (int i = 0; i < identifiers.Count; i++)
            {
                string identifier = identifiers[i] ?? string.Empty;
                if (identifier.Length == 0)
                    continue;

                if (TryGetSpecialRule(identifier, out string specialRule))
                {
                    if (MatchesSpecialRule(specialRule, metadataPath, itemName, item, labelText))
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

            specialRule = identifier["special:".Length..].Trim();
            return specialRule.Length > 0;
        }

        private static bool MatchesSpecialRule(string specialRule, string metadataPath, string itemName, Entity? item, string labelText)
        {
            if (specialRule.Equals("unique-items", StringComparison.OrdinalIgnoreCase))
                return item != null && IsUniqueItem(item);
            if (specialRule.Equals("heist-quest-contract", StringComparison.OrdinalIgnoreCase))
                return IsHeistQuestContract(itemName);
            if (specialRule.Equals("heist-non-quest-contract", StringComparison.OrdinalIgnoreCase))
                return IsHeistNonQuestContract(itemName);
            if (specialRule.Equals("inscribed-ultimatum", StringComparison.OrdinalIgnoreCase))
                return (item != null && IsInscribedUltimatum(item)) || metadataPath.Contains("ItemisedTrial", StringComparison.OrdinalIgnoreCase);
            if (specialRule.Equals("jewels-regular", StringComparison.OrdinalIgnoreCase))
                return IsRegularJewelsMetadataPath(metadataPath);
            if (specialRule.Equals("mysterious-wombgift-label", StringComparison.OrdinalIgnoreCase))
                return string.Equals(labelText.Trim(), "Mysterious Wombgift", StringComparison.OrdinalIgnoreCase);

            return false;
        }

        private static bool IsRegularJewelsMetadataPath(string metadataPath)
        {
            return metadataPath.Contains("Items/Jewels/", StringComparison.OrdinalIgnoreCase)
                && !metadataPath.Contains("Items/Jewels/JewelAbyss", StringComparison.OrdinalIgnoreCase)
                && !metadataPath.Contains("Items/Jewels/JewelPassiveTreeExpansion", StringComparison.OrdinalIgnoreCase);
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
            => !string.IsNullOrWhiteSpace(itemName) && Constants.HeistQuestContractNames.Contains(itemName);

        private static bool IsHeistNonQuestContract(string itemName)
            => !string.IsNullOrWhiteSpace(itemName)
               && itemName.StartsWith("Contract:", StringComparison.OrdinalIgnoreCase)
               && !Constants.HeistQuestContractNames.Contains(itemName);

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
    }
}
