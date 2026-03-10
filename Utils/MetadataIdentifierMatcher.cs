namespace ClickIt.Utils
{
    internal static class MetadataIdentifierMatcher
    {
        internal static bool ContainsSingle(string metadataPath, string itemName, string identifier)
        {
            metadataPath ??= string.Empty;
            itemName ??= string.Empty;
            identifier ??= string.Empty;

            if (identifier.Length == 0)
                return false;

            if (identifier.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                string nameFragment = identifier.Substring("name:".Length).Trim();
                return !string.IsNullOrWhiteSpace(nameFragment)
                    && itemName.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (metadataPath.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return identifier.StartsWith("Items/", StringComparison.OrdinalIgnoreCase)
                && metadataPath.IndexOf("Metadata/" + identifier, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool ContainsAny(string metadataPath, string itemName, IReadOnlyList<string> identifiers)
        {
            if (identifiers == null || identifiers.Count == 0)
                return false;

            for (int i = 0; i < identifiers.Count; i++)
            {
                if (ContainsSingle(metadataPath, itemName, identifiers[i] ?? string.Empty))
                    return true;
            }

            return false;
        }
    }
}
