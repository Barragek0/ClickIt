using ExileCore.PoEMemory.Elements;
using ClickIt.Utils;

namespace ClickIt.Services
{
    // Seams and helpers kept in a separate partial file so production code remains focused.
    public partial class LabelFilterService
    {
        // Test seam - delegate used to query key state so test environments don't need native Win32
        internal static Func<Keys, bool> KeyStateProvider { get; set; } = Keyboard.IsKeyDown;

        // Test seam - allows tests to short-circuit the expensive/native dependent check
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
