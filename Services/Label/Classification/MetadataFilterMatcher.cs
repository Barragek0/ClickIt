namespace ClickIt.Services.Label.Classification
{
    internal static class MetadataFilterMatcher
    {
        internal static bool Matches(
            string metadataPath,
            IReadOnlyList<string>? whitelist,
            IReadOnlyList<string>? blacklist)
            => Matches(metadataPath, string.Empty, string.Empty, whitelist, blacklist);

        internal static bool Matches(
            string metadataPath,
            string itemName,
            IReadOnlyList<string>? whitelist,
            IReadOnlyList<string>? blacklist)
            => Matches(metadataPath, itemName, string.Empty, whitelist, blacklist);

        internal static bool Matches(
            string metadataPath,
            string itemName,
            string labelText,
            IReadOnlyList<string>? whitelist,
            IReadOnlyList<string>? blacklist)
        {
            whitelist ??= [];
            blacklist ??= [];

            string safeMetadataPath = metadataPath ?? string.Empty;
            string safeItemName = itemName ?? string.Empty;
            string safeLabelText = labelText ?? string.Empty;

            bool whitelistPass = whitelist.Count == 0
                || MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(safeMetadataPath, safeItemName, item: null, safeLabelText, whitelist);
            if (!whitelistPass)
            {
                return false;
            }

            bool blacklistMatch = blacklist.Count > 0
                && MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(safeMetadataPath, safeItemName, item: null, safeLabelText, blacklist);
            return !blacklistMatch;
        }
    }
}