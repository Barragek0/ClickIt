using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using Microsoft.CSharp.RuntimeBinder;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const int InventoryDebugTrailCapacity = 32;
        private static readonly Utils.DebugSnapshotStore<InventoryDebugSnapshot> InventoryDebugStore = new(
            InventoryDebugSnapshot.Empty,
            InventoryDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot =>
                $"{snapshot.Sequence:00000} {snapshot.Stage} | f:{snapshot.InventoryFull} a:{snapshot.DecisionAllowPickup} c:{snapshot.CapacityCells} o:{snapshot.OccupiedCells} s:{snapshot.IsGroundStackable} p:{snapshot.HasPartialMatchingStack} n:{snapshot.Notes}");

        public sealed record InventoryDebugSnapshot(
            bool HasData,
            string Stage,
            bool InventoryFull,
            string InventoryFullSource,
            bool HasPrimaryInventory,
            bool UsedFullFlag,
            bool FullFlagValue,
            bool UsedCellOccupancy,
            int CapacityCells,
            int OccupiedCells,
            int InventoryEntityCount,
            int LayoutEntryCount,
            string GroundItemPath,
            string GroundItemName,
            bool IsGroundStackable,
            int MatchingPathCount,
            int PartialMatchingStackCount,
            bool HasPartialMatchingStack,
            bool DecisionAllowPickup,
            string Notes,
            long Sequence,
            long TimestampMs)
        {
            public static readonly InventoryDebugSnapshot Empty = new(
                HasData: false,
                Stage: string.Empty,
                InventoryFull: false,
                InventoryFullSource: string.Empty,
                HasPrimaryInventory: false,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: false,
                CapacityCells: 0,
                OccupiedCells: 0,
                InventoryEntityCount: 0,
                LayoutEntryCount: 0,
                GroundItemPath: string.Empty,
                GroundItemName: string.Empty,
                IsGroundStackable: false,
                MatchingPathCount: 0,
                PartialMatchingStackCount: 0,
                HasPartialMatchingStack: false,
                DecisionAllowPickup: false,
                Notes: string.Empty,
                Sequence: 0,
                TimestampMs: 0);
        }

        private readonly record struct InventoryFullProbe(
            bool HasPrimaryInventory,
            bool UsedFullFlag,
            bool FullFlagValue,
            bool UsedCellOccupancy,
            int CapacityCells,
            int OccupiedCells,
            int InventoryEntityCount,
            int LayoutEntryCount,
            bool IsFull,
            string Source,
            string Notes)
        {
            public static readonly InventoryFullProbe Empty = new(
                HasPrimaryInventory: false,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: false,
                CapacityCells: 0,
                OccupiedCells: 0,
                InventoryEntityCount: 0,
                LayoutEntryCount: 0,
                IsFull: false,
                Source: string.Empty,
                Notes: string.Empty);
        }

        public static InventoryDebugSnapshot GetLatestInventoryDebug()
        {
            return InventoryDebugStore.GetLatest();
        }

        public static IReadOnlyList<string> GetLatestInventoryDebugTrail()
        {
            return InventoryDebugStore.GetTrail();
        }

        private static void PublishInventoryDebug(InventoryDebugSnapshot snapshot)
        {
            InventoryDebugStore.SetLatest(snapshot);
        }

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
            bool inventoryFull = IsInventoryFullCore(gameController, out InventoryFullProbe probe);
            if (!inventoryFull)
            {
                PublishInventoryDebug(new InventoryDebugSnapshot(
                    HasData: true,
                    Stage: "InventoryNotFullAllow",
                    InventoryFull: false,
                    InventoryFullSource: probe.Source,
                    HasPrimaryInventory: probe.HasPrimaryInventory,
                    UsedFullFlag: probe.UsedFullFlag,
                    FullFlagValue: probe.FullFlagValue,
                    UsedCellOccupancy: probe.UsedCellOccupancy,
                    CapacityCells: probe.CapacityCells,
                    OccupiedCells: probe.OccupiedCells,
                    InventoryEntityCount: probe.InventoryEntityCount,
                    LayoutEntryCount: probe.LayoutEntryCount,
                    GroundItemPath: string.Empty,
                    GroundItemName: string.Empty,
                    IsGroundStackable: false,
                    MatchingPathCount: 0,
                    PartialMatchingStackCount: 0,
                    HasPartialMatchingStack: false,
                    DecisionAllowPickup: true,
                    Notes: probe.Notes,
                    Sequence: 0,
                    TimestampMs: Environment.TickCount64));

                return true;
            }

            Entity? groundItemEntity = TryGetWorldItemEntity(groundItem);
            string groundItemPath = groundItemEntity?.Path ?? string.Empty;
            string groundItemName = GetWorldItemBaseName(groundItem);
            bool isStackable = IsGroundItemStackableCore(groundItemEntity);
            int matchingPathCount = 0;
            int partialMatchingStackCount = 0;
            bool hasPartialMatchingStack = isStackable
                && HasMatchingPartialStackInInventoryCore(
                    groundItemPath,
                    gameController,
                    out matchingPathCount,
                    out partialMatchingStackCount);
            bool allowPickup = ShouldPickupWhenInventoryFullCore(inventoryFull: true, isStackable, hasPartialMatchingStack);

            PublishInventoryDebug(new InventoryDebugSnapshot(
                HasData: true,
                Stage: "InventoryFullDecision",
                InventoryFull: true,
                InventoryFullSource: probe.Source,
                HasPrimaryInventory: probe.HasPrimaryInventory,
                UsedFullFlag: probe.UsedFullFlag,
                FullFlagValue: probe.FullFlagValue,
                UsedCellOccupancy: probe.UsedCellOccupancy,
                CapacityCells: probe.CapacityCells,
                OccupiedCells: probe.OccupiedCells,
                InventoryEntityCount: probe.InventoryEntityCount,
                LayoutEntryCount: probe.LayoutEntryCount,
                GroundItemPath: groundItemPath,
                GroundItemName: groundItemName,
                IsGroundStackable: isStackable,
                MatchingPathCount: matchingPathCount,
                PartialMatchingStackCount: partialMatchingStackCount,
                HasPartialMatchingStack: hasPartialMatchingStack,
                DecisionAllowPickup: allowPickup,
                Notes: probe.Notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));

            return allowPickup;
        }

        internal static bool ShouldPickupWhenInventoryFullCore(bool inventoryFull, bool isStackable, bool hasPartialMatchingStack)
        {
            return !inventoryFull || (isStackable && hasPartialMatchingStack);
        }

        internal static bool IsPartialStackCore(int currentStackSize, int maxStackSize)
        {
            return currentStackSize > 0 && maxStackSize > 0 && currentStackSize < maxStackSize;
        }

        internal static bool IsPartialServerStackCore(bool fullStack, int size)
        {
            return size > 0 && !fullStack;
        }

        internal static bool IsInventoryCellUsageFullCore(int occupiedCellCount, int totalCellCapacity)
        {
            return totalCellCapacity > 0 && occupiedCellCount >= totalCellCapacity;
        }

        private static bool IsInventoryFullCore(GameController? gameController)
        {
            return IsInventoryFullCore(gameController, out _);
        }

        private static bool IsInventoryFullCore(GameController? gameController, out InventoryFullProbe probe)
        {
            probe = InventoryFullProbe.Empty;

            if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
            {
                probe = probe with { Notes = "Primary server inventory missing" };
                return false;
            }

            if (TryReadBool(primaryInventory, out bool full, s => s.IsFull))
            {
                probe = new InventoryFullProbe(
                    HasPrimaryInventory: true,
                    UsedFullFlag: true,
                    FullFlagValue: full,
                    UsedCellOccupancy: false,
                    CapacityCells: 0,
                    OccupiedCells: 0,
                    InventoryEntityCount: 0,
                    LayoutEntryCount: 0,
                    IsFull: full,
                    Source: "IsFull",
                    Notes: "Inventory fullness from server flag IsFull");
                return full;
            }
            if (TryReadBool(primaryInventory, out full, s => s.Full))
            {
                probe = new InventoryFullProbe(
                    HasPrimaryInventory: true,
                    UsedFullFlag: true,
                    FullFlagValue: full,
                    UsedCellOccupancy: false,
                    CapacityCells: 0,
                    OccupiedCells: 0,
                    InventoryEntityCount: 0,
                    LayoutEntryCount: 0,
                    IsFull: full,
                    Source: "Full",
                    Notes: "Inventory fullness from server flag Full");
                return full;
            }
            if (TryReadBool(primaryInventory, out full, s => s.InventoryFull))
            {
                probe = new InventoryFullProbe(
                    HasPrimaryInventory: true,
                    UsedFullFlag: true,
                    FullFlagValue: full,
                    UsedCellOccupancy: false,
                    CapacityCells: 0,
                    OccupiedCells: 0,
                    InventoryEntityCount: 0,
                    LayoutEntryCount: 0,
                    IsFull: full,
                    Source: "InventoryFull",
                    Notes: "Inventory fullness from server flag InventoryFull");
                return full;
            }

            if (!TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity))
            {
                probe = probe with
                {
                    HasPrimaryInventory = true,
                    Notes = "Unable to resolve inventory capacity"
                };
                return false;
            }

            if (!TryEnumeratePrimaryInventoryItemEntities(primaryInventory, out IReadOnlyList<Entity> inventoryItems, out string itemEnumDebug))
            {
                probe = probe with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    Notes = $"Unable to enumerate PlayerInventories[0].Inventory.Items ({itemEnumDebug})"
                };
                return false;
            }

            if (!TryResolveOccupiedInventoryCells(inventoryItems, totalCellCapacity, out int occupiedCellCount))
            {
                probe = probe with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    InventoryEntityCount = inventoryItems.Count,
                    LayoutEntryCount = inventoryItems.Count,
                    Notes = "Unable to resolve occupied inventory cells from PlayerInventories[0].Inventory.Items"
                };
                return false;
            }

            bool isFullByOccupancy = IsInventoryCellUsageFullCore(occupiedCellCount, totalCellCapacity);
            probe = new InventoryFullProbe(
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: totalCellCapacity,
                OccupiedCells: occupiedCellCount,
                InventoryEntityCount: inventoryItems.Count,
                LayoutEntryCount: inventoryItems.Count,
                IsFull: isFullByOccupancy,
                Source: "CellOccupancy",
                Notes: $"Inventory fullness from PlayerInventories[0].Inventory.Items footprint ({itemEnumDebug})");

            return isFullByOccupancy;
        }

        private static bool TryResolveOccupiedInventoryCells(
            IReadOnlyList<Entity> inventoryItems,
            int totalCellCapacity,
            out int occupiedCellCount)
        {
            occupiedCellCount = 0;
            if (totalCellCapacity <= 0)
                return false;

            if (inventoryItems == null || inventoryItems.Count == 0)
                return true;

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity itemEntity = inventoryItems[i];
                if (itemEntity == null)
                    continue;

                TryResolveInventoryItemSize(itemEntity, out int width, out int height);
                occupiedCellCount += Math.Max(1, width) * Math.Max(1, height);
                if (occupiedCellCount >= totalCellCapacity)
                {
                    occupiedCellCount = totalCellCapacity;
                    return true;
                }
            }

            occupiedCellCount = Math.Min(totalCellCapacity, occupiedCellCount);
            return true;
        }

        private static bool TryResolveInventoryCapacity(object primaryInventory, out int totalCellCapacity)
        {
            totalCellCapacity = 0;

            if (TryReadInt(primaryInventory, out int width, s => s.Width)
                && TryReadInt(primaryInventory, out int height, s => s.Height)
                && width > 0
                && height > 0)
            {
                totalCellCapacity = width * height;
                return true;
            }

            if (TryReadInt(primaryInventory, out int totalBoxes, s => s.TotalBoxes) && totalBoxes > 0)
            {
                totalCellCapacity = totalBoxes;
                return true;
            }

            if (TryReadInt(primaryInventory, out int capacity, s => s.Capacity) && capacity > 0)
            {
                totalCellCapacity = capacity;
                return true;
            }

            totalCellCapacity = 60;
            return true;
        }

        private static bool TryResolveInventoryItemSize(Entity itemEntity, out int width, out int height)
        {
            width = 1;
            height = 1;

            try
            {
                Base? baseComponent = itemEntity.GetComponent<Base>();
                bool widthResolved = TryReadInt(baseComponent, out width, s => s.Width);
                bool heightResolved = TryReadInt(baseComponent, out height, s => s.Height);
                if (widthResolved && heightResolved)
                {
                    width = Math.Max(1, width);
                    height = Math.Max(1, height);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsGroundItemStackableCore(Entity? itemEntity)
        {
            if (itemEntity == null)
                return false;

            return TryResolveServerStackState(itemEntity, out _, out _);
        }

        private static bool HasMatchingPartialStackInInventoryCore(
            string? worldItemPath,
            GameController? gameController,
            out int matchingPathCount,
            out int partialMatchingStackCount)
        {
            matchingPathCount = 0;
            partialMatchingStackCount = 0;

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

                matchingPathCount++;

                if (!TryResolveServerStackState(inventoryItem, out bool fullStack, out int stackSize))
                    continue;

                if (IsPartialServerStackCore(fullStack, stackSize))
                {
                    partialMatchingStackCount++;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveServerStackState(Entity itemEntity, out bool fullStack, out int stackSize)
        {
            fullStack = false;
            stackSize = 0;

            if (itemEntity == null)
                return false;

            try
            {
                object? stackComponent = itemEntity.GetComponent<ExileCore.PoEMemory.Components.Stack>();
                if (!TryReadBool(stackComponent, out fullStack, s => s.FullStack)
                    && !TryReadBool(stackComponent, out fullStack, s => s.IsFull)
                    && !TryReadBool(stackComponent, out fullStack, s => s.Full))
                    return false;

                if (!TryReadInt(stackComponent, out stackSize, s => s.Size)
                    && !TryReadInt(stackComponent, out stackSize, s => s.Count)
                    && !TryReadInt(stackComponent, out stackSize, s => s.StackSize)
                    && !TryReadInt(stackComponent, out stackSize, s => s.Amount))
                    return false;
            }
            catch
            {
                return false;
            }

            return stackSize > 0;
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

        private static bool TryEnumerateInventoryItemEntities(GameController? gameController, out IReadOnlyList<Entity> items)
        {
            items = [];

            if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
                return false;

            if (!TryEnumeratePrimaryInventoryItemEntities(primaryInventory, out IReadOnlyList<Entity> entities, out _))
                return false;

            items = entities;
            return items.Count > 0;
        }

        private static bool TryEnumeratePrimaryInventoryItemEntities(object primaryInventory, out IReadOnlyList<Entity> items, out string debugDetails)
        {
            items = [];
            debugDetails = string.Empty;

            if (!TryGetPrimaryServerInventoryItems(primaryInventory, out object? collectionObj, out string collectionDebug) || collectionObj == null)
            {
                debugDetails = $"items-collection: {collectionDebug}";
                return false;
            }

            var entities = new List<Entity>(64);
            int totalEntries = 0;
            int nullEntries = 0;
            int directEntityEntries = 0;
            int nestedEntityEntries = 0;
            int rejectedNonItemEntity = 0;
            int rejectedNestedNonEntity = 0;

            foreach (object? entry in EnumerateObjects(collectionObj))
            {
                totalEntries++;

                if (entry == null)
                {
                    nullEntries++;
                    continue;
                }

                if (entry is Entity directEntity)
                {
                    directEntityEntries++;
                    if (!IsInventoryItemEntity(directEntity, out _))
                    {
                        rejectedNonItemEntity++;
                        continue;
                    }

                    entities.Add(directEntity);
                    continue;
                }

                if (TryGetDynamicValue(entry, s => s.ItemEntity, out object? nestedItemObj)
                    && nestedItemObj is Entity nestedItemEntity
                    && IsInventoryItemEntity(nestedItemEntity, out _))
                {
                    nestedEntityEntries++;
                    entities.Add(nestedItemEntity);
                    continue;
                }

                rejectedNestedNonEntity++;
            }

            if (entities.Count == 0)
            {
                debugDetails =
                    $"{collectionDebug}; entries:{totalEntries} null:{nullEntries} direct:{directEntityEntries} nested:{nestedEntityEntries} rejectedNonItem:{rejectedNonItemEntity} rejectedNested:{rejectedNestedNonEntity}";
                return false;
            }

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
            debugDetails =
                $"{collectionDebug}; entries:{totalEntries} null:{nullEntries} direct:{directEntityEntries} nested:{nestedEntityEntries} rejectedNonItem:{rejectedNonItemEntity} rejectedNested:{rejectedNestedNonEntity} dedup:{entities.Count}->{items.Count}";
            return items.Count > 0;
        }

        private static bool TryGetPrimaryServerInventory(GameController? gameController, out object? primaryInventory)
        {
            primaryInventory = null;

            object? data = gameController?.IngameState?.Data;
            if (data == null)
                return false;

            object? serverData = null;
            if (!TryGetDynamicValue(data, s => s.ServerData, out serverData) || serverData == null)
                return false;

            if (!TryGetDynamicValue(serverData, s => s.PlayerInventories, out object? playerInventories) || playerInventories == null)
                return false;

            if (!TryGetFirstCollectionObject(playerInventories, out object? firstInventory) || firstInventory == null)
                return false;

            // Strict server-data path: IngameState.Data.ServerData.PlayerInventories[0]
            primaryInventory = firstInventory;
            return true;
        }

        private static bool TryGetPrimaryServerInventoryItems(object primaryInventory, out object? itemsCollection, out string debugDetails)
        {
            itemsCollection = null;
            debugDetails = string.Empty;

            if (!TryGetDynamicValue(primaryInventory, s => s.Inventory, out object? inventoryObj) || inventoryObj == null)
            {
                debugDetails = "read-failed: PlayerInventories[0].Inventory accessor unavailable";
                return false;
            }

            bool readOk = TryGetDynamicValue(inventoryObj, s => s.Items, out itemsCollection);
            if (!readOk)
            {
                debugDetails = "read-failed: PlayerInventories[0].Inventory.Items accessor unavailable";
                return false;
            }

            if (itemsCollection == null)
            {
                debugDetails = "read-ok: PlayerInventories[0].Inventory.Items is null";
                return false;
            }

            int previewCount = 0;
            foreach (object? _ in EnumerateObjects(itemsCollection))
            {
                previewCount++;
                if (previewCount >= 8)
                    break;
            }

            debugDetails = $"read-ok: PlayerInventories[0].Inventory.Items type={itemsCollection.GetType().Name} previewCount={previewCount}";
            return true;
        }

        private static bool TryGetPrimaryServerInventoryItems(object primaryInventory, out object? itemsCollection)
        {
            return TryGetPrimaryServerInventoryItems(primaryInventory, out itemsCollection, out _);
        }

        private static bool TryGetFirstCollectionObject(object collection, out object? first)
        {
            first = null;

            if (collection is System.Collections.IList list)
            {
                if (list.Count <= 0)
                    return false;

                first = list[0];
                return first != null;
            }

            foreach (object? entry in EnumerateObjects(collection))
            {
                first = entry;
                return first != null;
            }

            return false;
        }

        private static bool IsInventoryItemEntity(Entity? entity)
        {
            return IsInventoryItemEntity(entity, out _);
        }

        private static bool IsInventoryItemEntity(Entity? entity, out string reason)
        {
            reason = string.Empty;
            if (entity == null)
            {
                reason = "entity-null";
                return false;
            }

            string path = entity.Path ?? string.Empty;
            if (path.Length == 0)
            {
                reason = "path-empty";
                return false;
            }

            bool isItem = path.IndexOf("Metadata/Items/", StringComparison.OrdinalIgnoreCase) >= 0;
            string shortPath = path.Length <= 56 ? path : path.Substring(0, 56) + "...";
            reason = isItem ? "path-item" : $"path-non-item:{shortPath}";
            return isItem;
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

        private static bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
        {
            return DynamicAccess.TryReadBool(source, accessor, out value);
        }

        private static bool TryReadInt(object? source, out int value, Func<dynamic, object?> accessor)
        {
            return DynamicAccess.TryReadInt(source, accessor, out value);
        }

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
        {
            return DynamicAccess.TryGetDynamicValue(source, accessor, out value);
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
