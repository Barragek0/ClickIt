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
        private bool isPOEActive()
        {

            if (ActiveWindowTitle().IndexOf("Path of Exile", 0, StringComparison.CurrentCultureIgnoreCase) == -1)
            {
                if (Settings.DebugMode)
                    LogMessage("(ClickIt) Path of exile window not active");
                return false;
            }
            return true;
        }
        private bool isPanelOpen()
        {

            if (GameController.IngameState.IngameUi.OpenLeftPanel.Address != 0 || GameController.IngameState.IngameUi.OpenRightPanel.Address != 0)
            {
                if (Settings.DebugMode)
                    LogMessage("(ClickIt) Left or right panel is open");
                return true;
            }
            return false;
        }
        private bool groundItemsVisible()
        {
            if (GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Count < 1)
            {
                if (Settings.DebugMode)
                    LogMessage("(ClickIt) No ground items found");
                return false;
            }
            return true;

        }
        private bool isShrineVisible()
        {
            Entity shrine = null;
            foreach (Entity validEntity in GameController.EntityListWrapper.OnlyValidEntities)
            {
                if (((validEntity.HasComponent<Shrine>() && validEntity.GetComponent<Shrine>().IsAvailable) || validEntity.Path == "Metadata/Shrines/Shrine") && validEntity.IsTargetable && !validEntity.IsOpened && !validEntity.IsHidden)
                {
                    shrine = validEntity;
                }
            }
            if (shrine == null)
            {
                if (Settings.DebugMode)
                    LogMessage("(ClickIt) No shrines found");
                return false;
            }
            return true;

        }

        private bool inTownOrHideout()
        {

            if (GameController.Area.CurrentArea.IsHideout || GameController.Area.CurrentArea.IsTown)
            {
                if (Settings.DebugMode)
                    LogMessage("(ClickIt) In hideout or town");
                return true;
            }
            return false;
        }

        public override Job Tick()
        {
            if (Timer.ElapsedMilliseconds < Settings.WaitTimeInMs.Value - 10 + Random.Next(0, 20))
                return null;
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
            try
            {
                if (!Input.GetKeyState(Settings.ClickLabelKey.Value))
                    return;
                if (!isPOEActive())
                    return;
                //if (GameController.IngameState.IngameUi.ChatTitlePanel.IsVisible) return null; // this has been removed or renamed? can't find the new reference for it
                if (Settings.BlockOnOpenLeftRightPanel && isPanelOpen())
                    return;
                if (inTownOrHideout())
                    return;

                LabelOnGround nextLabel = null;
                Entity shrine = null;

                Gamewindow = GameController.Window.GetWindowRectangle();

                if (Settings.ClickItems && groundItemsVisible())
                {
                    if (Settings.CachingEnable)
                        nextLabel = GetLabelCaching();
                    else 
                        nextLabel = GetLabelNoCaching();
                }

                if (shrine != null && Settings.ClickShrines && isShrineVisible() &&
                    GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)).X > Gamewindow.TopLeft.X &&
                    GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)).X < Gamewindow.TopRight.X &&
                    GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)).Y > Gamewindow.TopLeft.Y &&
                    GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)).Y < Gamewindow.BottomLeft.Y)
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
                    if (nextLabel.Label.GetElementByString("The monster is imprisoned by powerful Essences.") != null && Settings.ClickEssences)
                    {
                        Element label = nextLabel.Label.Parent;
                        if (label.GetElementByString("Corrupted") == null && Settings.CorruptEssences &&
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
                            //If we have too many essences here, self sustaining remnants of corruption is very difficult
                            label.GetElementByString("Shrieking Essence of Sorrow") != null ||
                            label.GetElementByString("Shrieking Essence of Rage") != null ||
                            label.GetElementByString("Shrieking Essence of Loathing") != null ||
                            label.GetElementByString("Shrieking Essence of Zeal") != null ||
                            label.GetElementByString("Shrieking Essence of Spite") != null)
                            )
                        {
                            //we should corrupt this

                            float latency = GameController.Game.IngameState.CurLatency;

                            //we have to open the inventory first for inventoryItems to fetch items correctly
                            Keyboard.KeyPress(Settings.OpenInventoryKey);
                            Thread.Sleep((int)(latency + Settings.InventoryOpenDelayInMs));

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
                    else if (Settings.ClickItems)
                    {
                        Input.SetCursorPos(centerOfLabel.Value);
                        Input.Click(MouseButtons.Left);
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e.ToString());
            }
        }
        private bool isBasicChest(LabelOnGround label)
        {
            switch (label.ItemOnGround.RenderName.ToLower())
            {
                case "chest":
                    return true;
            }
            return false;
        }
        private LabelOnGround GetLabelCaching()
        {
            var label = CachedLabels.Value.Find(x => x.ItemOnGround.DistancePlayer <= Settings.ClickDistance && 
                (Settings.ClickItems.Value && 
                x.ItemOnGround.Type == EntityType.WorldItem && 
                (!Settings.IgnoreUniques || x.ItemOnGround.GetComponent<WorldItem>()?.ItemEntity.GetComponent<Mods>()?.ItemRarity != ItemRarity.Unique || 
                x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path.StartsWith("Metadata/Items/Metamorphosis/Metamorphosis")) ||
                (Settings.ClickBasicChests.Value && x.ItemOnGround.Type == EntityType.Chest && isBasicChest(x)) ||
                (Settings.ClickLeagueChests.Value && x.ItemOnGround.Type == EntityType.Chest && !isBasicChest(x)) ||
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
                (Settings.ClickBasicChests.Value && x.ItemOnGround.Type == EntityType.Chest && isBasicChest(x)) ||
                (Settings.ClickLeagueChests.Value && x.ItemOnGround.Type == EntityType.Chest && !isBasicChest(x)) ||
                Settings.ClickAreaTransitions.Value && x.ItemOnGround.Type == EntityType.AreaTransition ||
                Settings.ClickEssences.Value && x.Label.GetElementByString("The monster is imprisoned by powerful Essences.") != null));
        }
    }
}
