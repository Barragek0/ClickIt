namespace ClickIt.Features.Labels.Classification
{
    internal sealed class LabelInteractionRuleService(
        IWorldItemMetadataPolicy worldItemMetadataPolicy,
        InventoryInteractionPolicy inventoryInteractionPolicy)
    {
        private readonly IWorldItemMetadataPolicy _worldItemMetadataPolicy = worldItemMetadataPolicy;
        private readonly InventoryInteractionPolicy _inventoryInteractionPolicy = inventoryInteractionPolicy;

        public bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label)
        {
            return _worldItemMetadataPolicy.ShouldAllowWorldItemByMetadata(
                settings,
                item,
                gameController,
                label,
                _inventoryInteractionPolicy.ShouldAllowWorldItemWhenInventoryFull);
        }

        public bool ShouldAllowClosedDoorPastMechanic(GameController? gameController)
            => _inventoryInteractionPolicy.ShouldAllowClosedDoorPastMechanic(gameController);

        public static bool ShouldClickEssence(bool clickEssences, LabelOnGround label)
        {
            if (!clickEssences)
                return false;

            return LabelContainsText(label, "The monster is imprisoned by powerful Essences.");
        }

        public static string? GetRitualMechanicId(bool clickRitualInitiate, bool clickRitualCompleted, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("Leagues/Ritual", StringComparison.OrdinalIgnoreCase))
                return null;

            bool hasFavoursText = LabelContainsText(label, "Interact to view Favours");
            if (clickRitualInitiate && !hasFavoursText)
                return MechanicIds.RitualInitiate;
            if (clickRitualCompleted && hasFavoursText)
                return MechanicIds.RitualCompleted;

            return null;
        }

        public static bool ShouldClickStrongbox(ClickSettings settings, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !TryGetLabelItem(label, out object? item) || item == null)
                return false;

            if (!TryGetChestLocked(item, out bool isLocked) || isLocked)
                return false;

            IReadOnlyList<string> clickMetadata = settings.StrongboxClickMetadata ?? [];
            IReadOnlyList<string> dontClickMetadata = settings.StrongboxDontClickMetadata ?? [];
            if (clickMetadata.Count == 0)
                return false;

            if (IsUniqueStrongbox(item))
            {
                if (ContainsStrongboxUniqueIdentifier(dontClickMetadata))
                    return false;

                return ContainsStrongboxUniqueIdentifier(clickMetadata);
            }

            string renderName = DynamicAccess.TryReadString(item, DynamicAccessProfiles.RenderName, out string resolvedRenderName)
                ? resolvedRenderName
                : string.Empty;
            bool dontClickMatch = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(path, renderName, dontClickMetadata);
            if (dontClickMatch)
                return false;

            return MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(path, renderName, clickMetadata);
        }

        private static bool LabelContainsText(LabelOnGround? label, string text)
        {
            return TryGetLabelAdapter(label, out IElementAdapter? adapter)
                && LabelElementSearch.GetElementByStringCore(adapter, text) != null;
        }

        private static bool TryGetLabelAdapter(LabelOnGround? label, out IElementAdapter? adapter)
        {
            adapter = null;
            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                || rawLabel == null)
                return false;

            adapter = rawLabel switch
            {
                IElementAdapter existingAdapter => existingAdapter,
                Element element => new ElementAdapter(element),
                _ => null,
            };

            return adapter != null;
        }

        private static bool TryGetLabelItem(LabelOnGround? label, out object? item)
        {
            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out item)
                && item != null;
        }

        private static bool TryGetChestLocked(object item, out bool isLocked)
        {
            isLocked = false;
            if (!DynamicAccess.TryGetComponent<Chest>(item, out object? rawChest)
                || rawChest == null)
                return false;

            return DynamicAccess.TryReadBool(rawChest, DynamicAccessProfiles.IsLocked, out isLocked);
        }

        private static bool ContainsStrongboxUniqueIdentifier(IReadOnlyList<string> metadataIdentifiers)
        {
            if (metadataIdentifiers == null || metadataIdentifiers.Count == 0)
                return false;

            for (int i = 0; i < metadataIdentifiers.Count; i++)
                if (string.Equals(metadataIdentifiers[i], "special:strongbox-unique", StringComparison.OrdinalIgnoreCase))
                    return true;


            return false;
        }

        private static bool IsUniqueStrongbox(object item)
        {
            if (!DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Rarity, out object? rawRarity)
                || rawRarity == null)
                return false;

            return rawRarity switch
            {
                MonsterRarity rarity => rarity == MonsterRarity.Unique,
                int rarityValue => rarityValue == (int)MonsterRarity.Unique,
                _ => false,
            };
        }
    }
}