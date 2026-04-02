using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ClickIt.Services.Label.Inventory;
using ClickIt.Definitions;

namespace ClickIt.Services.Label.Classification
{
    internal sealed class LabelInteractionRuleService(
        IWorldItemMetadataPolicy worldItemMetadataPolicy,
        InventoryDomainFacade inventoryDomain,
        string stoneOfPassageMetadataIdentifier)
    {
        private readonly IWorldItemMetadataPolicy _worldItemMetadataPolicy = worldItemMetadataPolicy;
        private readonly InventoryDomainFacade _inventoryDomain = inventoryDomain;
        private readonly string _stoneOfPassageMetadataIdentifier = stoneOfPassageMetadataIdentifier;

        public bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label)
        {
            return _worldItemMetadataPolicy.ShouldAllowWorldItemByMetadata(
                settings,
                item,
                gameController,
                label,
                _inventoryDomain.ShouldAllowWorldItemWhenInventoryFull);
        }

        public bool ShouldAllowClosedDoorPastMechanic(GameController? gameController)
            => _inventoryDomain.ShouldAllowClosedDoorPastMechanic(gameController, _stoneOfPassageMetadataIdentifier);

        public static bool ShouldClickEssence(bool clickEssences, LabelOnGround label)
        {
            if (!clickEssences)
                return false;

            return LabelUtils.GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

        public static string? GetRitualMechanicId(bool clickRitualInitiate, bool clickRitualCompleted, string path, LabelOnGround label)
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

        public static bool ShouldClickStrongbox(ClickSettings settings, string path, LabelOnGround label)
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