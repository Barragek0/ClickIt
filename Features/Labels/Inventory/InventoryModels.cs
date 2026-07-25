namespace ClickIt.Features.Labels.Inventory
{
    internal readonly record struct InventoryFullProbe(
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

    internal readonly record struct InventoryLayoutSnapshot(
        IReadOnlyList<InventoryLayoutEntry> Entries,
        string Source,
        string DebugDetails,
        bool IsReliable,
        int RawEntryCount)
    {
        public static readonly InventoryLayoutSnapshot Empty = new(
            Entries: [],
            Source: string.Empty,
            DebugDetails: string.Empty,
            IsReliable: false,
            RawEntryCount: 0);
    }

    internal readonly record struct InventorySnapshot(
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
            InventoryItems: []);
    }
}