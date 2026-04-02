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
        private MechanicClassifierDependencies? _classificationDependencies;

        private MechanicClassifierDependencies ClassificationDependencies
            => _classificationDependencies ??= new MechanicClassifierDependencies(
                _worldItemMetadataPolicy.GetWorldItemMetadataPath,
                ShouldAllowWorldItemByMetadata,
                ShouldClickStrongbox,
                static (clickEssences, label) => ShouldClickEssence(clickEssences, label),
                static (clickRitualInitiate, clickRitualCompleted, path, label) => GetRitualMechanicId(clickRitualInitiate, clickRitualCompleted, path, label),
                ShouldAllowClosedDoorPastMechanic);

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
            bool dontClickMatch = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(path, renderName, dontClickMetadata);
            if (dontClickMatch)
                return false;

            return MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(path, renderName, clickMetadata);
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

    }
}