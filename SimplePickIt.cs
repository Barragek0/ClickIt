using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimplePickIt
{
    public class SimplePickIt : BaseSettingsPlugin<SimplePickItSettings>
    {
        private Stopwatch Timer { get; } = new Stopwatch();
        private Random Random { get; } = new Random();
        public static SimplePickIt Controller { get; set; }
        public int[,] inventorySlots { get; set; } = new int[0, 0];
        public ServerInventory InventoryItems { get; set; }
        private TimeCache<List<CustomItem>> CachedItems { get; set; }
        private RectangleF Gamewindow;
        public override bool Initialise()
        {
            Controller = this;
            Gamewindow = GameController.Window.GetWindowRectangle();
            //CachedItems = new TimeCache<List<CustomItem>>(UpdateLabelComponent, Settings.CacheIntervall);
            Timer.Start();
            return true;
        }

        public override Job Tick()
        {
            InventoryItems = GameController.Game.IngameState.ServerData.PlayerInventories[0].Inventory;
            inventorySlots = Misc.GetContainer2DArray(InventoryItems);
            if (!Input.GetKeyState(Settings.PickUpKey.Value)) return null;
            //if (CachedItems.Value.Count < 1) return null;
            if (GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Count < 1) return null;
            if (Timer.ElapsedMilliseconds < Settings.WaitTimeInMs.Value - 10 + Random.Next(0, 20)) return null;
           
            Timer.Restart();
            PickItem();
            //return new Job("SimplePickIt", PickItem);
            return null;
        }

        private List<CustomItem> UpdateLabelComponent() =>
            GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible
            .Where(x =>
                x.ItemOnGround?.Path != null &&
                x.Label.GetClientRect().Center.PointInRectangle(new RectangleF(0, 0, Gamewindow.Width, Gamewindow.Height)) &&
                x.CanPickUp)
            .Select(x => new CustomItem(x, GameController.Files))
            .OrderBy(x => x.Distance)
            .ToList();

        private void PickItem()
        {
            if (Settings.DebugMode) LogMessage("Trying to Pick item");
            Gamewindow = GameController.Window.GetWindowRectangle();
            var nextItem = GetItemToPick();
            if (nextItem == null)
            {
                if (Settings.DebugMode) LogMessage("nextItem in PickItem() is null");
                return;
            }

            var centerOfLabel = nextItem?.Label?.GetClientRect().Center 
                + Gamewindow.TopLeft
                + new Vector2(Random.Next(0, 2), Random.Next(0, 2));
            
            if (!centerOfLabel.HasValue)
            {
                if (Settings.DebugMode) LogMessage("centerOfLabel has no Value");
                return;
            }
            Input.SetCursorPos(centerOfLabel.Value);
            Input.Click(MouseButtons.Left);
        }
        private LabelOnGround GetItemToPick()
        {
            var list = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Where(x =>
                x.ItemOnGround.DistancePlayer <= Settings.PickUpDistance &&
                x.Label.GetClientRect().Center.PointInRectangle(new RectangleF(0, 0, Gamewindow.Width, Gamewindow.Height)) &&
                Misc.CanFitInventory(new CustomItem(x, GameController.Files))).OrderBy(x => x.ItemOnGround.DistancePlayer).ToList();

            var closestValidItem = list.FirstOrDefault();
          
            if (closestValidItem == null)
            {
                if(Settings.DebugMode) LogError($"closestValidItem is null!");
                return null;
            }
            return closestValidItem;
        }
    }
}
