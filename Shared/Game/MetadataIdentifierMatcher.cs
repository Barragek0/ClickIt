namespace ClickIt.Shared.Game
{
    internal static class MetadataIdentifierMatcher
    {
        private static bool IsPathBoundary(char c)
        {
            return !char.IsLetterOrDigit(c);
        }

        private static bool ContainsWithPathBoundaries(string source, string fragment)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(fragment))
                return false;

            int searchStart = 0;
            while (searchStart < source.Length)
            {
                int index = source.IndexOf(fragment, searchStart, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                bool hasStartBoundary = index == 0 || IsPathBoundary(source[index - 1]);
                int endIndex = index + fragment.Length;
                bool hasEndBoundary = endIndex == source.Length || IsPathBoundary(source[endIndex]);

                if (hasStartBoundary && hasEndBoundary)
                    return true;

                searchStart = index + 1;
            }

            return false;
        }

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

            bool strongboxIdentifier = identifier.IndexOf("StrongBoxes/", StringComparison.OrdinalIgnoreCase) >= 0;

            if (strongboxIdentifier)
            {
                if (ContainsWithPathBoundaries(metadataPath, identifier))
                    return true;
            }
            else if (metadataPath.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0)
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
