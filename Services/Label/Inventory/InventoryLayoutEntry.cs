namespace ClickIt.Services.Label.Inventory
{
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
}