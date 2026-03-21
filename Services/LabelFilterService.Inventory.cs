using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        private const int InventoryProbeCacheWindowMs = 50;
        private const int InventoryDebugTrailCapacity = 32;

        private static readonly object InventoryProbeCacheLock = new();
        private static long _inventoryProbeCacheTimestampMs;
        private static GameController? _inventoryProbeCacheController;
        private static InventoryFullProbe _inventoryProbeCacheValue = InventoryFullProbe.Empty;
        private static bool _inventoryProbeCacheHasValue;

        private static long _inventoryItemsCacheTimestampMs;
        private static GameController? _inventoryItemsCacheController;
        private static IReadOnlyList<Entity> _inventoryItemsCacheValue = Array.Empty<Entity>();
        private static bool _inventoryItemsCacheHasValue;

        [ThreadStatic]
        private static HashSet<long>? _threadInventoryUniqueEntityAddresses;

        private static long _inventoryLayoutCacheTimestampMs;
        private static object? _inventoryLayoutCachePrimaryInventory;
        private static int _inventoryLayoutCacheWidth;
        private static int _inventoryLayoutCacheHeight;
        private static IReadOnlyList<InventoryLayoutEntry> _inventoryLayoutCacheEntries = Array.Empty<InventoryLayoutEntry>();
        private static string _inventoryLayoutCacheSource = string.Empty;
        private static string _inventoryLayoutCacheDebugDetails = string.Empty;
        private static bool _inventoryLayoutCacheIsReliable;
        private static int _inventoryLayoutCacheRawEntryCount;
        private static bool _inventoryLayoutCacheHasValue;

        private static readonly DebugSnapshotStore<InventoryDebugSnapshot> InventoryDebugStore = new(
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

        private readonly record struct InventoryLayoutSnapshot(
            IReadOnlyList<InventoryLayoutEntry> Entries,
            string Source,
            string DebugDetails,
            bool IsReliable,
            int RawEntryCount)
        {
            public static readonly InventoryLayoutSnapshot Empty = new(
                Entries: Array.Empty<InventoryLayoutEntry>(),
                Source: string.Empty,
                DebugDetails: string.Empty,
                IsReliable: false,
                RawEntryCount: 0);
        }

        internal readonly struct InventoryLayoutEntry
        {
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }

            public InventoryLayoutEntry(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        public static InventoryDebugSnapshot GetLatestInventoryDebug() => InventoryDebugStore.GetLatest();

        public static IReadOnlyList<string> GetLatestInventoryDebugTrail() => InventoryDebugStore.GetTrail();

        private static void PublishInventoryDebug(InventoryDebugSnapshot snapshot) => InventoryDebugStore.SetLatest(snapshot);

        private static bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
        {
            bool inventoryFull = IsInventoryFullCore(gameController, out InventoryFullProbe probe);

            if (ShouldAllowPickupWhenPrimaryInventoryMissingCore(probe.HasPrimaryInventory, probe.Notes))
            {
                PublishInventoryDebug(CreateInventoryDebugSnapshot(
                    stage: "PrimaryInventoryMissingAllow",
                    probe,
                    groundItemPath: string.Empty,
                    groundItemName: GetWorldItemBaseName(groundItem),
                    isStackable: false,
                    matchingPathCount: 0,
                    partialMatchingStackCount: 0,
                    hasPartialMatchingStack: false,
                    allowPickup: true));

                return true;
            }

            Entity? groundItemEntity = TryGetWorldItemEntity(groundItem);
            string groundItemPath = groundItemEntity?.Path ?? string.Empty;
            string groundItemName = GetWorldItemBaseName(groundItem);
            bool isStackable = IsGroundItemStackableCore(groundItemEntity);

            if (ShouldAllowPickupWhenGroundItemEntityMissingCore(inventoryFull, groundItemEntity))
            {
                PublishInventoryDebug(CreateInventoryDebugSnapshot(
                    stage: "InventoryNotFullUnknownItemAllow",
                    probe,
                    groundItemPath,
                    groundItemName,
                    isStackable,
                    matchingPathCount: 0,
                    partialMatchingStackCount: 0,
                    hasPartialMatchingStack: false,
                    allowPickup: true));

                return true;
            }

            if (ShouldAllowPickupWhenGroundItemIdentityMissingCore(inventoryFull, groundItemPath, groundItemName))
            {
                PublishInventoryDebug(CreateInventoryDebugSnapshot(
                    stage: "InventoryNotFullUnknownIdentityAllow",
                    probe,
                    groundItemPath,
                    groundItemName,
                    isStackable,
                    matchingPathCount: 0,
                    partialMatchingStackCount: 0,
                    hasPartialMatchingStack: false,
                    allowPickup: true));

                return true;
            }

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

            if (!inventoryFull)
            {
                bool allowPickupWhenNotFull = hasSpaceForGroundItem || (isStackable && hasPartialMatchingStack);
                string stage = allowPickupWhenNotFull ? "InventoryNotFullAllow" : "InventoryNotFullNoFit";

                PublishInventoryDebug(CreateInventoryDebugSnapshot(
                    stage,
                    probe,
                    groundItemPath,
                    groundItemName,
                    isStackable,
                    matchingPathCount,
                    partialMatchingStackCount,
                    hasPartialMatchingStack,
                    allowPickupWhenNotFull));

                return allowPickupWhenNotFull;
            }

            bool allowPickup = ShouldPickupWhenInventoryFullCore(
                inventoryFull: true,
                isStackable,
                hasPartialMatchingStack);

            PublishInventoryDebug(CreateInventoryDebugSnapshot(
                stage: "InventoryFullDecision",
                probe,
                groundItemPath,
                groundItemName,
                isStackable,
                matchingPathCount,
                partialMatchingStackCount,
                hasPartialMatchingStack,
                allowPickup));

            return allowPickup;
        }

        private static InventoryDebugSnapshot CreateInventoryDebugSnapshot(
            string stage,
            InventoryFullProbe probe,
            string groundItemPath,
            string groundItemName,
            bool isStackable,
            int matchingPathCount,
            int partialMatchingStackCount,
            bool hasPartialMatchingStack,
            bool allowPickup)
        {
            return new InventoryDebugSnapshot(
                HasData: true,
                Stage: stage,
                InventoryFull: probe.IsFull,
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
                TimestampMs: Environment.TickCount64);
        }

        internal static bool ShouldPickupWhenInventoryFullCore(bool inventoryFull, bool isStackable, bool hasPartialMatchingStack)
            => !inventoryFull || (isStackable && hasPartialMatchingStack);

        internal static bool IsPartialStackCore(int currentStackSize, int maxStackSize)
            => currentStackSize > 0 && maxStackSize > 0 && currentStackSize < maxStackSize;

        internal static bool IsPartialServerStackCore(bool fullStack, int size)
            => size > 0 && !fullStack;

        internal static bool IsInventoryCellUsageFullCore(int occupiedCellCount, int totalCellCapacity)
            => totalCellCapacity > 0 && occupiedCellCount >= totalCellCapacity;

        internal static bool ShouldAllowPickupWhenPrimaryInventoryMissingCore(bool hasPrimaryInventory, string notes)
            => !hasPrimaryInventory && notes == "Primary server inventory missing";

        internal static bool ShouldAllowPickupWhenGroundItemEntityMissingCore(bool inventoryFull, Entity? groundItemEntity)
            => !inventoryFull && groundItemEntity == null;

        internal static bool ShouldAllowPickupWhenGroundItemIdentityMissingCore(bool inventoryFull, string? groundItemPath, string? groundItemName)
            => !inventoryFull
                && string.IsNullOrWhiteSpace(groundItemPath)
                && string.IsNullOrWhiteSpace(groundItemName);

        private static bool IsInventoryFullCore(GameController? gameController, out InventoryFullProbe probe)
        {
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryProbe(gameController, now, out InventoryFullProbe cachedProbe))
            {
                probe = cachedProbe;
                return probe.IsFull;
            }

            if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
            {
                probe = InventoryFullProbe.Empty with { Notes = "Primary server inventory missing" };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (TryReadInventoryFullFlag(primaryInventory, out bool fullFlagValue, out string fullFlagSource))
            {
                probe = CreateInventoryFullFlagProbe(fullFlagValue, fullFlagSource);
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (!TryResolveInventoryCapacity(primaryInventory, out int totalCellCapacity))
            {
                probe = InventoryFullProbe.Empty with { HasPrimaryInventory = true, Notes = "Unable to resolve inventory capacity" };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (!TryResolveInventoryDimensions(primaryInventory, out int inventoryWidth, out int inventoryHeight))
            {
                probe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    Notes = "Unable to resolve inventory dimensions"
                };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (!TryResolveInventoryLayoutEntries(
                primaryInventory,
                inventoryWidth,
                inventoryHeight,
                out IReadOnlyList<InventoryLayoutEntry> layoutEntries,
                out string layoutSource,
                out string layoutDebug,
                out bool layoutReliable,
                out int layoutRawCount))
            {
                probe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    Notes = $"Unable to resolve inventory layout entries from {layoutSource} ({layoutDebug})"
                };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (!layoutReliable)
            {
                probe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    InventoryEntityCount = layoutRawCount,
                    LayoutEntryCount = layoutEntries.Count,
                    Notes = $"Inventory layout unreliable from {layoutSource} ({layoutDebug})"
                };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            if (!TryResolveOccupiedInventoryCellsFromLayout(layoutEntries, inventoryWidth, inventoryHeight, out int occupiedCellCount))
            {
                probe = InventoryFullProbe.Empty with
                {
                    HasPrimaryInventory = true,
                    CapacityCells = totalCellCapacity,
                    InventoryEntityCount = layoutRawCount,
                    LayoutEntryCount = layoutEntries.Count,
                    Notes = $"Unable to resolve occupied cells from {layoutSource}"
                };
                SetCachedInventoryProbe(gameController, now, probe);
                return probe.IsFull;
            }

            bool isFull = IsInventoryCellUsageFullCore(occupiedCellCount, totalCellCapacity);
            probe = new InventoryFullProbe(
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: totalCellCapacity,
                OccupiedCells: occupiedCellCount,
                InventoryEntityCount: layoutRawCount,
                LayoutEntryCount: layoutEntries.Count,
                IsFull: isFull,
                Source: "CellOccupancy",
                Notes: $"Inventory fullness from {layoutSource} footprint ({layoutDebug})");

            SetCachedInventoryProbe(gameController, now, probe);
            return probe.IsFull;
        }

        private static bool HasInventorySpaceForGroundItemCore(Entity? groundItemEntity, GameController? gameController)
        {
            if (groundItemEntity == null)
                return false;

            if (!TryResolveInventoryItemSize(groundItemEntity, out int requiredWidth, out int requiredHeight))
                return false;

            if (requiredWidth <= 0 || requiredHeight <= 0)
                return false;

            if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
                return false;

            if (!TryResolveInventoryDimensions(primaryInventory, out int inventoryWidth, out int inventoryHeight))
                return false;

            if (!TryResolveInventoryLayoutEntries(
                primaryInventory,
                inventoryWidth,
                inventoryHeight,
                out IReadOnlyList<InventoryLayoutEntry> layoutEntries,
                out _,
                out _,
                out bool layoutReliable,
                out _))
            {
                return false;
            }

            if (!layoutReliable)
                return false;

            return HasSpaceForItemFootprintCore(inventoryWidth, inventoryHeight, layoutEntries, requiredWidth, requiredHeight);
        }

        private static bool TryResolveInventoryDimensions(object primaryInventory, out int width, out int height)
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

        private static bool TryResolveInventoryLayoutEntries(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight,
            out IReadOnlyList<InventoryLayoutEntry> entries)
            => TryResolveInventoryLayoutEntries(primaryInventory, inventoryWidth, inventoryHeight, out entries, out _);

        private static bool TryResolveInventoryLayoutEntries(
            object primaryInventory,
            int inventoryWidth,
            int inventoryHeight,
            out IReadOnlyList<InventoryLayoutEntry> entries,
            out string source)
            => TryResolveInventoryLayoutEntries(primaryInventory, inventoryWidth, inventoryHeight, out entries, out source, out _, out _, out _);

        private static bool TryResolveInventoryLayoutEntries(
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

        private static bool TryBuildInventoryLayoutEntriesFromCollection(
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

        private static bool TryResolveOccupiedInventoryCellsFromLayout(
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

        private static Entity? TryGetInventoryItemEntityFromEntry(object entry)
        {
            if (entry is Entity directEntity)
                return directEntity;

            if (TryGetDynamicValue(entry, s => s.ItemEntity, out object? nestedItemObj)
                && nestedItemObj is Entity nestedItemEntity)
                return nestedItemEntity;

            if (TryGetDynamicValue(entry, s => s.Item, out object? itemObj)
                && itemObj is Entity itemEntity)
                return itemEntity;

            if (TryGetDynamicValue(entry, s => s.Entity, out object? entityObj)
                && entityObj is Entity entityFromSlot)
                return entityFromSlot;

            return null;
        }

        private static bool TryResolveInventoryItemPosition(object entry, Entity? itemEntity, out int x, out int y)
        {
            if (TryReadInventoryCoordinates(entry, out x, out y))
                return true;

            if (itemEntity != null && TryReadInventoryCoordinates(itemEntity, out x, out y))
                return true;

            x = 0;
            y = 0;
            return false;
        }

        private static bool TryResolveInventoryEntrySize(object entry, Entity? itemEntity, out int width, out int height)
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

        private static bool TryReadInventoryCoordinates(object source, out int x, out int y)
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

        private static bool TryReadCoordinatePair(object source, string xFieldName, string yFieldName, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (!TryReadIntByName(source, xFieldName, out x))
                return false;

            if (!TryReadIntByName(source, yFieldName, out y))
                return false;

            return x >= 0 && y >= 0;
        }

        private static bool TryReadIntByName(object source, string memberName, out int value)
        {
            switch (memberName)
            {
                case "PosX": return TryReadInt(source, out value, s => s.PosX);
                case "PosY": return TryReadInt(source, out value, s => s.PosY);
                case "InventoryX": return TryReadInt(source, out value, s => s.InventoryX);
                case "InventoryY": return TryReadInt(source, out value, s => s.InventoryY);
                case "ItemCellX": return TryReadInt(source, out value, s => s.ItemCellX);
                case "ItemCellY": return TryReadInt(source, out value, s => s.ItemCellY);
                case "CellX": return TryReadInt(source, out value, s => s.CellX);
                case "CellY": return TryReadInt(source, out value, s => s.CellY);
                case "PositionX": return TryReadInt(source, out value, s => s.PositionX);
                case "PositionY": return TryReadInt(source, out value, s => s.PositionY);
                case "X": return TryReadInt(source, out value, s => s.X);
                case "Y": return TryReadInt(source, out value, s => s.Y);
                case "Column": return TryReadInt(source, out value, s => s.Column);
                case "Row": return TryReadInt(source, out value, s => s.Row);
                case "SizeX": return TryReadInt(source, out value, s => s.SizeX);
                case "SizeY": return TryReadInt(source, out value, s => s.SizeY);
                default:
                    value = 0;
                    return false;
            }
        }

        internal static bool HasSpaceForItemFootprintCore(
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

        private static bool TryReadInventoryFullFlag(object primaryInventory, out bool full, out string source)
        {
            full = false;
            source = string.Empty;

            if (TryReadBool(primaryInventory, out full, s => s.IsFull))
            {
                source = "IsFull";
                return true;
            }

            if (TryReadBool(primaryInventory, out full, s => s.Full))
            {
                source = "Full";
                return true;
            }

            if (TryReadBool(primaryInventory, out full, s => s.InventoryFull))
            {
                source = "InventoryFull";
                return true;
            }

            return false;
        }

        private static InventoryFullProbe CreateInventoryFullFlagProbe(bool full, string source)
        {
            return new InventoryFullProbe(
                HasPrimaryInventory: true,
                UsedFullFlag: true,
                FullFlagValue: full,
                UsedCellOccupancy: false,
                CapacityCells: 0,
                OccupiedCells: 0,
                InventoryEntityCount: 0,
                LayoutEntryCount: 0,
                IsFull: full,
                Source: source,
                Notes: $"Inventory fullness from server flag {source}");
        }

        private static bool TryResolveInventoryItemSize(Entity itemEntity, out int width, out int height)
        {
            width = 1;
            height = 1;

            try
            {
                Base? baseComponent = itemEntity.GetComponent<Base>();
                return TryResolveInventoryItemSizeFromBase(baseComponent, out width, out height);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryResolveInventoryItemSizeFromBase(object? baseComponent, out int width, out int height)
        {
            width = 1;
            height = 1;

            if (baseComponent == null)
                return false;

            if (TryResolveInventoryItemCellSizeFromInfo(baseComponent, out width, out height))
            {
                width = Math.Max(1, width);
                height = Math.Max(1, height);
                return true;
            }

            if (!TryReadInt(baseComponent, out width, s => s.ItemCellsSizeX)
                || !TryReadInt(baseComponent, out height, s => s.ItemCellsSizeY))
            {
                return false;
            }

            width = Math.Max(1, width);
            height = Math.Max(1, height);
            return true;
        }

        private static bool TryResolveInventoryItemCellSizeFromInfo(object baseComponent, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (!TryGetDynamicValue(baseComponent, s => s.Info, out object? info) || info == null)
                return false;

            if (!TryReadInt(info, out width, s => s.ItemCellsSizeX)
                || !TryReadInt(info, out height, s => s.ItemCellsSizeY))
            {
                return false;
            }

            return width > 0 && height > 0;
        }

        private static bool IsGroundItemStackableCore(Entity? itemEntity)
            => itemEntity != null && TryResolveServerStackState(itemEntity, out _, out _);

        private static bool HasMatchingPartialStackInInventoryCore(
            string? worldItemPath,
            Entity? groundItemEntity,
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

            bool requiresIncubatorLevelMatch = IsIncubatorPath(worldItemPath);
            bool hasGroundIncubatorLevel = TryResolveCurrencyItemLevel(groundItemEntity, out int groundIncubatorLevel);

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Entity inventoryItem = inventoryItems[i];
                if (inventoryItem == null)
                    continue;

                string inventoryPath = inventoryItem.Path ?? string.Empty;
                if (!inventoryPath.Equals(worldItemPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                matchingPathCount++;

                bool hasInventoryIncubatorLevel = TryResolveCurrencyItemLevel(inventoryItem, out int inventoryIncubatorLevel);
                if (!ShouldAllowIncubatorStackMatchCore(
                    requiresIncubatorLevelMatch,
                    hasGroundIncubatorLevel,
                    groundIncubatorLevel,
                    hasInventoryIncubatorLevel,
                    inventoryIncubatorLevel))
                {
                    continue;
                }

                if (TryResolveServerStackState(inventoryItem, out bool fullStack, out int stackSize)
                    && IsPartialServerStackCore(fullStack, stackSize))
                {
                    partialMatchingStackCount++;
                    return true;
                }
            }

            return false;
        }

        private static bool IsIncubatorPath(string? metadataPath)
            => !string.IsNullOrWhiteSpace(metadataPath)
               && metadataPath.IndexOf("Incubation", StringComparison.OrdinalIgnoreCase) >= 0;

        internal static bool ShouldAllowIncubatorStackMatchCore(
            bool requiresIncubatorLevelMatch,
            bool hasGroundIncubatorLevel,
            int groundIncubatorLevel,
            bool hasInventoryIncubatorLevel,
            int inventoryIncubatorLevel)
        {
            if (!requiresIncubatorLevelMatch)
                return true;

            if (!hasGroundIncubatorLevel || !hasInventoryIncubatorLevel)
                return false;

            return groundIncubatorLevel == inventoryIncubatorLevel;
        }

        private static bool TryResolveCurrencyItemLevel(Entity? itemEntity, out int currencyItemLevel)
        {
            currencyItemLevel = 0;
            if (itemEntity == null)
                return false;

            try
            {
                Base? baseComponent = itemEntity.GetComponent<Base>();
                if (baseComponent == null)
                    return false;

                if (TryReadInt(baseComponent, out currencyItemLevel, s => s.CurrencyItemLevel))
                    return currencyItemLevel > 0;

                currencyItemLevel = baseComponent.CurrencyItemLevel;
                return currencyItemLevel > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveServerStackState(Entity itemEntity, out bool fullStack, out int stackSize)
        {
            fullStack = false;
            stackSize = 0;

            try
            {
                object? stack = itemEntity.GetComponent<Stack>();
                bool hasFullFlag = TryReadStackFullFlag(stack, out fullStack);
                bool hasSize = TryReadStackSize(stack, out stackSize);
                return hasFullFlag && hasSize && stackSize > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadStackFullFlag(object? stack, out bool fullStack)
            => TryReadBool(stack, out fullStack, s => s.FullStack)
               || TryReadBool(stack, out fullStack, s => s.IsFull)
               || TryReadBool(stack, out fullStack, s => s.Full);

        private static bool TryReadStackSize(object? stack, out int stackSize)
            => TryReadInt(stack, out stackSize, s => s.Size)
               || TryReadInt(stack, out stackSize, s => s.Count)
               || TryReadInt(stack, out stackSize, s => s.StackSize)
               || TryReadInt(stack, out stackSize, s => s.Amount);

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
            long now = Environment.TickCount64;
            if (TryGetCachedInventoryItems(gameController, now, out IReadOnlyList<Entity> cachedItems))
            {
                items = cachedItems;
                return items.Count > 0;
            }

            items = Array.Empty<Entity>();
            if (!TryGetPrimaryServerInventory(gameController, out object? primaryInventory) || primaryInventory == null)
            {
                SetCachedInventoryItems(gameController, now, items);
                return false;
            }

            if (!TryEnumeratePrimaryInventoryItemEntitiesFast(primaryInventory, out IReadOnlyList<Entity> entities))
            {
                SetCachedInventoryItems(gameController, now, items);
                return false;
            }

            items = entities;
            SetCachedInventoryItems(gameController, now, items);
            return items.Count > 0;
        }

        private static bool TryEnumeratePrimaryInventoryItemEntitiesFast(object primaryInventory, out IReadOnlyList<Entity> items)
        {
            items = Array.Empty<Entity>();

            if (!TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? collectionObj) || collectionObj == null)
                return false;

            HashSet<long> uniqueAddresses = GetThreadInventoryUniqueEntityAddressSet();
            uniqueAddresses.Clear();

            var uniqueEntities = new List<Entity>(32);

            foreach (object? entry in EnumerateObjects(collectionObj))
            {
                if (entry == null)
                    continue;

                Entity? entity = TryGetInventoryItemEntityFromEntry(entry);
                if (entity == null || !IsInventoryItemEntity(entity, out _))
                    continue;

                AddUniqueInventoryEntity(entity, uniqueAddresses, uniqueEntities);
            }

            if (uniqueEntities.Count == 0)
                return false;

            items = uniqueEntities;
            return true;
        }

        private static bool TryEnumeratePrimaryInventoryItemEntities(object primaryInventory, out IReadOnlyList<Entity> items, out string debugDetails)
        {
            items = Array.Empty<Entity>();
            debugDetails = string.Empty;

            if (!TryGetPrimaryServerInventorySlotItems(primaryInventory, out object? collectionObj, out string collectionDebug) || collectionObj == null)
            {
                debugDetails = $"items-collection: {collectionDebug}";
                return false;
            }

            int totalEntries = 0;
            int nullEntries = 0;
            int extractedEntityEntries = 0;

            HashSet<long> uniqueAddresses = GetThreadInventoryUniqueEntityAddressSet();
            uniqueAddresses.Clear();
            var uniqueEntities = new List<Entity>(32);

            foreach (object? entry in EnumerateObjects(collectionObj))
            {
                totalEntries++;
                if (entry == null)
                {
                    nullEntries++;
                    continue;
                }

                Entity? entity = TryGetInventoryItemEntityFromEntry(entry);
                if (entity == null || !IsInventoryItemEntity(entity, out _))
                    continue;

                extractedEntityEntries++;
                AddUniqueInventoryEntity(entity, uniqueAddresses, uniqueEntities);
            }

            if (uniqueEntities.Count == 0)
            {
                debugDetails = $"{collectionDebug}; entries:{totalEntries} null:{nullEntries} extracted:{extractedEntityEntries}";
                return false;
            }

            items = uniqueEntities;
            debugDetails = $"{collectionDebug}; entries:{totalEntries} null:{nullEntries} extracted:{extractedEntityEntries} dedup:{extractedEntityEntries}->{items.Count}";
            return true;
        }

        private static HashSet<long> GetThreadInventoryUniqueEntityAddressSet()
        {
            HashSet<long>? addresses = _threadInventoryUniqueEntityAddresses;
            if (addresses != null)
                return addresses;

            addresses = new HashSet<long>();
            _threadInventoryUniqueEntityAddresses = addresses;
            return addresses;
        }

        private static void AddUniqueInventoryEntity(Entity entity, HashSet<long> uniqueAddresses, List<Entity> uniqueEntities)
        {
            if (entity == null)
                return;

            long address = entity.Address;
            if (address == 0 || !uniqueAddresses.Add(address))
                return;

            uniqueEntities.Add(entity);
        }

        private static bool TryGetPrimaryServerInventory(GameController? gameController, out object? primaryInventory)
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

        private static bool TryGetPrimaryServerInventorySlotItems(object primaryInventory, out object? slotItemsCollection, out string debugDetails)
        {
            slotItemsCollection = null;
            debugDetails = string.Empty;

            if (!TryGetDynamicValue(primaryInventory, s => s.Inventory, out object? inventoryObj) || inventoryObj == null)
            {
                debugDetails = "read-failed: PlayerInventories[0].Inventory accessor unavailable";
                return false;
            }

            if (!TryGetDynamicValue(inventoryObj, s => s.InventorySlotItems, out slotItemsCollection))
            {
                debugDetails = "read-failed: PlayerInventories[0].Inventory.InventorySlotItems accessor unavailable";
                return false;
            }

            if (slotItemsCollection == null)
            {
                debugDetails = "read-ok: PlayerInventories[0].Inventory.InventorySlotItems is null";
                return false;
            }

            int previewCount = CountPreviewObjects(slotItemsCollection, 8);
            debugDetails = $"read-ok: PlayerInventories[0].Inventory.InventorySlotItems type={slotItemsCollection.GetType().Name} previewCount={previewCount}";
            return true;
        }

        private static bool TryGetPrimaryServerInventorySlotItems(object primaryInventory, out object? slotItemsCollection)
        {
            slotItemsCollection = null;

            if (!TryGetDynamicValue(primaryInventory, s => s.Inventory, out object? inventoryObj) || inventoryObj == null)
                return false;

            if (!TryGetDynamicValue(inventoryObj, s => s.InventorySlotItems, out slotItemsCollection))
                return false;

            return slotItemsCollection != null;
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
            reason = isItem ? "path-item" : "path-non-item";
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

        private static int CountPreviewObjects(object? source, int maxCount)
        {
            if (maxCount <= 0)
                return 0;

            int count = 0;
            foreach (object? _ in EnumerateObjects(source))
            {
                count++;
                if (count >= maxCount)
                    break;
            }

            return count;
        }

        private static bool TryGetCachedInventoryProbe(GameController? gameController, long now, out InventoryFullProbe probe)
        {
            lock (InventoryProbeCacheLock)
            {
                if (_inventoryProbeCacheHasValue
                    && ReferenceEquals(_inventoryProbeCacheController, gameController)
                    && IsCacheFresh(now, _inventoryProbeCacheTimestampMs, InventoryProbeCacheWindowMs))
                {
                    probe = _inventoryProbeCacheValue;
                    return true;
                }
            }

            probe = InventoryFullProbe.Empty;
            return false;
        }

        private static void SetCachedInventoryProbe(GameController? gameController, long now, InventoryFullProbe probe)
        {
            lock (InventoryProbeCacheLock)
            {
                _inventoryProbeCacheController = gameController;
                _inventoryProbeCacheTimestampMs = now;
                _inventoryProbeCacheValue = probe;
                _inventoryProbeCacheHasValue = true;
            }
        }

        private static bool TryGetCachedInventoryItems(GameController? gameController, long now, out IReadOnlyList<Entity> items)
        {
            lock (InventoryProbeCacheLock)
            {
                if (_inventoryItemsCacheHasValue
                    && ReferenceEquals(_inventoryItemsCacheController, gameController)
                    && IsCacheFresh(now, _inventoryItemsCacheTimestampMs, InventoryProbeCacheWindowMs))
                {
                    items = _inventoryItemsCacheValue;
                    return true;
                }
            }

            items = Array.Empty<Entity>();
            return false;
        }

        private static bool TryGetCachedInventoryLayout(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            out InventoryLayoutSnapshot snapshot)
        {
            lock (InventoryProbeCacheLock)
            {
                if (_inventoryLayoutCacheHasValue
                    && ReferenceEquals(_inventoryLayoutCachePrimaryInventory, primaryInventory)
                    && _inventoryLayoutCacheWidth == inventoryWidth
                    && _inventoryLayoutCacheHeight == inventoryHeight
                    && IsCacheFresh(now, _inventoryLayoutCacheTimestampMs, InventoryProbeCacheWindowMs))
                {
                    snapshot = new InventoryLayoutSnapshot(
                        Entries: _inventoryLayoutCacheEntries,
                        Source: _inventoryLayoutCacheSource,
                        DebugDetails: _inventoryLayoutCacheDebugDetails,
                        IsReliable: _inventoryLayoutCacheIsReliable,
                        RawEntryCount: _inventoryLayoutCacheRawEntryCount);
                    return true;
                }
            }

            snapshot = InventoryLayoutSnapshot.Empty;
            return false;
        }

        private static void SetCachedInventoryLayout(
            object primaryInventory,
            long now,
            int inventoryWidth,
            int inventoryHeight,
            InventoryLayoutSnapshot snapshot)
        {
            lock (InventoryProbeCacheLock)
            {
                _inventoryLayoutCachePrimaryInventory = primaryInventory;
                _inventoryLayoutCacheTimestampMs = now;
                _inventoryLayoutCacheWidth = inventoryWidth;
                _inventoryLayoutCacheHeight = inventoryHeight;
                _inventoryLayoutCacheEntries = snapshot.Entries ?? Array.Empty<InventoryLayoutEntry>();
                _inventoryLayoutCacheSource = snapshot.Source ?? string.Empty;
                _inventoryLayoutCacheDebugDetails = snapshot.DebugDetails ?? string.Empty;
                _inventoryLayoutCacheIsReliable = snapshot.IsReliable;
                _inventoryLayoutCacheRawEntryCount = snapshot.RawEntryCount;
                _inventoryLayoutCacheHasValue = true;
            }
        }

        private static void SetCachedInventoryItems(GameController? gameController, long now, IReadOnlyList<Entity> items)
        {
            lock (InventoryProbeCacheLock)
            {
                _inventoryItemsCacheController = gameController;
                _inventoryItemsCacheTimestampMs = now;
                _inventoryItemsCacheValue = items;
                _inventoryItemsCacheHasValue = true;
            }
        }

        private static bool IsCacheFresh(long now, long cachedAtMs, int windowMs)
        {
            if (cachedAtMs <= 0 || windowMs <= 0)
                return false;

            long age = now - cachedAtMs;
            return age >= 0 && age <= windowMs;
        }

        internal static void ClearInventoryProbeCacheForShutdown()
        {
            lock (InventoryProbeCacheLock)
            {
                _inventoryProbeCacheTimestampMs = 0;
                _inventoryProbeCacheController = null;
                _inventoryProbeCacheValue = InventoryFullProbe.Empty;
                _inventoryProbeCacheHasValue = false;

                _inventoryItemsCacheTimestampMs = 0;
                _inventoryItemsCacheController = null;
                _inventoryItemsCacheValue = Array.Empty<Entity>();
                _inventoryItemsCacheHasValue = false;

                _inventoryLayoutCacheTimestampMs = 0;
                _inventoryLayoutCachePrimaryInventory = null;
                _inventoryLayoutCacheWidth = 0;
                _inventoryLayoutCacheHeight = 0;
                _inventoryLayoutCacheEntries = Array.Empty<InventoryLayoutEntry>();
                _inventoryLayoutCacheSource = string.Empty;
                _inventoryLayoutCacheDebugDetails = string.Empty;
                _inventoryLayoutCacheIsReliable = false;
                _inventoryLayoutCacheRawEntryCount = 0;
                _inventoryLayoutCacheHasValue = false;

                _threadInventoryUniqueEntityAddresses = null;
            }
        }

        private static bool TryReadBool(object? source, out bool value, Func<dynamic, object?> accessor)
            => DynamicAccess.TryReadBool(source, accessor, out value);

        private static bool TryReadInt(object? source, out int value, Func<dynamic, object?> accessor)
            => DynamicAccess.TryReadInt(source, accessor, out value);

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicAccess.TryGetDynamicValue(source, accessor, out value);
    }
}