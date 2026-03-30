using ClickIt.Definitions;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        internal static bool MatchesMetadataFiltersForTests(string metadataPath, IReadOnlyList<string>? whitelist, IReadOnlyList<string>? blacklist)
        {
            return MatchesMetadataFiltersForTests(metadataPath, string.Empty, whitelist, blacklist);
        }

        internal static bool MatchesMetadataFiltersForTests(string metadataPath, string itemName, IReadOnlyList<string>? whitelist, IReadOnlyList<string>? blacklist)
        {
            return MatchesMetadataFiltersForTests(metadataPath, itemName, string.Empty, whitelist, blacklist);
        }

        internal static bool MatchesMetadataFiltersForTests(string metadataPath, string itemName, string labelText, IReadOnlyList<string>? whitelist, IReadOnlyList<string>? blacklist)
        {
            whitelist ??= [];
            blacklist ??= [];

            bool whitelistPass = whitelist.Count == 0 || ContainsAnyMetadataIdentifier(metadataPath ?? string.Empty, itemName ?? string.Empty, item: null, labelText ?? string.Empty, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && ContainsAnyMetadataIdentifier(metadataPath ?? string.Empty, itemName ?? string.Empty, item: null, labelText ?? string.Empty, blacklist);
            return !blacklistMatch;
        }

        private static bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label)
        {
            string metadata = GetWorldItemMetadataPath(item);
            string itemName = GetWorldItemBaseName(item);
            string labelText = GetWorldItemLabelText(label);

            IReadOnlyList<string> whitelist = settings.ItemTypeWhitelistMetadata ?? [];
            IReadOnlyList<string> blacklist = settings.ItemTypeBlacklistMetadata ?? [];

            bool whitelistPass = whitelist.Count == 0 || ContainsAnyMetadataIdentifier(metadata, itemName, item, labelText, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && ContainsAnyMetadataIdentifier(metadata, itemName, item, labelText, blacklist);
            if (blacklistMatch)
                return false;

            return ShouldAllowWorldItemWhenInventoryFull(item, gameController);
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
