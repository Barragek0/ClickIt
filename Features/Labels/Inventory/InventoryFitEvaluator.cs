using System.Buffers;

namespace ClickIt.Features.Labels.Inventory
{
    internal static class InventoryFitEvaluator
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
            byte[]? rentedBuffer = null;
            Span<byte> occupied = totalCells <= 256
                ? stackalloc byte[totalCells]
                : rentedBuffer = ArrayPool<byte>.Shared.Rent(totalCells);
            occupied = occupied[..totalCells];
            occupied.Clear();

            try
            {
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
            finally
            {
                if (rentedBuffer != null)
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
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
            byte[]? rentedBuffer = null;
            Span<byte> occupied = totalCells <= 256
                ? stackalloc byte[totalCells]
                : rentedBuffer = ArrayPool<byte>.Shared.Rent(totalCells);
            occupied = occupied[..totalCells];
            occupied.Clear();

            try
            {
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
            finally
            {
                if (rentedBuffer != null)
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
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
}