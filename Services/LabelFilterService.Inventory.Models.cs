using System;
using System.Collections.Generic;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
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

        private readonly record struct InventorySnapshot(
            bool HasPrimaryInventory,
            object? PrimaryInventory,
            int CapacityCells,
            int Width,
            int Height,
            InventoryLayoutSnapshot Layout,
            int OccupiedCells,
            InventoryFullProbe FullProbe,
            IReadOnlyList<Entity> InventoryItems)
        {
            public static readonly InventorySnapshot Empty = new(
                HasPrimaryInventory: false,
                PrimaryInventory: null,
                CapacityCells: 0,
                Width: 0,
                Height: 0,
                Layout: InventoryLayoutSnapshot.Empty,
                OccupiedCells: 0,
                FullProbe: InventoryFullProbe.Empty,
                InventoryItems: Array.Empty<Entity>());
        }
    }
}