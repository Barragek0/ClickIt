using ClickIt.Definitions;
using ClickIt.Services.Label.Classification;
using ClickIt.Utils;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;

#nullable enable

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private static readonly MechanicClassifierDependencies ClassifierDependencies = new(
            GetWorldItemMetadataPath,
            ShouldAllowWorldItemByMetadata,
            ShouldClickStrongbox,
            static (clickEssences, label) => ShouldClickEssence(clickEssences, label),
            static (clickRitualInitiate, clickRitualCompleted, path, label) => GetRitualMechanicId(clickRitualInitiate, clickRitualCompleted, path, label),
            ShouldAllowClosedDoorPastMechanic);
        private static readonly LabelClassificationEngine ClassificationEngine = new(ClassifierDependencies);

        private static string? GetClickableMechanicId(LabelOnGround label, Entity item, ClickSettings settings, ExileCore.GameController? gameController)
            => ClassificationEngine.GetClickableMechanicId(label, item, settings, gameController);

        internal static string? GetAreaTransitionMechanicId(bool clickAreaTransitions, bool clickLabyrinthTrials, EntityType type, string path)
            => MechanicClassifier.GetAreaTransitionMechanicId(clickAreaTransitions, clickLabyrinthTrials, type, path);

        internal static bool ShouldClickWorldItemCore(bool clickItems, EntityType type, Entity item)
            => MechanicClassifier.ShouldClickWorldItemCore(clickItems, type, item);

        internal static string? GetChestMechanicIdFromConfiguredRules(
            bool clickBasicChests,
            bool clickLeagueChests,
            bool clickLeagueChestsOther,
            IReadOnlySet<string>? enabledSpecificLeagueChestIds,
            EntityType type,
            string? path,
            string renderName)
            => MechanicClassifier.GetChestMechanicIdFromConfiguredRules(
                clickBasicChests,
                clickLeagueChests,
                clickLeagueChestsOther,
                enabledSpecificLeagueChestIds,
                type,
                path,
                renderName);

        internal static bool TryGetSettlersOreMechanicId(string? path, out string? mechanicId)
            => ClassificationEngine.TryGetSettlersOreMechanicId(path, out mechanicId);

        internal static bool IsHarvestPath(string path)
            => MechanicClassifier.IsHarvestPath(path);

        internal static bool IsSettlersPetrifiedWoodPath(string path)
            => MechanicClassifier.IsSettlersPetrifiedWoodPath(path);

        internal static bool IsSettlersOrePath(string path)
            => MechanicClassifier.IsSettlersOrePath(path);

        internal static bool IsSettlersVerisiumPath(string path)
            => MechanicClassifier.IsSettlersVerisiumPath(path);

        internal static bool ShouldClickAltar(bool highlightEater, bool highlightExarch, bool clickEater, bool clickExarch, string path)
            => MechanicClassifier.ShouldClickAltar(highlightEater, highlightExarch, clickEater, clickExarch, path);

        private static bool ShouldClickEssence(bool clickEssences, LabelOnGround label)
        {
            if (!clickEssences)
                return false;

            return LabelUtils.GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

        private static string? GetRitualMechanicId(bool clickRitualInitiate, bool clickRitualCompleted, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("Leagues/Ritual", StringComparison.OrdinalIgnoreCase))
                return null;

            bool hasFavoursText = LabelUtils.GetElementByString(label.Label, "Interact to view Favours") != null;
            if (clickRitualInitiate && !hasFavoursText)
                return MechanicIds.RitualInitiate;
            if (clickRitualCompleted && hasFavoursText)
                return MechanicIds.RitualCompleted;

            return null;
        }

        private static bool ShouldClickStrongbox(ClickSettings settings, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || label?.ItemOnGround == null)
                return false;

            Chest? chest = label.ItemOnGround.GetComponent<Chest>();
            if (chest?.IsLocked != false)
                return false;

            IReadOnlyList<string> clickMetadata = settings.StrongboxClickMetadata ?? [];
            IReadOnlyList<string> dontClickMetadata = settings.StrongboxDontClickMetadata ?? [];
            if (clickMetadata.Count == 0)
                return false;

            if (IsUniqueStrongbox(label))
            {
                if (ContainsStrongboxUniqueIdentifier(dontClickMetadata))
                    return false;

                return ContainsStrongboxUniqueIdentifier(clickMetadata);
            }

            string renderName = label.ItemOnGround.RenderName ?? string.Empty;
            bool dontClickMatch = ContainsAnyMetadataIdentifier(path, renderName, dontClickMetadata);
            if (dontClickMatch)
                return false;

            return ContainsAnyMetadataIdentifier(path, renderName, clickMetadata);
        }

        private static bool ContainsStrongboxUniqueIdentifier(IReadOnlyList<string> metadataIdentifiers)
        {
            if (metadataIdentifiers == null || metadataIdentifiers.Count == 0)
                return false;

            for (int i = 0; i < metadataIdentifiers.Count; i++)
            {
                if (string.Equals(metadataIdentifiers[i], "special:strongbox-unique", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsUniqueStrongbox(LabelOnGround? label)
            => label?.ItemOnGround?.Rarity == MonsterRarity.Unique;

        internal static bool IsBasicChestName(string? name)
            => MechanicClassifier.IsBasicChestName(name);
    }
}