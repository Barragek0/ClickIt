using ExileCore.Shared.Enums;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        internal static Func<Keys, bool> KeyStateProvider { get; set; } = Keyboard.IsKeyDown;

        internal static Func<LabelFilterService, IReadOnlyList<global::ExileCore.PoEMemory.Elements.LabelOnGround>?, bool> LazyModeRestrictedChecker { get; set; } = (svc, labels) => svc.HasLazyModeRestrictedItemsOnScreen(labels);

        public string? LastLazyModeRestrictionReason => LazyModeBlockerService.LastRestrictionReason;

        public bool HasLazyModeRestrictedItemsOnScreen(IReadOnlyList<global::ExileCore.PoEMemory.Elements.LabelOnGround>? allLabels)
            => LazyModeBlockerService.HasRestrictedItemsOnScreen(allLabels);

        internal static bool ShouldBlockLazyModeForNearbyMonsters(
            int nearbyNormalCount,
            int normalThreshold,
            int nearbyMagicCount,
            int magicThreshold,
            int nearbyRareCount,
            int rareThreshold,
            int nearbyUniqueCount,
            int uniqueThreshold)
            => global::ClickIt.Services.Label.Application.LazyModeBlockerService.ShouldBlockLazyModeForNearbyMonsters(
                nearbyNormalCount,
                normalThreshold,
                nearbyMagicCount,
                magicThreshold,
                nearbyRareCount,
                rareThreshold,
                nearbyUniqueCount,
                uniqueThreshold);

        internal static string BuildNearbyMonsterBlockReason(
            int nearbyNormalCount,
            int normalThreshold,
            int normalDistance,
            bool normalTriggered,
            int nearbyMagicCount,
            int magicThreshold,
            int magicDistance,
            bool magicTriggered,
            int nearbyRareCount,
            int rareThreshold,
            int rareDistance,
            bool rareTriggered,
            int nearbyUniqueCount,
            int uniqueThreshold,
            int uniqueDistance,
            bool uniqueTriggered)
            => global::ClickIt.Services.Label.Application.LazyModeBlockerService.BuildNearbyMonsterBlockReason(
                nearbyNormalCount,
                normalThreshold,
                normalDistance,
                normalTriggered,
                nearbyMagicCount,
                magicThreshold,
                magicDistance,
                magicTriggered,
                nearbyRareCount,
                rareThreshold,
                rareDistance,
                rareTriggered,
                nearbyUniqueCount,
                uniqueThreshold,
                uniqueDistance,
                uniqueTriggered);
    }
}