using ClickIt.Definitions;
using ClickIt.Services.Label.Classification;
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

            bool whitelistPass = whitelist.Count == 0 || MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(metadataPath ?? string.Empty, itemName ?? string.Empty, item: null, labelText ?? string.Empty, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(metadataPath ?? string.Empty, itemName ?? string.Empty, item: null, labelText ?? string.Empty, blacklist);
            return !blacklistMatch;
        }

        private bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label)
        {
            return _worldItemMetadataPolicy.ShouldAllowWorldItemByMetadata(settings, item, gameController, label, ShouldAllowWorldItemWhenInventoryFull);
        }

    }
}
