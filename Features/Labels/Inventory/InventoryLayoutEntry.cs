namespace ClickIt.Features.Labels.Inventory
{
    internal readonly struct InventoryLayoutEntry(int x, int y, int width, int height)
    {
        public int X { get; } = x;
        public int Y { get; } = y;
        public int Width { get; } = width;
        public int Height { get; } = height;
    }
}