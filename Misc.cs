using System;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;

namespace SimplePickIt
{
    public class Misc
    {

        public static bool CanFitInventory(CustomItem groundItem)
        {
            return FindSpotInventory(groundItem) != new Vector2(-1, -1);
        }

        /* Container.FindSpot(item)
         *	Finds a spot available in the buffer to place the item.
         */
        public static Vector2 FindSpotInventory(CustomItem item)
        {
            var location = new Vector2(-1, -1);
            var InventorySlots = SimplePickIt.Controller.inventorySlots;
            var inventoryItems = SimplePickIt.Controller.InventoryItems.InventorySlotItems;
            var width = 12;
            var height = 5;

            if (InventorySlots == null)
                return location;

            for (var yCol = 0; yCol < height - (item.Height - 1); yCol++)
            for (var xRow = 0; xRow < width - (item.Width - 1); xRow++)
            {
                var success = 0;

                for (var xWidth = 0; xWidth < item.Width; xWidth++)
                for (var yHeight = 0; yHeight < item.Height; yHeight++)
                    if (InventorySlots[yCol + yHeight, xRow + xWidth] == 0)
                        success++;
                    else if (inventoryItems.Any(x =>
                        x.PosX == xRow && x.PosY == yCol && CanItemBeStacked(item, x) == StackableItem.Can))
                        success++;

                if (success >= item.Height * item.Width) return new Vector2(xRow, yCol);
            }

            return location;
        }

        public static StackableItem CanItemBeStacked(CustomItem item, ServerInventory.InventSlotItem inventoryItem)
        {
            // return false if not the same item
            if (item.GroundItem.Path != inventoryItem.Item.Path)
                return StackableItem.Cannot;

            // return false if the items dont have the Stack component
            // probably only need to do it on one item but for smoll brain reasons...here we go
            if (!item.GroundItem.HasComponent<Stack>() || !inventoryItem.Item.HasComponent<Stack>())
                return StackableItem.Cannot;

            var itemStackComp = item.GroundItem.GetComponent<Stack>();
            var inventoryItemStackComp = inventoryItem.Item.GetComponent<Stack>();

            if (inventoryItemStackComp.Size == inventoryItemStackComp.Info.MaxStackSize)
                return StackableItem.Cannot;

            return StackableItem.Can;
        }

        public enum StackableItem
        {
            Cannot,
            Can
        }

        public static int[,] GetContainer2DArray(ServerInventory containerItems)
        {
            var containerCells = new int[containerItems.Rows, containerItems.Columns];

            try
            {
                foreach (var item in containerItems.InventorySlotItems)
                {
                    var itemSizeX = item.SizeX;
                    var itemSizeY = item.SizeY;
                    var inventPosX = item.PosX;
                    var inventPosY = item.PosY;
                    for (var y = 0; y < itemSizeY; y++)
                    for (var x = 0; x < itemSizeX; x++)
                        containerCells[y + inventPosY, x + inventPosX] = 1;
                }

                return containerCells;
            }
            catch (Exception e)
            {
                // ignored
                SimplePickIt.Controller.LogMessage(e.ToString(), 5);
            }

            return containerCells;
        }
    }
}
