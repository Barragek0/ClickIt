using ExileCore;
using ExileCore.PoEMemory.Components;
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
using System.Windows.Forms;

namespace ClickIt
{
    public class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        private Stopwatch Timer { get; } = new Stopwatch();
        private Random Random { get; } = new Random();
        public static ClickIt Controller { get; set; }
        public int[,] inventorySlots { get; set; } = new int[0, 0];
        public ServerInventory InventoryItems { get; set; }
        private TimeCache<List<LabelOnGround>> CachedLabels { get; set; }
        private RectangleF Gamewindow;
        public override bool Initialise()
        {
            Controller = this;
            Gamewindow = GameController.Window.GetWindowRectangle();
            Settings.ReloadPluginButton.OnPressed += () => { ToggleCaching(); };

            if (Settings.CachingEnable)
            {
                CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, Settings.CacheInterval);
            }
            
            Timer.Start();
            return true;
        }
        private void ToggleCaching()
        {
            if(Settings.CachingEnable.Value && CachedLabels == null)
            {
                CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, Settings.CacheInterval);
            }
            else
            {
                CachedLabels = null;
            }
        }

        public override Job Tick()
        {
            InventoryItems = GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
            inventorySlots = Misc.GetContainer2DArray(InventoryItems);
            if (!Input.GetKeyState(Settings.ClickLabelKey.Value))
                return null;
            //if (GameController.IngameState.IngameUi.ChatTitlePanel.IsVisible) return null; // this has been removed or renamed? can't find the new reference for it
            if (Settings.BlockOnOpenLeftPanel && GameController.IngameState.IngameUi.OpenLeftPanel.Address != 0)
            {
                LogMessage("OpenLeftPanel is open, not clicking");
                return null;
            }
            if (GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Count < 1)
            {
                LogMessage("Items on ground less than 1, not clicking");
                return null;
            }
            if (Timer.ElapsedMilliseconds < Settings.WaitTimeInMs.Value - 10 + Random.Next(0, 20)) return null;
            if (GameController.Area.CurrentArea.IsHideout || GameController.Area.CurrentArea.IsTown)
            {
                LogMessage("In hideout or town, not clicking");
                return null;
            }

            Timer.Restart();
            ClickLabel();
            return null;
        }

        private List<LabelOnGround> UpdateLabelComponent() =>
            GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible
            .Where(x =>
                x.ItemOnGround?.Path != null &&
                x.Label.GetClientRect().Center.PointInRectangle(new RectangleF(0, 0, Gamewindow.Width, Gamewindow.Height)) &&
                (x.ItemOnGround.Type == EntityType.WorldItem || x.ItemOnGround.Type == EntityType.Shrine ||
                x.ItemOnGround.Type == EntityType.Chest && !x.ItemOnGround.GetComponent<Chest>().OpenOnDamage ||
                x.ItemOnGround.Type == EntityType.AreaTransition))
            .OrderBy(x => x.ItemOnGround.DistancePlayer)
            .ToList();

        private void ClickLabel()
        {
            if (Settings.DebugMode) LogMessage("Trying to Click Label");
            Gamewindow = GameController.Window.GetWindowRectangle();

            LabelOnGround nextLabel;
            if (Settings.CachingEnable) nextLabel = GetLabelCaching();
            else nextLabel = GetLabelNoCaching();
            
            if (nextLabel == null)
            {
                if (Settings.DebugMode) LogMessage("nextLabel in ClickLabel() is null");
                return;
            }

            var centerOfLabel = nextLabel?.Label?.GetClientRect().Center 
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
        private LabelOnGround GetLabelCaching()
        {
            var label = CachedLabels.Value.Find(x => x.ItemOnGround.DistancePlayer <= Settings.ClickDistance && 
                (Settings.ClickItems.Value && 
                x.ItemOnGround.Type == EntityType.WorldItem && 
                (!Settings.IgnoreUniques || x.ItemOnGround.GetComponent<WorldItem>()?.ItemEntity.GetComponent<Mods>()?.ItemRarity != ItemRarity.Unique || 
                x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path.StartsWith("Metadata/Items/Metamorphosis/Metamorphosis")) ||
                Settings.ClickChests.Value && x.ItemOnGround.Type == EntityType.Chest ||
                Settings.ClickAreaTransitions.Value && x.ItemOnGround.Type == EntityType.AreaTransition ||
                Settings.ClickShrines.Value && x.ItemOnGround.Type == EntityType.Shrine));
            return label;
        }
        private LabelOnGround GetLabelNoCaching()
        {
            var list = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Where(x =>
                x.ItemOnGround?.Path != null &&
                x.Label.GetClientRect().Center.PointInRectangle(new RectangleF(0, 0, Gamewindow.Width, Gamewindow.Height)) &&
                (x.ItemOnGround.Type == EntityType.WorldItem || x.ItemOnGround.Type == EntityType.Shrine ||
                x.ItemOnGround.Type == EntityType.Chest && !x.ItemOnGround.GetComponent<Chest>().OpenOnDamage ||
                x.ItemOnGround.Type == EntityType.AreaTransition))
            .OrderBy(x => x.ItemOnGround.DistancePlayer).ToList();


            return list.Find(x => x.ItemOnGround.DistancePlayer <= Settings.ClickDistance &&
                (Settings.ClickItems.Value &&
                x.ItemOnGround.Type == EntityType.WorldItem &&
                (!Settings.IgnoreUniques || x.ItemOnGround.GetComponent<WorldItem>()?.ItemEntity.GetComponent<Mods>()?.ItemRarity != ItemRarity.Unique ||
                x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path.StartsWith("Metadata/Items/Metamorphosis/Metamorphosis")) ||
                Settings.ClickChests.Value && x.ItemOnGround.Type == EntityType.Chest ||
                Settings.ClickAreaTransitions.Value && x.ItemOnGround.Type == EntityType.AreaTransition ||
                Settings.ClickShrines.Value && x.ItemOnGround.Type == EntityType.Shrine));
        }
    }
}
