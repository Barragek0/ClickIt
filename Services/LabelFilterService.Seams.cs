using ClickIt.Utils;
using ClickIt.Definitions;
using ExileCore.Shared.Enums;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        internal static Func<Keys, bool> KeyStateProvider { get; set; } = Keyboard.IsKeyDown;

        internal static Func<LabelFilterService, IReadOnlyList<global::ExileCore.PoEMemory.Elements.LabelOnGround>?, bool> LazyModeRestrictedChecker { get; set; } = (svc, labels) => svc.HasLazyModeRestrictedItemsOnScreenImpl(labels);

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

        // Legacy reflective seam retained in seams file so runtime interaction rules stay clean.
        private static string? GetChestMechanicIdInternal(
            bool clickBasicChests,
            bool clickLeagueChests,
            bool clickLeagueChestsOther,
            bool clickMirageGoldenDjinnCache,
            bool clickMirageSilverDjinnCache,
            bool clickMirageBronzeDjinnCache,
            bool clickHeistSecureLocker,
            bool clickBreachGraspingCoffers,
            bool clickSynthesisSynthesisedStash,
            EntityType type,
            string? path,
            string renderName)
        {
            IReadOnlySet<string> enabledSpecificLeagueChestIds = BuildEnabledLeagueChestSpecificIdSetFromLegacyFlags(
                clickMirageGoldenDjinnCache,
                clickMirageSilverDjinnCache,
                clickMirageBronzeDjinnCache,
                clickHeistSecureLocker,
                clickBreachGraspingCoffers,
                clickSynthesisSynthesisedStash);

            return GetChestMechanicIdFromConfiguredRules(
                clickBasicChests,
                clickLeagueChests,
                clickLeagueChestsOther,
                enabledSpecificLeagueChestIds,
                type,
                path,
                renderName);
        }

        private static IReadOnlySet<string> BuildEnabledLeagueChestSpecificIdSetFromLegacyFlags(
            bool clickMirageGoldenDjinnCache,
            bool clickMirageSilverDjinnCache,
            bool clickMirageBronzeDjinnCache,
            bool clickHeistSecureLocker,
            bool clickBreachGraspingCoffers,
            bool clickSynthesisSynthesisedStash)
        {
            HashSet<string> enabled = new(StringComparer.OrdinalIgnoreCase);
            AddLeagueChestSpecificIdIfEnabled(enabled, clickMirageGoldenDjinnCache, MechanicIds.MirageGoldenDjinnCache);
            AddLeagueChestSpecificIdIfEnabled(enabled, clickMirageSilverDjinnCache, MechanicIds.MirageSilverDjinnCache);
            AddLeagueChestSpecificIdIfEnabled(enabled, clickMirageBronzeDjinnCache, MechanicIds.MirageBronzeDjinnCache);
            AddLeagueChestSpecificIdIfEnabled(enabled, clickHeistSecureLocker, MechanicIds.HeistSecureLocker);
            AddLeagueChestSpecificIdIfEnabled(enabled, clickBreachGraspingCoffers, MechanicIds.BreachGraspingCoffers);
            AddLeagueChestSpecificIdIfEnabled(enabled, clickSynthesisSynthesisedStash, MechanicIds.SynthesisSynthesisedStash);
            return enabled;
        }

        private static void AddLeagueChestSpecificIdIfEnabled(HashSet<string> enabledIds, bool isEnabled, string specificId)
        {
            if (!isEnabled || string.IsNullOrWhiteSpace(specificId))
                return;

            enabledIds.Add(specificId);
        }
    }
}
