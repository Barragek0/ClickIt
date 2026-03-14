using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private static bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController)
        {
            string metadata = GetWorldItemMetadataPath(item);
            string itemName = GetWorldItemBaseName(item);
            IReadOnlyList<string> whitelist = settings.ItemTypeWhitelistMetadata ?? [];
            IReadOnlyList<string> blacklist = settings.ItemTypeBlacklistMetadata ?? [];

            bool whitelistPass = whitelist.Count == 0 || ContainsAnyMetadataIdentifier(metadata, itemName, item, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && ContainsAnyMetadataIdentifier(metadata, itemName, item, blacklist);
            if (blacklistMatch)
                return false;

            return ShouldAllowWorldItemWhenInventoryFull(item, gameController);
        }

        private static bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
        {
            if (!IsInventoryFullCore(gameController))
                return true;

            Entity? groundItemEntity = TryGetWorldItemEntity(groundItem);
            bool isStackable = IsStackableCurrencyCore(groundItemEntity, gameController);
            bool hasPartialMatchingStack = HasMatchingPartialStackInInventoryCore(groundItemEntity?.Path, gameController);
            return ShouldPickupWhenInventoryFullCore(inventoryFull: true, isStackable, hasPartialMatchingStack);
        }

        internal static bool ShouldPickupWhenInventoryFullCore(bool inventoryFull, bool isStackable, bool hasPartialMatchingStack)
        {
            return !inventoryFull || (isStackable && hasPartialMatchingStack);
        }

        internal static bool IsPartialStackCore(int currentStackSize, int maxStackSize)
        {
            return currentStackSize > 0 && maxStackSize > 0 && currentStackSize < maxStackSize;
        }

        private static bool IsInventoryFullCore(GameController? gameController)
        {
            if (!TryResolveInventorySlotStates(gameController, out int totalSlots, out int occupiedSlots))
                return false;

            return totalSlots > 0 && occupiedSlots >= totalSlots;
        }

        private static bool TryResolveInventorySlotStates(GameController? gameController, out int totalSlots, out int occupiedSlots)
        {
            totalSlots = 0;
            occupiedSlots = 0;

            if (!TryEnumerateInventorySlotEntries(gameController, out IReadOnlyList<object?> slots))
                return false;

            for (int i = 0; i < slots.Count; i++)
            {
                object? slot = slots[i];
                if (slot == null)
                    continue;

                totalSlots++;
                if (IsInventorySlotOccupied(slot))
                    occupiedSlots++;
            }

            return totalSlots > 0;
        }

        private static bool IsInventorySlotOccupied(object slot)
        {
            if (TryGetPropertyValue(slot, "HasItem", out object? hasItemObj) && hasItemObj is bool hasItem)
                return hasItem;

            if (TryGetPropertyValue(slot, "IsEmpty", out object? isEmptyObj) && isEmptyObj is bool isEmpty)
                return !isEmpty;

            return TryGetNestedEntityLikeObject(slot, out _);
        }

        private static bool HasMatchingPartialStackInInventoryCore(string? worldItemPath, GameController? gameController)
        {
            if (string.IsNullOrWhiteSpace(worldItemPath))
                return false;

            if (!TryEnumerateInventoryItemEntities(gameController, out IReadOnlyList<Entity> inventoryItems))
                return false;

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity inventoryItem = inventoryItems[i];
                if (inventoryItem == null)
                    continue;

                string inventoryPath = inventoryItem.Path ?? string.Empty;
                if (!inventoryPath.Equals(worldItemPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryResolveStackSizes(inventoryItem, gameController, out int currentStack, out int maxStack))
                    continue;

                if (IsPartialStackCore(currentStack, maxStack))
                    return true;
            }

            return false;
        }

        private static bool TryResolveStackSizes(Entity itemEntity, GameController? gameController, out int currentStack, out int maxStack)
        {
            currentStack = 0;
            maxStack = 0;

            if (itemEntity == null || gameController == null)
                return false;

            try
            {
                object? stackComponent = itemEntity.GetComponent<ExileCore.PoEMemory.Components.Stack>();
                if (!TryReadIntMember(stackComponent, ["Size", "Count", "StackSize", "Amount"], out currentStack))
                    return false;
            }
            catch
            {
                return false;
            }

            try
            {
                object? baseItemType = gameController.Files?.BaseItemTypes?.Translate(itemEntity.Path ?? string.Empty);
                if (!TryReadIntMember(baseItemType, ["StackSize", "MaxStackSize", "Stack"], out maxStack))
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static bool TryReadIntMember(object? source, IReadOnlyList<string> memberNames, out int value)
        {
            value = 0;
            if (source == null)
                return false;

            for (int i = 0; i < memberNames.Count; i++)
            {
                string member = memberNames[i];
                if (!TryGetPropertyValue(source, member, out object? raw))
                    continue;

                if (raw is int intValue)
                {
                    value = intValue;
                    return true;
                }

                try
                {
                    value = Convert.ToInt32(raw);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static Entity? TryGetWorldItemEntity(Entity? worldItem)
        {
            if (worldItem == null)
                return null;

            try
            {
                WorldItem? worldItemComp = worldItem.GetComponent<WorldItem>();
                return worldItemComp?.ItemEntity;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryEnumerateInventorySlotEntries(GameController? gameController, out IReadOnlyList<object?> slots)
        {
            slots = [];

            if (!TryGetInventoryRoots(gameController, out IReadOnlyList<object?> roots))
                return false;

            string[] slotCollectionNames = ["VisibleInventorySlots", "InventorySlots", "Slots", "Cells", "InventorySlotItems"];
            foreach (object? root in roots)
            {
                if (root == null)
                    continue;

                for (int i = 0; i < slotCollectionNames.Length; i++)
                {
                    if (!TryGetPropertyValue(root, slotCollectionNames[i], out object? collectionObj) || collectionObj == null)
                        continue;

                    List<object?> extracted = [.. EnumerateObjects(collectionObj)];
                    if (extracted.Count == 0)
                        continue;

                    slots = extracted;
                    return true;
                }
            }

            return false;
        }

        private static bool TryEnumerateInventoryItemEntities(GameController? gameController, out IReadOnlyList<Entity> items)
        {
            items = [];

            if (!TryGetInventoryRoots(gameController, out IReadOnlyList<object?> roots))
                return false;

            var entities = new List<Entity>(64);

            string[] itemCollectionNames = ["VisibleInventoryItems", "InventoryItems", "Items", "InventorySlotItems", "Slots", "Cells"];
            foreach (object? root in roots)
            {
                if (root == null)
                    continue;

                for (int i = 0; i < itemCollectionNames.Length; i++)
                {
                    if (!TryGetPropertyValue(root, itemCollectionNames[i], out object? collectionObj) || collectionObj == null)
                        continue;

                    foreach (object? entry in EnumerateObjects(collectionObj))
                    {
                        if (entry is Entity directEntity)
                        {
                            entities.Add(directEntity);
                            continue;
                        }

                        if (TryGetNestedEntityLikeObject(entry, out Entity? nestedEntity) && nestedEntity != null)
                        {
                            entities.Add(nestedEntity);
                        }
                    }
                }
            }

            if (entities.Count == 0)
                return false;

            // Deduplicate by address to avoid repeated matches across multiple reflected paths.
            var unique = new Dictionary<long, Entity>();
            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];
                if (entity == null)
                    continue;

                long address = entity.Address;
                if (address == 0)
                    continue;

                unique[address] = entity;
            }

            items = [.. unique.Values];
            return items.Count > 0;
        }

        private static bool TryGetInventoryRoots(GameController? gameController, out IReadOnlyList<object?> roots)
        {
            roots = [];
            var ingameUi = gameController?.IngameState?.IngameUi;
            if (ingameUi == null)
                return false;

            var candidates = new List<object?>(6)
            {
                ingameUi
            };

            object? inventoryPanel = null;
            try
            {
                inventoryPanel = ingameUi.InventoryPanel;
            }
            catch
            {
            }

            if (inventoryPanel == null && TryGetPropertyValue(ingameUi, "InventoryPanel", out object? reflectedInventoryPanel))
            {
                inventoryPanel = reflectedInventoryPanel;
            }

            if (inventoryPanel != null)
            {
                candidates.Add(inventoryPanel);

                object? inventoryObj = null;
                if (TryGetPropertyValue(inventoryPanel, "Inventory", out object? reflectedInventory))
                {
                    inventoryObj = reflectedInventory;
                }

                if (inventoryObj != null)
                    candidates.Add(inventoryObj);
            }

            object? inventoryDirect = null;
            if (TryGetPropertyValue(ingameUi, "Inventory", out object? reflectedInventoryDirect))
            {
                inventoryDirect = reflectedInventoryDirect;
            }

            if (inventoryDirect != null)
                candidates.Add(inventoryDirect);

            roots = candidates;
            return roots.Count > 0;
        }

        private static bool TryGetNestedEntityLikeObject(object? source, out Entity? entity)
        {
            entity = null;
            if (source == null)
                return false;

            if (source is Entity directEntity)
            {
                entity = directEntity;
                return true;
            }

            string[] candidateMembers = ["ItemEntity", "Item", "Entity"];
            for (int i = 0; i < candidateMembers.Length; i++)
            {
                if (!TryGetPropertyValue(source, candidateMembers[i], out object? nested) || nested == null)
                    continue;

                if (nested is Entity nestedEntity)
                {
                    entity = nestedEntity;
                    return true;
                }

                if (nested != source && TryGetNestedEntityLikeObject(nested, out Entity? deepEntity) && deepEntity != null)
                {
                    entity = deepEntity;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetPropertyValue(object source, string propertyName, out object? value)
        {
            value = null;
            if (source == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            try
            {
                var prop = source.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop == null)
                    return false;

                value = prop.GetValue(source);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<object?> EnumerateObjects(object? source)
        {
            if (source == null)
                yield break;

            if (source is string)
            {
                yield return source;
                yield break;
            }

            if (source is System.Collections.IEnumerable enumerable)
            {
                foreach (object? entry in enumerable)
                    yield return entry;
                yield break;
            }

            yield return source;
        }

        private static bool ContainsAnyMetadataIdentifier(string metadataPath, string itemName, IReadOnlyList<string> identifiers)
        {
            return ContainsAnyMetadataIdentifier(metadataPath, itemName, item: null, identifiers);
        }

        private static bool ContainsAnyMetadataIdentifier(string metadataPath, string itemName, Entity? item, IReadOnlyList<string> identifiers)
        {
            if (identifiers == null || identifiers.Count == 0)
                return false;

            metadataPath ??= string.Empty;
            itemName ??= string.Empty;

            for (int i = 0; i < identifiers.Count; i++)
            {
                string identifier = identifiers[i] ?? string.Empty;
                if (identifier.Length == 0)
                    continue;

                if (TryGetSpecialRule(identifier, out string specialRule))
                {
                    if (MatchesSpecialRule(specialRule, metadataPath, itemName, item))
                        return true;

                    continue;
                }

                if (MetadataIdentifierMatcher.ContainsSingle(metadataPath, itemName, identifier))
                    return true;
            }

            return false;
        }

        private static bool TryGetSpecialRule(string identifier, out string specialRule)
        {
            specialRule = string.Empty;
            if (!identifier.StartsWith("special:", StringComparison.OrdinalIgnoreCase))
                return false;

            specialRule = identifier.Substring("special:".Length).Trim();
            return specialRule.Length > 0;
        }

        private static bool MatchesSpecialRule(string specialRule, string metadataPath, string itemName, Entity? item)
        {
            if (specialRule.Equals("unique-items", StringComparison.OrdinalIgnoreCase))
                return item != null && IsUniqueItem(item);

            if (specialRule.Equals("heist-quest-contract", StringComparison.OrdinalIgnoreCase))
                return IsHeistQuestContract(itemName);

            if (specialRule.Equals("heist-non-quest-contract", StringComparison.OrdinalIgnoreCase))
                return IsHeistNonQuestContract(itemName);

            if (specialRule.Equals("inscribed-ultimatum", StringComparison.OrdinalIgnoreCase))
            {
                return (item != null && IsInscribedUltimatum(item))
                    || metadataPath.IndexOf("ItemisedTrial", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (specialRule.Equals("jewels-regular", StringComparison.OrdinalIgnoreCase))
                return IsRegularJewelsMetadataPath(metadataPath);

            return false;
        }

        private static bool IsRegularJewelsMetadataPath(string metadataPath)
        {
            return metadataPath.IndexOf("Items/Jewels/", StringComparison.OrdinalIgnoreCase) >= 0
                && metadataPath.IndexOf("Items/Jewels/JewelAbyss", StringComparison.OrdinalIgnoreCase) < 0
                && metadataPath.IndexOf("Items/Jewels/JewelPassiveTreeExpansion", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsUniqueItem(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                Mods? mods = itemEntity?.GetComponent<Mods>();
                return mods?.ItemRarity == ItemRarity.Unique
                    && !(itemEntity?.Path?.StartsWith("Metadata/Items/Metamorphosis/", StringComparison.OrdinalIgnoreCase) ?? false);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsHeistQuestContract(string itemName)
        {
            return !string.IsNullOrWhiteSpace(itemName)
                && Constants.HeistQuestContractNames.Contains(itemName);
        }

        private static bool IsHeistNonQuestContract(string itemName)
        {
            return !string.IsNullOrWhiteSpace(itemName)
                && itemName.StartsWith("Contract:", StringComparison.OrdinalIgnoreCase)
                && !Constants.HeistQuestContractNames.Contains(itemName);
        }

        private static bool IsInscribedUltimatum(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                return itemEntity?.Path?.Contains("ItemisedTrial", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetWorldItemMetadataPath(Entity item)
        {
            try
            {
                string resolvedMetadata = EntityHelpers.ResolveWorldItemMetadataPath(item);
                if (TryGetWorldItemComponentMetadata(item, out string componentMetadata))
                {
                    return SelectBestWorldItemMetadataPath(resolvedMetadata, componentMetadata);
                }

                return resolvedMetadata;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryGetWorldItemComponentMetadata(Entity? item, out string metadata)
        {
            metadata = string.Empty;
            if (item == null)
                return false;

            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                string itemEntityMetadata = itemEntity?.Metadata ?? string.Empty;
                if (string.IsNullOrWhiteSpace(itemEntityMetadata))
                    return false;

                metadata = itemEntityMetadata;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string SelectBestWorldItemMetadataPath(string resolvedMetadata, string componentMetadata)
        {
            if (string.IsNullOrWhiteSpace(componentMetadata))
                return resolvedMetadata ?? string.Empty;

            if (string.IsNullOrWhiteSpace(resolvedMetadata))
                return componentMetadata;

            // When label/path resolution points to misc world objects, prefer concrete item metadata.
            if (resolvedMetadata.IndexOf("Metadata/MiscellaneousObjects/", StringComparison.OrdinalIgnoreCase) >= 0)
                return componentMetadata;

            return resolvedMetadata;
        }

        private static string GetWorldItemBaseName(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                string? itemName = itemEntity?.GetComponent<Base>()?.Name;
                return itemName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsStackableCurrencyCore(Entity? itemEntity, GameController? gameController)
        {
            try
            {
                if (gameController == null)
                    return false;
                if (itemEntity == null)
                    return false;

                var baseItemType = gameController.Files.BaseItemTypes.Translate(itemEntity.Path ?? string.Empty);
                if (baseItemType == null)
                    return false;

                return string.Equals(baseItemType.ClassName, "StackableCurrency", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
