using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hwnd, StringBuilder ss, int count);
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
            if (ActiveWindowTitle().IndexOf("Path of Exile", 0, StringComparison.CurrentCultureIgnoreCase) == -1)
            {
                LogMessage("Path of exile window not active, not clicking");
                return null;
            }
            //if (GameController.IngameState.IngameUi.ChatTitlePanel.IsVisible) return null; // this has been removed or renamed? can't find the new reference for it
            if (Settings.BlockOnOpenLeftRightPanel && GameController.IngameState.IngameUi.OpenLeftPanel.Address != 0)
            {
                LogMessage("OpenLeftPanel is open, not clicking");
                return null;
            }
            if (Settings.BlockOnOpenLeftRightPanel && GameController.IngameState.IngameUi.OpenRightPanel.Address != 0)
            {
                LogMessage("OpenRightPanel is open, not clicking");
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

        private static string ActiveWindowTitle()
        {
            const int nChar = 256;
            StringBuilder ss = new StringBuilder(nChar);
            IntPtr handle = IntPtr.Zero;
            handle = GetForegroundWindow();
            if (GetWindowText(handle, ss, nChar) > 0) return ss.ToString();
            else return "";
        }

        private List<LabelOnGround> UpdateLabelComponent() =>
            GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible
            .Where(x =>
                x.ItemOnGround?.Path != null &&
                x.Label.GetClientRect().Center.PointInRectangle(new RectangleF(0, 0, Gamewindow.Width, Gamewindow.Height)) &&
                (x.ItemOnGround.Type == EntityType.WorldItem ||
                x.ItemOnGround.Type == EntityType.Chest && !x.ItemOnGround.GetComponent<Chest>().OpenOnDamage ||
                x.ItemOnGround.Type == EntityType.AreaTransition ||
                x.Label.GetElementByString("The monster is imprisoned by powerful Essences.") != null))
            .OrderBy(x => x.ItemOnGround.DistancePlayer)
            .ToList();

        private void ClickLabel()
        {
            if (Settings.DebugMode) LogMessage("Trying to Click Label");
            Gamewindow = GameController.Window.GetWindowRectangle();

            LabelOnGround nextLabel;
            if (Settings.CachingEnable) nextLabel = GetLabelCaching();
            else nextLabel = GetLabelNoCaching();

            Entity shrine = null;

            foreach (Entity validEntity in GameController.EntityListWrapper.OnlyValidEntities)
            {
                if (validEntity.HasComponent<Shrine>() && validEntity.GetComponent<Shrine>().IsAvailable && validEntity.IsTargetable && !validEntity.IsOpened && !validEntity.IsHidden)
                {
                    shrine = validEntity;
                }
            }

            if (nextLabel == null && shrine == null)
            {
                if (Settings.DebugMode) LogMessage("nextLabel/shrine in ClickLabel() are both null");
                return;
            }

            if (shrine != null && new RectangleF(shrine.Pos.Translate(0, 0, 0).X, shrine.Pos.Translate(0, 0, 0).Y, Gamewindow.Width, Gamewindow.Height).Center.PointInRectangle(new RectangleF(0, 0, Gamewindow.Width, Gamewindow.Height)))
            {
                Input.SetCursorPos(GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)));
                Input.Click(MouseButtons.Left);
            }
            else
            {

                var centerOfLabel = nextLabel?.Label?.GetClientRect().Center
                    + Gamewindow.TopLeft
                    + new Vector2(Random.Next(0, 2), Random.Next(0, 2));

                if (!centerOfLabel.HasValue)
                {
                    if (Settings.DebugMode) LogMessage("centerOfLabel has no Value");
                    return;
                }
                if (nextLabel.Label.GetElementByString("The monster is imprisoned by powerful Essences.") != null)
                {
                    Element label = nextLabel.Label.Parent;
                    if (label.GetElementByString("Corrupted") == null &&
                        //According to the wiki, shrieking essences of these types cannot be corrupted into the special ones. Only screaming can.
                        (label.GetElementByString("Screaming Essence of Misery") != null ||
                        label.GetElementByString("Screaming Essence of Envy") != null ||
                        label.GetElementByString("Screaming Essence of Dread") != null ||
                        label.GetElementByString("Screaming Essence of Scorn") != null ||
                        //Update: Wiki is wrong, shrieking can also be upgraded to them.
                        label.GetElementByString("Shrieking Essence of Misery") != null ||
                        label.GetElementByString("Shrieking Essence of Envy") != null ||
                        label.GetElementByString("Shrieking Essence of Dread") != null ||
                        label.GetElementByString("Shrieking Essence of Scorn") != null ||
                        //Corrupt if there's an essence that is worth more when upgraded
                        //This can change based on the market, so it will be better off using methods from ninjaprice rather than manually listing them, will update it later
                        label.GetElementByString("Shrieking Essence of Greed") != null ||
                        label.GetElementByString("Shrieking Essence of Contempt") != null ||
                        label.GetElementByString("Shrieking Essence of Hatred") != null ||
                        label.GetElementByString("Shrieking Essence of Anger") != null ||
                        label.GetElementByString("Shrieking Essence of Sorrow") != null ||
                        label.GetElementByString("Shrieking Essence of Rage") != null ||
                        label.GetElementByString("Shrieking Essence of Wrath") != null ||
                        label.GetElementByString("Shrieking Essence of Loathing") != null ||
                        label.GetElementByString("Shrieking Essence of Zeal") != null ||
                        label.GetElementByString("Shrieking Essence of Spite") != null)
                        )
                    {
                        //we should corrupt this

                        float latency = GameController.Game.IngameState.CurLatency;

                        //we have to open the inventory first for inventoryItems to fetch items correctly
                        Keyboard.KeyPress(Settings.OpenInventoryKey);
                        Thread.Sleep((int)(latency + Settings.WaitTimeInMs));

                        var inventoryItems = GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory]?.VisibleInventoryItems.ToList();

                        var remnantOfCorruption = inventoryItems.FirstOrDefault(slot => slot.Item.Path == "Metadata/Items/Currency/CurrencyCorruptMonolith");
                        if (remnantOfCorruption == null)
                        {
                            Keyboard.KeyPress(Settings.OpenInventoryKey);
                            return;
                        }

                        Input.SetCursorPos(remnantOfCorruption.GetClientRectCache.Center + GameController.Window.GetWindowRectangle().TopLeft);
                        Thread.Sleep((int)(latency + this.Settings.WaitTimeInMs));

                        Mouse.RightClick();
                        Thread.Sleep((int)(latency + this.Settings.WaitTimeInMs));

                        centerOfLabel = nextLabel?.Label?.GetClientRect().Center
                            + Gamewindow.TopLeft
                            + new Vector2(Random.Next(0, 2), Random.Next(0, 2));
                        Input.SetCursorPos(centerOfLabel.Value);
                        Thread.Sleep((int)(latency + this.Settings.WaitTimeInMs));

                        Mouse.LeftClick();

                        Thread.Sleep((int)(latency + this.Settings.WaitTimeInMs));

                        Keyboard.KeyPress(Settings.OpenInventoryKey);

                        Thread.Sleep((int)(latency + this.Settings.WaitTimeInMs));

                    }
                    else
                    {
                        Input.SetCursorPos(centerOfLabel.Value);
                        Input.Click(MouseButtons.Left);
                    }
                }
                else
                {
                    Input.SetCursorPos(centerOfLabel.Value);
                    Input.Click(MouseButtons.Left);
                }
            }
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
                Settings.ClickShrines.Value && x.ItemOnGround.Type == EntityType.Shrine ||
                Settings.ClickEssences.Value && x.Label.GetElementByString("The monster is imprisoned by powerful Essences.") != null));
            return label;
        }
        private LabelOnGround GetLabelNoCaching()
        {
            var list = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Where(x =>
                x.ItemOnGround?.Path != null &&
                x.Label.GetClientRect().Center.PointInRectangle(new RectangleF(0, 0, Gamewindow.Width, Gamewindow.Height)) &&
                (x.ItemOnGround.Type == EntityType.WorldItem ||
                x.ItemOnGround.Type == EntityType.Chest && !x.ItemOnGround.GetComponent<Chest>().OpenOnDamage ||
                x.ItemOnGround.Type == EntityType.AreaTransition ||
                x.Label.GetElementByString("The monster is imprisoned by powerful Essences.") != null))
            .OrderBy(x => x.ItemOnGround.DistancePlayer).ToList();


            return list.Find(x => x.ItemOnGround.DistancePlayer <= Settings.ClickDistance &&
                (Settings.ClickItems.Value &&
                x.ItemOnGround.Type == EntityType.WorldItem &&
                (!Settings.IgnoreUniques || x.ItemOnGround.GetComponent<WorldItem>()?.ItemEntity.GetComponent<Mods>()?.ItemRarity != ItemRarity.Unique ||
                x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path.StartsWith("Metadata/Items/Metamorphosis/Metamorphosis")) ||
                Settings.ClickChests.Value && x.ItemOnGround.Type == EntityType.Chest ||
                Settings.ClickAreaTransitions.Value && x.ItemOnGround.Type == EntityType.AreaTransition ||
                Settings.ClickEssences.Value && x.Label.GetElementByString("The monster is imprisoned by powerful Essences.") != null));
        }
    }
}
