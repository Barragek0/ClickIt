using System;
using System.Collections.Generic;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private static class InventorySnapshotProvider
        {
            public static bool TryBuild(GameController? gameController, out InventorySnapshot snapshot)
            {
                snapshot = InventorySnapshot.Empty;

                if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
                    return false;

                if (!InventoryLayoutParser.TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity))
                    return false;

                if (!InventoryLayoutParser.TryResolveInventoryDimensions(primaryInventory, out int inventoryWidth, out int inventoryHeight))
                    return false;

                if (!InventoryLayoutParser.TryResolveInventoryLayoutEntries(
                    primaryInventory,
                    inventoryWidth,
                    inventoryHeight,
                    out IReadOnlyList<InventoryLayoutEntry> entries,
                    out string source,
                    out string debugDetails,
                    out bool isReliable,
                    out int rawEntryCount))
                {
                    InventoryFullProbe layoutFailureProbe = InventoryFullProbe.Empty with
                    {
                        HasPrimaryInventory = true,
                        CapacityCells = totalCellCapacity,
                        Notes = $"Unable to resolve inventory layout entries from {source} ({debugDetails})"
                    };

                    snapshot = InventorySnapshot.Empty with
                    {
                        HasPrimaryInventory = true,
                        PrimaryInventory = primaryInventory,
                        CapacityCells = totalCellCapacity,
                        Width = inventoryWidth,
                        Height = inventoryHeight,
                        FullProbe = layoutFailureProbe
                    };
                    return true;
                }

                InventoryLayoutSnapshot layoutSnapshot = new(
                    Entries: entries,
                    Source: source,
                    DebugDetails: debugDetails,
                    IsReliable: isReliable,
                    RawEntryCount: rawEntryCount);

                if (TryReadInventoryFullFlag(primaryInventory, out bool fullFlagValue, out string fullFlagSource))
                {
                    InventoryFullProbe fullFlagProbe = CreateInventoryFullFlagProbe(fullFlagValue, fullFlagSource);
                    snapshot = new InventorySnapshot(
                        HasPrimaryInventory: true,
                        PrimaryInventory: primaryInventory,
                        CapacityCells: totalCellCapacity,
                        Width: inventoryWidth,
                        Height: inventoryHeight,
                        Layout: layoutSnapshot,
                        OccupiedCells: 0,
                        FullProbe: fullFlagProbe,
                        InventoryItems: ResolveInventoryItems(primaryInventory));
                    return true;
                }

                if (!isReliable)
                {
                    InventoryFullProbe unreliableProbe = InventoryFullProbe.Empty with
                    {
                        HasPrimaryInventory = true,
                        CapacityCells = totalCellCapacity,
                        InventoryEntityCount = rawEntryCount,
                        LayoutEntryCount = entries.Count,
                        Notes = $"Inventory layout unreliable from {source} ({debugDetails})"
                    };

                    snapshot = new InventorySnapshot(
                        HasPrimaryInventory: true,
                        PrimaryInventory: primaryInventory,
                        CapacityCells: totalCellCapacity,
                        Width: inventoryWidth,
                        Height: inventoryHeight,
                        Layout: layoutSnapshot,
                        OccupiedCells: 0,
                        FullProbe: unreliableProbe,
                        InventoryItems: ResolveInventoryItems(primaryInventory));
                    return true;
                }

                if (!InventoryFitEvaluator.TryResolveOccupiedInventoryCellsFromLayout(entries, inventoryWidth, inventoryHeight, out int occupiedCellCount))
                {
                    InventoryFullProbe occupiedCellFailureProbe = InventoryFullProbe.Empty with
                    {
                        HasPrimaryInventory = true,
                        CapacityCells = totalCellCapacity,
                        InventoryEntityCount = rawEntryCount,
                        LayoutEntryCount = entries.Count,
                        Notes = $"Unable to resolve occupied cells from {source}"
                    };

                    snapshot = new InventorySnapshot(
                        HasPrimaryInventory: true,
                        PrimaryInventory: primaryInventory,
                        CapacityCells: totalCellCapacity,
                        Width: inventoryWidth,
                        Height: inventoryHeight,
                        Layout: layoutSnapshot,
                        OccupiedCells: 0,
                        FullProbe: occupiedCellFailureProbe,
                        InventoryItems: ResolveInventoryItems(primaryInventory));
                    return true;
                }

                bool isFull = IsInventoryCellUsageFullCore(occupiedCellCount, totalCellCapacity);
                InventoryFullProbe occupancyProbe = new(
                    HasPrimaryInventory: true,
                    UsedFullFlag: false,
                    FullFlagValue: false,
                    UsedCellOccupancy: true,
                    CapacityCells: totalCellCapacity,
                    OccupiedCells: occupiedCellCount,
                    InventoryEntityCount: rawEntryCount,
                    LayoutEntryCount: entries.Count,
                    IsFull: isFull,
                    Source: "CellOccupancy",
                    Notes: $"Inventory fullness from {source} footprint ({debugDetails})");

                snapshot = new InventorySnapshot(
                    HasPrimaryInventory: true,
                    PrimaryInventory: primaryInventory,
                    CapacityCells: totalCellCapacity,
                    Width: inventoryWidth,
                    Height: inventoryHeight,
                    Layout: layoutSnapshot,
                    OccupiedCells: occupiedCellCount,
                    FullProbe: occupancyProbe,
                    InventoryItems: ResolveInventoryItems(primaryInventory));
                return true;
            }

            private static IReadOnlyList<Entity> ResolveInventoryItems(object primaryInventory)
            {
                return TryEnumeratePrimaryInventoryItemEntitiesFast(primaryInventory, out IReadOnlyList<Entity> entities)
                    ? entities
                    : Array.Empty<Entity>();
            }
        }

        private static class InventoryDynamicAdapter
        {
            public static bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
                => DynamicAccess.TryReadBool(source, accessor, out value);

            public static bool TryReadInt(object? source, out int value, Func<dynamic, object?> accessor)
                => DynamicAccess.TryReadInt(source, accessor, out value);

            public static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
                => DynamicAccess.TryGetDynamicValue(source, accessor, out value);

            public static bool TryGetPrimaryServerInventory(GameController? gameController, out object? primaryInventory)
            {
                primaryInventory = null;

                object? data = gameController?.IngameState?.Data;
                if (data == null)
                    return false;

                if (!TryGetDynamicValue(data, s => s.ServerData, out object? serverData) || serverData == null)
                    return false;

                if (!TryGetDynamicValue(serverData, s => s.PlayerInventories, out object? playerInventories) || playerInventories == null)
                    return false;

                if (!TryGetFirstCollectionObject(playerInventories, out object? firstInventory) || firstInventory == null)
                    return false;

                primaryInventory = firstInventory;
                return true;
            }

            public static bool TryGetPrimaryServerInventorySlotItems(object primaryInventory, out object? slotItemsCollection)
            {
                slotItemsCollection = null;

                if (!TryGetDynamicValue(primaryInventory, s => s.Inventory, out object? inventoryObj) || inventoryObj == null)
                    return false;

                if (!TryGetDynamicValue(inventoryObj, s => s.InventorySlotItems, out slotItemsCollection))
                    return false;

                return slotItemsCollection != null;
            }
        }

        private readonly record struct InventoryPickupSnapshot(
            InventoryFullProbe Probe,
            bool InventoryFull,
            Entity? GroundItemEntity,
            string GroundItemPath,
            string GroundItemName,
            bool IsStackable,
            int MatchingPathCount,
            int PartialMatchingStackCount,
            bool HasPartialMatchingStack,
            bool HasSpaceForGroundItem);

        private static class InventoryPickupSnapshotBuilder
        {
            public static InventoryPickupSnapshot Build(Entity groundItem, GameController? gameController)
            {
                bool inventoryFull = IsInventoryFullCore(gameController, out InventoryFullProbe probe);

                Entity? groundItemEntity = TryGetWorldItemEntity(groundItem);
                string groundItemPath = groundItemEntity?.Path ?? string.Empty;
                string groundItemName = GetWorldItemBaseName(groundItem);
                bool isStackable = IsGroundItemStackableCore(groundItemEntity);

                int matchingPathCount = 0;
                int partialMatchingStackCount = 0;
                bool hasPartialMatchingStack = isStackable
                    && HasMatchingPartialStackInInventoryCore(
                        groundItemPath,
                        groundItemEntity,
                        gameController,
                        out matchingPathCount,
                        out partialMatchingStackCount);

                bool hasSpaceForGroundItem = HasInventorySpaceForGroundItemCore(groundItemEntity, gameController);

                return new InventoryPickupSnapshot(
                    Probe: probe,
                    InventoryFull: inventoryFull,
                    GroundItemEntity: groundItemEntity,
                    GroundItemPath: groundItemPath,
                    GroundItemName: groundItemName,
                    IsStackable: isStackable,
                    MatchingPathCount: matchingPathCount,
                    PartialMatchingStackCount: partialMatchingStackCount,
                    HasPartialMatchingStack: hasPartialMatchingStack,
                    HasSpaceForGroundItem: hasSpaceForGroundItem);
            }
        }

        private interface IInventoryIntMemberReadStrategy
        {
            string Name { get; }
            bool TryRead(object source, out int value);
        }

        private sealed class InventoryIntMemberReadStrategy(string name, Func<object, (bool ok, int value)> reader) : IInventoryIntMemberReadStrategy
        {
            public string Name { get; } = name;

            public bool TryRead(object source, out int value)
            {
                (bool ok, int resolvedValue) = reader(source);
                value = resolvedValue;
                return ok;
            }
        }

        private static class InventoryLayoutParser
        {
            private static readonly IInventoryIntMemberReadStrategy[] IntMemberReadStrategies =
            [
                CreateIntReadStrategy("PosX", static s => ReadIntDynamic(s, static d => d.PosX)),
                CreateIntReadStrategy("PosY", static s => ReadIntDynamic(s, static d => d.PosY)),
                CreateIntReadStrategy("InventoryX", static s => ReadIntDynamic(s, static d => d.InventoryX)),
                CreateIntReadStrategy("InventoryY", static s => ReadIntDynamic(s, static d => d.InventoryY)),
                CreateIntReadStrategy("ItemCellX", static s => ReadIntDynamic(s, static d => d.ItemCellX)),
                CreateIntReadStrategy("ItemCellY", static s => ReadIntDynamic(s, static d => d.ItemCellY)),
                CreateIntReadStrategy("CellX", static s => ReadIntDynamic(s, static d => d.CellX)),
                CreateIntReadStrategy("CellY", static s => ReadIntDynamic(s, static d => d.CellY)),
                CreateIntReadStrategy("PositionX", static s => ReadIntDynamic(s, static d => d.PositionX)),
                CreateIntReadStrategy("PositionY", static s => ReadIntDynamic(s, static d => d.PositionY)),
                CreateIntReadStrategy("X", static s => ReadIntDynamic(s, static d => d.X)),
                CreateIntReadStrategy("Y", static s => ReadIntDynamic(s, static d => d.Y)),
                CreateIntReadStrategy("Column", static s => ReadIntDynamic(s, static d => d.Column)),
                CreateIntReadStrategy("Row", static s => ReadIntDynamic(s, static d => d.Row)),
                CreateIntReadStrategy("SizeX", static s => ReadIntDynamic(s, static d => d.SizeX)),
                CreateIntReadStrategy("SizeY", static s => ReadIntDynamic(s, static d => d.SizeY))
            ];

            public static bool TryResolveInventoryCapacity(object primaryInventory, out int totalCellCapacity)
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

            public static bool TryResolveInventoryDimensions(object primaryInventory, out int width, out int height)
            {
                width = 0;
                height = 0;

                if (TryReadInt(primaryInventory, out int resolvedWidth, s => s.Width)
                    && TryReadInt(primaryInventory, out int resolvedHeight, s => s.Height)
                    && resolvedWidth > 0
                    && resolvedHeight > 0)
                {
                    width = resolvedWidth;
                    height = resolvedHeight;
                    return true;
                }

                if (TryReadInt(primaryInventory, out int totalBoxes, s => s.TotalBoxes) && totalBoxes > 0)
                {
                    width = 12;
                    height = Math.Max(1, totalBoxes / 12);
                    return true;
                }

                if (TryReadInt(primaryInventory, out int capacity, s => s.Capacity) && capacity > 0)
                {
                    width = 12;
                    height = Math.Max(1, capacity / 12);
                    return true;
                }

                width = 12;
                height = 5;
                return true;
            }

            public static bool TryResolveInventoryLayoutEntries(
                object primaryInventory,
                int inventoryWidth,
                int inventoryHeight,
                out IReadOnlyList<InventoryLayoutEntry> entries,
                out string source,
                out string debugDetails,
                out bool isReliable,
                out int rawEntryCount)
            {
                entries = Array.Empty<InventoryLayoutEntry>();
                source = string.Empty;
                debugDetails = string.Empty;
                isReliable = false;
                rawEntryCount = 0;

                if (inventoryWidth <= 0 || inventoryHeight <= 0)
                    return false;

                long now = Environment.TickCount64;
                if (TryGetCachedInventoryLayout(primaryInventory, now, inventoryWidth, inventoryHeight, out InventoryLayoutSnapshot cachedSnapshot))
                {
                    entries = cachedSnapshot.Entries;
                    source = cachedSnapshot.Source;
                    debugDetails = cachedSnapshot.DebugDetails;
                    isReliable = cachedSnapshot.IsReliable;
                    rawEntryCount = cachedSnapshot.RawEntryCount;
                    return true;
                }

                if (TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? slotItemsCollection) && slotItemsCollection != null)
                {
                    if (TryBuildInventoryLayoutEntriesFromCollection(slotItemsCollection, inventoryWidth, inventoryHeight, out List<InventoryLayoutEntry> slotEntries, out int slotRawCount))
                    {
                        entries = slotEntries;
                        source = "PlayerInventories[0].InventorySlotItems";
                        rawEntryCount = slotRawCount;
                        isReliable = slotRawCount == 0 || slotEntries.Count > 0;
                        debugDetails = $"raw:{slotRawCount} parsed:{slotEntries.Count}";
                        SetCachedInventoryLayout(
                            primaryInventory,
                            now,
                            inventoryWidth,
                            inventoryHeight,
                            new InventoryLayoutSnapshot(entries, source, debugDetails, isReliable, rawEntryCount));
                        return true;
                    }
                }

                source = "PlayerInventories[0].InventorySlotItems";
                debugDetails = "read-failed: PlayerInventories[0].InventorySlotItems accessor unavailable or unreadable";
                SetCachedInventoryLayout(
                    primaryInventory,
                    now,
                    inventoryWidth,
                    inventoryHeight,
                    new InventoryLayoutSnapshot(entries, source, debugDetails, isReliable, rawEntryCount));
                return false;
            }

            public static bool TryBuildInventoryLayoutEntriesFromCollection(
                object collection,
                int inventoryWidth,
                int inventoryHeight,
                out List<InventoryLayoutEntry> entries,
                out int rawEntryCount)
            {
                entries = new List<InventoryLayoutEntry>();
                rawEntryCount = 0;
                if (inventoryWidth <= 0 || inventoryHeight <= 0)
                    return false;

                entries.EnsureCapacity(32);
                foreach (object? entry in EnumerateObjects(collection))
                {
                    if (entry == null)
                        continue;

                    rawEntryCount++;
                    Entity? itemEntity = TryGetInventoryItemEntityFromEntry(entry);

                    if (!TryResolveInventoryItemPosition(entry, itemEntity, out int x, out int y))
                        continue;

                    if (!TryResolveInventoryEntrySize(entry, itemEntity, out int width, out int height))
                        continue;

                    int clampedX = Math.Clamp(x, 0, Math.Max(0, inventoryWidth - 1));
                    int clampedY = Math.Clamp(y, 0, Math.Max(0, inventoryHeight - 1));
                    int clampedWidth = Math.Max(1, Math.Min(width, inventoryWidth - clampedX));
                    int clampedHeight = Math.Max(1, Math.Min(height, inventoryHeight - clampedY));

                    entries.Add(new InventoryLayoutEntry(clampedX, clampedY, clampedWidth, clampedHeight));
                }

                return true;
            }

            public static bool TryResolveInventoryItemPosition(object entry, Entity? itemEntity, out int x, out int y)
            {
                if (TryReadInventoryCoordinates(entry, out x, out y))
                    return true;

                if (itemEntity != null && TryReadInventoryCoordinates(itemEntity, out x, out y))
                    return true;

                x = 0;
                y = 0;
                return false;
            }

            public static bool TryResolveInventoryEntrySize(object entry, Entity? itemEntity, out int width, out int height)
            {
                if (TryReadCoordinatePair(entry, "SizeX", "SizeY", out width, out height)
                    && width > 0
                    && height > 0)
                {
                    width = Math.Max(1, width);
                    height = Math.Max(1, height);
                    return true;
                }

                if (itemEntity != null && TryResolveInventoryItemSize(itemEntity, out width, out height))
                    return true;

                width = 1;
                height = 1;
                return false;
            }

            public static bool TryReadInventoryCoordinates(object source, out int x, out int y)
            {
                x = 0;
                y = 0;

                if (TryReadCoordinatePair(source, "PosX", "PosY", out x, out y)) return true;
                if (TryReadCoordinatePair(source, "InventoryX", "InventoryY", out x, out y)) return true;
                if (TryReadCoordinatePair(source, "ItemCellX", "ItemCellY", out x, out y)) return true;
                if (TryReadCoordinatePair(source, "CellX", "CellY", out x, out y)) return true;
                if (TryReadCoordinatePair(source, "PositionX", "PositionY", out x, out y)) return true;
                if (TryReadCoordinatePair(source, "X", "Y", out x, out y)) return true;
                if (TryReadCoordinatePair(source, "Column", "Row", out x, out y)) return true;

                if (TryGetDynamicValue(source, s => s.InventoryPosition, out object? inventoryPosition)
                    && inventoryPosition != null
                    && TryReadCoordinatePair(inventoryPosition, "X", "Y", out x, out y))
                    return true;

                if (TryGetDynamicValue(source, s => s.InventoryPositionNum, out object? inventoryPositionNum)
                    && inventoryPositionNum != null
                    && TryReadCoordinatePair(inventoryPositionNum, "X", "Y", out x, out y))
                    return true;

                if (TryGetDynamicValue(source, s => s.Location, out object? location)
                    && location != null)
                {
                    if (TryGetDynamicValue(location, s => s.InventoryPosition, out object? locationInventoryPosition)
                        && locationInventoryPosition != null
                        && TryReadCoordinatePair(locationInventoryPosition, "X", "Y", out x, out y))
                        return true;

                    if (TryGetDynamicValue(location, s => s.InventoryPositionNum, out object? locationInventoryPositionNum)
                        && locationInventoryPositionNum != null
                        && TryReadCoordinatePair(locationInventoryPositionNum, "X", "Y", out x, out y))
                        return true;
                }

                return false;
            }

            public static bool TryReadCoordinatePair(object source, string xFieldName, string yFieldName, out int x, out int y)
            {
                x = 0;
                y = 0;

                if (!TryReadIntByName(source, xFieldName, out x))
                    return false;

                if (!TryReadIntByName(source, yFieldName, out y))
                    return false;

                return x >= 0 && y >= 0;
            }

            public static bool TryReadIntByName(object source, string memberName, out int value)
            {
                for (int i = 0; i < IntMemberReadStrategies.Length; i++)
                {
                    IInventoryIntMemberReadStrategy strategy = IntMemberReadStrategies[i];
                    if (!strategy.Name.Equals(memberName, StringComparison.Ordinal))
                        continue;

                    return strategy.TryRead(source, out value);
                }

                value = 0;
                return false;
            }

            private static IInventoryIntMemberReadStrategy CreateIntReadStrategy(
                string name,
                Func<object, (bool ok, int value)> reader)
                => new InventoryIntMemberReadStrategy(name, reader);

            private static (bool ok, int value) ReadIntDynamic(object source, Func<dynamic, object?> accessor)
            {
                return TryReadInt(source, out int value, accessor)
                    ? (true, value)
                    : (false, 0);
            }
        }

        private static class InventoryFitEvaluator
        {
            public static bool TryResolveOccupiedInventoryCellsFromLayout(
                IReadOnlyList<InventoryLayoutEntry> layoutEntries,
                int inventoryWidth,
                int inventoryHeight,
                out int occupiedCellCount)
            {
                occupiedCellCount = 0;
                if (inventoryWidth <= 0 || inventoryHeight <= 0)
                    return false;

                if (layoutEntries == null || layoutEntries.Count == 0)
                    return true;

                int totalCells = inventoryWidth * inventoryHeight;
                Span<byte> occupied = totalCells <= 256
                    ? stackalloc byte[totalCells]
                    : new byte[totalCells];

                for (int i = 0; i < layoutEntries.Count; i++)
                {
                    InventoryLayoutEntry entry = layoutEntries[i];
                    int maxX = Math.Min(inventoryWidth, entry.X + entry.Width);
                    int maxY = Math.Min(inventoryHeight, entry.Y + entry.Height);

                    for (int y = Math.Max(0, entry.Y); y < maxY; y++)
                    {
                        for (int x = Math.Max(0, entry.X); x < maxX; x++)
                        {
                            int index = (y * inventoryWidth) + x;
                            if (occupied[index] != 0)
                                continue;

                            occupied[index] = 1;
                            occupiedCellCount++;
                        }
                    }
                }

                return true;
            }

            public static bool HasSpaceForItemFootprint(
                int inventoryWidth,
                int inventoryHeight,
                IReadOnlyList<InventoryLayoutEntry> occupiedEntries,
                int requiredWidth,
                int requiredHeight)
            {
                if (inventoryWidth <= 0 || inventoryHeight <= 0)
                    return false;

                if (requiredWidth <= 0 || requiredHeight <= 0)
                    return false;

                if (requiredWidth > inventoryWidth || requiredHeight > inventoryHeight)
                    return false;

                int totalCells = inventoryWidth * inventoryHeight;
                Span<byte> occupied = totalCells <= 256
                    ? stackalloc byte[totalCells]
                    : new byte[totalCells];

                for (int i = 0; i < occupiedEntries.Count; i++)
                {
                    InventoryLayoutEntry entry = occupiedEntries[i];
                    int maxX = Math.Min(inventoryWidth, entry.X + entry.Width);
                    int maxY = Math.Min(inventoryHeight, entry.Y + entry.Height);
                    for (int y = Math.Max(0, entry.Y); y < maxY; y++)
                    {
                        for (int x = Math.Max(0, entry.X); x < maxX; x++)
                            occupied[(y * inventoryWidth) + x] = 1;
                    }
                }

                int maxStartX = inventoryWidth - requiredWidth;
                int maxStartY = inventoryHeight - requiredHeight;
                for (int startY = 0; startY <= maxStartY; startY++)
                {
                    for (int startX = 0; startX <= maxStartX; startX++)
                    {
                        if (CanPlaceAt(occupied, inventoryWidth, startX, startY, requiredWidth, requiredHeight))
                            return true;
                    }
                }

                return false;
            }

            private static bool CanPlaceAt(Span<byte> occupied, int inventoryWidth, int startX, int startY, int width, int height)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    for (int x = startX; x < startX + width; x++)
                    {
                        if (occupied[(y * inventoryWidth) + x] != 0)
                            return false;
                    }
                }

                return true;
            }
        }

        private static class InventoryPickupPolicy
        {
            public static bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            {
                InventoryPickupSnapshot snapshot = InventoryPickupSnapshotBuilder.Build(groundItem, gameController);

                if (ShouldAllowPickupWhenPrimaryInventoryMissingCore(snapshot.Probe.HasPrimaryInventory, snapshot.Probe.Notes))
                {
                    PublishInventoryDebug(CreateInventoryDebugSnapshot(
                        stage: "PrimaryInventoryMissingAllow",
                        snapshot.Probe,
                        groundItemPath: string.Empty,
                        groundItemName: snapshot.GroundItemName,
                        isStackable: false,
                        matchingPathCount: 0,
                        partialMatchingStackCount: 0,
                        hasPartialMatchingStack: false,
                        allowPickup: true));

                    return true;
                }

                if (ShouldAllowPickupWhenGroundItemEntityMissingCore(snapshot.InventoryFull, snapshot.GroundItemEntity))
                {
                    PublishInventoryDebug(CreateInventoryDebugSnapshot(
                        stage: "InventoryNotFullUnknownItemAllow",
                        snapshot.Probe,
                        snapshot.GroundItemPath,
                        snapshot.GroundItemName,
                        snapshot.IsStackable,
                        matchingPathCount: 0,
                        partialMatchingStackCount: 0,
                        hasPartialMatchingStack: false,
                        allowPickup: true));

                    return true;
                }

                if (ShouldAllowPickupWhenGroundItemIdentityMissingCore(snapshot.InventoryFull, snapshot.GroundItemPath, snapshot.GroundItemName))
                {
                    PublishInventoryDebug(CreateInventoryDebugSnapshot(
                        stage: "InventoryNotFullUnknownIdentityAllow",
                        snapshot.Probe,
                        snapshot.GroundItemPath,
                        snapshot.GroundItemName,
                        snapshot.IsStackable,
                        matchingPathCount: 0,
                        partialMatchingStackCount: 0,
                        hasPartialMatchingStack: false,
                        allowPickup: true));

                    return true;
                }

                if (!snapshot.InventoryFull)
                {
                    bool allowPickupWhenNotFull = snapshot.HasSpaceForGroundItem || (snapshot.IsStackable && snapshot.HasPartialMatchingStack);
                    string stage = allowPickupWhenNotFull ? "InventoryNotFullAllow" : "InventoryNotFullNoFit";

                    PublishInventoryDebug(CreateInventoryDebugSnapshot(
                        stage,
                        snapshot.Probe,
                        snapshot.GroundItemPath,
                        snapshot.GroundItemName,
                        snapshot.IsStackable,
                        snapshot.MatchingPathCount,
                        snapshot.PartialMatchingStackCount,
                        snapshot.HasPartialMatchingStack,
                        allowPickupWhenNotFull));

                    return allowPickupWhenNotFull;
                }

                bool allowPickup = ShouldPickupWhenInventoryFullCore(
                    inventoryFull: true,
                    snapshot.IsStackable,
                    snapshot.HasPartialMatchingStack);

                PublishInventoryDebug(CreateInventoryDebugSnapshot(
                    stage: "InventoryFullDecision",
                    snapshot.Probe,
                    snapshot.GroundItemPath,
                    snapshot.GroundItemName,
                    snapshot.IsStackable,
                    snapshot.MatchingPathCount,
                    snapshot.PartialMatchingStackCount,
                    snapshot.HasPartialMatchingStack,
                    allowPickup));

                return allowPickup;
            }
        }
    }
}