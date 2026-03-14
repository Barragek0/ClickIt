using ExileCore.PoEMemory.Elements;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        internal static Func<Keys, bool> KeyStateProvider { get; set; } = Keyboard.IsKeyDown;

        internal static Func<LabelFilterService, IReadOnlyList<LabelOnGround>?, bool> LazyModeRestrictedChecker { get; set; } = (svc, labels) => svc.HasLazyModeRestrictedItemsOnScreenImpl(labels);

        internal static bool MatchesMetadataFiltersForTests(string metadataPath, IReadOnlyList<string>? whitelist, IReadOnlyList<string>? blacklist)
        {
            return MatchesMetadataFiltersForTests(metadataPath, string.Empty, whitelist, blacklist);
        }

        internal static bool MatchesMetadataFiltersForTests(string metadataPath, string itemName, IReadOnlyList<string>? whitelist, IReadOnlyList<string>? blacklist)
        {
            whitelist ??= [];
            blacklist ??= [];

            bool whitelistPass = whitelist.Count == 0 || ContainsAnyMetadataIdentifier(metadataPath ?? string.Empty, itemName ?? string.Empty, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && ContainsAnyMetadataIdentifier(metadataPath ?? string.Empty, itemName ?? string.Empty, blacklist);
            return !blacklistMatch;
        }
    }
}
