using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Nodes;
using SharpDX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClickIt
{
#nullable enable
    public class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        private Stopwatch Timer { get; } = new Stopwatch();
        private Stopwatch SecondTimer { get; } = new Stopwatch();
        private Random Random { get; } = new Random();
        private TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder ss, int count);

        private bool waitingForCorruption = false;

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
        }

        public override bool Initialise()
        {
            Settings.ReloadPluginButton.OnPressed += ToggleCaching;
            Settings.ReportBugButton.OnPressed += () => { _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues"); };

            if (Settings.CachingEnable)
            {
                CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, Settings.CacheInterval);
            }

            Timer.Start();
            SecondTimer.Start();
            return true;
        }

        private RectangleF FullScreenArea()
        {
            return new RectangleF(0, 0, GameController.Window.GetWindowRectangle().Width,
                GameController.Window.GetWindowRectangle().Height);
        }

        private bool PointIsInClickableArea(Vector2 point)
        {
            Stopwatch timer = new();
            timer.Start();

            bool isInClickableArea = point.PointInRectangle(FullScreenArea()) &&
                   //is point in bottom left corner with health globe/flasks?
                   (!point.PointInRectangle(new RectangleF(
                       (float)(GameController.Window.GetWindowRectangle().BottomLeft.X / 3),
                       (float)(GameController.Window.GetWindowRectangle().BottomLeft.Y / 5 * 3.92),
                       (float)(GameController.Window.GetWindowRectangle().BottomLeft.X +
                               (GameController.Window.GetWindowRectangle().BottomRight.X / 3.4)),
                       GameController.Window.GetWindowRectangle().BottomLeft.Y)))
                   //is point in bottom right corner with skills and mana globe?
                   && !point.PointInRectangle(new RectangleF(
                       (float)(GameController.Window.GetWindowRectangle().BottomRight.X / 3 * 2.12),
                       (float)(GameController.Window.GetWindowRectangle().BottomLeft.Y / 5 * 3.92),
                       GameController.Window.GetWindowRectangle().BottomRight.X,
                       GameController.Window.GetWindowRectangle().BottomRight.Y))
                   //is point at the very top of the screen where buffs and debuffs are?
                   && !point.PointInRectangle(new RectangleF(
                       GameController.Window.GetWindowRectangle().TopLeft.X,
                       GameController.Window.GetWindowRectangle().TopLeft.Y,
                       GameController.Window.GetWindowRectangle().TopRight.X / 2,
                       GameController.Window.GetWindowRectangle().TopLeft.Y + 120));
            timer.Stop();
            if (Settings.DebugMode)
            {
                LogMessage("Checking if point is in clickable area took " + timer.ElapsedMilliseconds + " ms", 5);
            }
            return isInClickableArea;
            //if the point is in any of these, we don't want to click or move the mouse to it
        }

        public List<FieldInfo> fields = new();

        public override void Render()
        {
            if (Settings.DebugMode && Settings.RenderDebug)
            {
                Graphics.DrawFrame(new RectangleF(
                    (float)(GameController.Window.GetWindowRectangle().BottomLeft.X / 3),
                    (float)(GameController.Window.GetWindowRectangle().BottomLeft.Y / 5 * 3.92),
                    (float)(GameController.Window.GetWindowRectangle().BottomLeft.X +
                            (GameController.Window.GetWindowRectangle().BottomRight.X / 3.4)),
                    GameController.Window.GetWindowRectangle().BottomLeft.Y), Color.Green, 1);
                Graphics.DrawFrame(new RectangleF(
                    (float)(GameController.Window.GetWindowRectangle().BottomRight.X / 3 * 2.12),
                    (float)(GameController.Window.GetWindowRectangle().BottomLeft.Y / 5 * 3.92),
                    GameController.Window.GetWindowRectangle().BottomRight.X,
                    GameController.Window.GetWindowRectangle().BottomRight.Y), Color.Cyan, 1);
                Graphics.DrawFrame(new RectangleF(
                    GameController.Window.GetWindowRectangle().TopLeft.X,
                    GameController.Window.GetWindowRectangle().TopLeft.Y,
                    GameController.Window.GetWindowRectangle().TopRight.X / 2,
                    GameController.Window.GetWindowRectangle().TopLeft.Y + 120), Color.Yellow, 1);
            }

            if (fields.Count == 0)
            {
                foreach (FieldInfo field in typeof(ClickItSettings).GetFields(BindingFlags.Public |
                                                                              BindingFlags.NonPublic |
                                                                              BindingFlags.Instance |
                                                                              BindingFlags.Static).ToList())
                {
                    fields.Add(field);
                }
            }

            foreach (PrimaryAltarComponent altar in altarComponents.ToList())
            {
                if (altar.TopMods.Element.GetClientRect().Center.PointInRectangle(FullScreenArea()) && altar.TopMods.Element.IsVisible)
                {
                    decimal TopWeight = 0;

                    decimal TopUpsideWeight = 0;
                    decimal TopUpside1Weight = 0;
                    decimal TopUpside2Weight = 0;

                    decimal TopDownsideWeight = 0;
                    decimal TopDownside1Weight = 0;
                    decimal TopDownside2Weight = 0;

                    decimal BottomWeight = 0;

                    decimal BottomUpsideWeight = 0;
                    decimal BottomUpside1Weight = 0;
                    decimal BottomUpside2Weight = 0;

                    decimal BottomDownsideWeight = 0;
                    decimal BottomDownside1Weight = 0;
                    decimal BottomDownside2Weight = 0;
                    if (Settings.DebugMode && Settings.RenderDebug)
                    {
                        LogMessage("Render 1");
                    }

                    if (Settings.DebugMode && Settings.RenderDebug)
                    {
                        LogMessage("Render 2");
                    }

                    foreach (FieldInfo field in fields.ToList())
                    {
                        string FieldName = field.Name.Replace("<", "").Replace(">", "").Replace("k__BackingField", "");
                        if (Settings.DebugMode && Settings.RenderDebug)
                        {
                            LogMessage("Render 3");
                        }

                        if (FieldName.ToLower().Equals(altar.TopMods.FirstUpside.ToLower() + "_weight") ||
                            FieldName.ToLower().Equals(altar.TopMods.SecondUpside.ToLower() + "_weight"))
                        {
                            if (FieldName.ToLower().Equals(altar.TopMods.FirstUpside.ToLower() + "_weight"))
                            {
                                if (Settings.DebugMode && Settings.RenderDebug)
                                {
                                    LogMessage("Render 4-1-1 - " + altar.TopMods.FirstUpside.ToLower() + "_weight" + " = " +
                                               ((RangeNode<int>)field.GetValue(Settings)).Value);
                                }

                                TopUpside1Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            }
                            else if (FieldName.ToLower().Equals(altar.TopMods.SecondUpside.ToLower() + "_weight"))
                            {
                                if (Settings.DebugMode && Settings.RenderDebug)
                                {
                                    LogMessage("Render 4-1-2 - " + altar.TopMods.FirstUpside.ToLower() + "_weight");
                                }

                                TopUpside2Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            }

                            TopUpsideWeight = TopUpside1Weight + TopUpside2Weight;
                            if (Settings.DebugMode && Settings.RenderDebug)
                            {
                                LogMessage("Render 4-1 - " + TopUpsideWeight + "|" + TopUpside1Weight + "|" +
                                           TopUpside2Weight);
                            }
                        }
                        else if (FieldName.ToLower().Equals(altar.TopMods.FirstDownside.ToLower() + "_weight") ||
                                 FieldName.ToLower().Equals(altar.TopMods.SecondDownside.ToLower() + "_weight"))
                        {
                            if (FieldName.ToLower().Equals(altar.TopMods.FirstDownside.ToLower() + "_weight"))
                            {
                                TopDownside1Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            }
                            else if (FieldName.ToLower().Equals(altar.TopMods.SecondDownside.ToLower() + "_weight"))
                            {
                                TopDownside2Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            }

                            TopDownsideWeight = TopDownside1Weight + TopDownside2Weight;
                            if (Settings.DebugMode && Settings.RenderDebug)
                            {
                                LogMessage("Render 4-2 - " + TopDownsideWeight + "|" + TopDownside1Weight + "|" +
                                           TopDownside2Weight);
                            }
                        }
                        else if (FieldName.ToLower().Equals(altar.BottomMods.FirstUpside.ToLower() + "_weight") ||
                                 FieldName.ToLower().Equals(altar.BottomMods.SecondUpside.ToLower() + "_weight"))
                        {
                            if (FieldName.ToLower().Equals(altar.BottomMods.FirstUpside.ToLower() + "_weight"))
                            {
                                BottomUpside1Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            }
                            else if (FieldName.ToLower().Equals(altar.BottomMods.SecondUpside.ToLower() + "_weight"))
                            {
                                BottomUpside2Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            }

                            BottomUpsideWeight = BottomUpside1Weight + BottomUpside2Weight;
                            if (Settings.DebugMode && Settings.RenderDebug)
                            {
                                LogMessage("Render 4-3 - " + BottomUpsideWeight + "|" + BottomUpside1Weight + "|" +
                                           BottomUpside2Weight);
                            }
                        }
                        else if (FieldName.ToLower().Equals(altar.BottomMods.FirstDownside.ToLower() + "_weight") ||
                                 FieldName.ToLower().Equals(altar.BottomMods.SecondDownside.ToLower() + "_weight"))
                        {
                            if (FieldName.ToLower().Equals(altar.BottomMods.FirstDownside.ToLower() + "_weight"))
                            {
                                BottomDownside1Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            }
                            else if (FieldName.ToLower().Equals(altar.BottomMods.SecondDownside.ToLower() + "_weight"))
                            {
                                BottomDownside2Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            }

                            BottomDownsideWeight = BottomDownside1Weight + BottomDownside2Weight;
                            if (Settings.DebugMode && Settings.RenderDebug)
                            {
                                LogMessage("Render 4-4 - " + BottomDownsideWeight + "|" + BottomDownside1Weight + "|" +
                                           BottomDownside2Weight);
                            }
                        }
                        else
                        {
                            if (Settings.DebugMode && Settings.RenderDebug)
                            {
                                if (!string.IsNullOrEmpty(altar.TopMods.FirstUpside))
                                {
                                    LogMessage("Render 4-5-1: " + altar.TopMods.FirstUpside + "_Weight");
                                }

                                if (!string.IsNullOrEmpty(altar.TopMods.SecondUpside))
                                {
                                    LogMessage("Render 4-5-2: " + altar.TopMods.SecondUpside + "_Weight");
                                }

                                if (!string.IsNullOrEmpty(altar.TopMods.FirstDownside))
                                {
                                    LogMessage("Render 4-5-3: " + altar.TopMods.FirstDownside + "_Weight");
                                }

                                if (!string.IsNullOrEmpty(altar.TopMods.SecondDownside))
                                {
                                    LogMessage("Render 4-5-4: " + altar.TopMods.SecondDownside + "_Weight");
                                }

                                if (!string.IsNullOrEmpty(altar.BottomMods.FirstUpside))
                                {
                                    LogMessage("Render 4-5-5: " + altar.BottomMods.FirstUpside + "_Weight");
                                }

                                if (!string.IsNullOrEmpty(altar.BottomMods.SecondUpside))
                                {
                                    LogMessage("Render 4-5-6: " + altar.BottomMods.SecondUpside + "_Weight");
                                }

                                if (!string.IsNullOrEmpty(altar.BottomMods.FirstDownside))
                                {
                                    LogMessage("Render 4-5-7: " + altar.BottomMods.FirstDownside + "_Weight");
                                }

                                if (!string.IsNullOrEmpty(altar.BottomMods.SecondDownside))
                                {
                                    LogMessage("Render 4-5-8: " + altar.BottomMods.SecondDownside + "_Weight");
                                }
                            }
                        }

                        if (Settings.DebugMode && Settings.RenderDebug)
                        {
                            LogMessage(
                                "Render 9 - top - " + TopWeight + " = " + TopUpsideWeight + " / " + TopDownsideWeight);
                        }

                        TopWeight = Math.Round((TopUpsideWeight <= 0 ? 1 : TopUpsideWeight) /
                                    (TopDownsideWeight <= 0 ? 1 : TopDownsideWeight), 2);
                        if (Settings.DebugMode && Settings.RenderDebug)
                        {
                            LogMessage("Render 9 - bot - " + BottomWeight + " = " + BottomUpsideWeight + " / " +
                                       BottomDownsideWeight);
                        }

                        BottomWeight = Math.Round((BottomUpsideWeight <= 0 ? 1 : BottomUpsideWeight) /
                                       (BottomDownsideWeight <= 0 ? 1 : BottomDownsideWeight), 2);
                    }

                    if (TopUpside1Weight <= 0 && !string.IsNullOrEmpty(altar.TopMods.FirstUpside.ToLower()))
                    {
                        LogError("Could not match top upside 1 with field - " + altar.TopMods.FirstUpside.ToLower() +
                                 "_weight");
                    }

                    if (TopUpside2Weight <= 0 && !string.IsNullOrEmpty(altar.TopMods.SecondUpside.ToLower()))
                    {
                        LogError("Could not match top upside 2 with field - " + altar.TopMods.SecondUpside.ToLower() +
                                 "_weight");
                    }

                    if (TopDownside1Weight <= 0 && !string.IsNullOrEmpty(altar.TopMods.FirstDownside.ToLower()))
                    {
                        LogError("Could not match top downside 1 with field - " + altar.TopMods.FirstDownside.ToLower() +
                                 "_weight");
                    }

                    if (TopDownside2Weight <= 0 && !string.IsNullOrEmpty(altar.TopMods.SecondDownside.ToLower()))
                    {
                        LogError("Could not match top downside 2 with field - " + altar.TopMods.SecondDownside.ToLower() +
                                 "_weight");
                    }

                    if (BottomUpside1Weight <= 0 && !string.IsNullOrEmpty(altar.BottomMods.FirstUpside.ToLower()))
                    {
                        LogError("Could not match bottom upside 1 with field - " + altar.BottomMods.FirstUpside.ToLower() +
                                 "_weight");
                    }

                    if (BottomUpside2Weight <= 0 && !string.IsNullOrEmpty(altar.BottomMods.SecondUpside.ToLower()))
                    {
                        LogError("Could not match bottom upside 2 with field - " + altar.BottomMods.SecondUpside.ToLower() +
                                 "_weight");
                    }

                    if (BottomDownside1Weight <= 0 && !string.IsNullOrEmpty(altar.BottomMods.FirstDownside.ToLower()))
                    {
                        LogError("Could not match bottom downside 1 with field - " +
                                 altar.BottomMods.FirstDownside.ToLower() + "_weight");
                    }

                    if (BottomDownside2Weight <= 0 && !string.IsNullOrEmpty(altar.BottomMods.SecondDownside.ToLower()))
                    {
                        LogError("Could not match bottom downside 2 with field - " +
                                 altar.BottomMods.SecondDownside.ToLower() + "_weight");
                    }

                    Element? boxToClick = null;
                    if (TopUpsideWeight <= 0)
                    {
                        _ = Graphics.DrawText("Top upside weights couldn't be recognised " +
                            "\n1:" + (string.IsNullOrEmpty(altar.TopMods.FirstUpside) ? "null" : string.IsNullOrEmpty(altar.TopMods.FirstUpside)) +
                            "\n2:" + (string.IsNullOrEmpty(altar.TopMods.SecondUpside) ? "null" : string.IsNullOrEmpty(altar.TopMods.SecondUpside)) +
                            "\nPlease report this as a bug on github",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.Orange, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.Yellow, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.Yellow, 2);
                    }
                    else if (TopDownsideWeight <= 0)
                    {
                        _ = Graphics.DrawText("Top downside weights couldn't be recognised " +
                            "\n1:" + (string.IsNullOrEmpty(altar.TopMods.FirstDownside) ? "null" : string.IsNullOrEmpty(altar.TopMods.FirstDownside)) +
                            "\n2:" + (string.IsNullOrEmpty(altar.TopMods.SecondDownside) ? "null" : string.IsNullOrEmpty(altar.TopMods.SecondDownside)) +
                            "\nPlease report this as a bug on github",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.Orange, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.Yellow, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.Yellow, 2);
                    }
                    else if (BottomUpsideWeight <= 0)
                    {
                        _ = Graphics.DrawText("Bottom upside weights couldn't be recognised " +
                            "\n1:" + (string.IsNullOrEmpty(altar.BottomMods.FirstUpside) ? "null" : string.IsNullOrEmpty(altar.BottomMods.FirstUpside)) +
                            "\n2:" + (string.IsNullOrEmpty(altar.BottomMods.SecondUpside) ? "null" : string.IsNullOrEmpty(altar.BottomMods.SecondUpside)) +
                            "\nPlease report this as a bug on github",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.Orange, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.Yellow, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.Yellow, 2);
                    }
                    else if (BottomDownsideWeight <= 0)
                    {
                        _ = Graphics.DrawText("Bottom downside weights couldn't be recognised " +
                            "\n1:" + (string.IsNullOrEmpty(altar.BottomMods.FirstDownside) ? "null" : string.IsNullOrEmpty(altar.BottomMods.FirstDownside)) +
                            "\n2:" + (string.IsNullOrEmpty(altar.BottomMods.SecondDownside) ? "null" : string.IsNullOrEmpty(altar.BottomMods.SecondDownside)) +
                            "\nPlease report this as a bug on github",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.Orange, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.Yellow, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.Yellow, 2);
                    }
                    else if ((TopDownside1Weight >= 90 || TopDownside2Weight >= 90) && (BottomDownside1Weight >= 90 || BottomDownside2Weight >= 90))
                    {
                        _ = Graphics.DrawText("Weighting has been overridden\n\nBoth options have downsides with a weight of 90+ that may brick your build.",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.Orange, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.OrangeRed, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.OrangeRed, 2);
                    }
                    else if (TopUpside1Weight >= 90 || TopUpside2Weight >= 90)
                    {
                        _ = Graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of 90+",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.LawnGreen, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.LawnGreen, 3);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.OrangeRed, 2);
                        boxToClick = altar.TopButton.Element;
                    }
                    else if (BottomUpside1Weight >= 90 || BottomUpside2Weight >= 90)
                    {
                        _ = Graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of 90+",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.LawnGreen, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.OrangeRed, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.LawnGreen, 3);
                        boxToClick = altar.BottomButton.Element;
                    }
                    else if (TopDownside1Weight >= 90 || TopDownside2Weight >= 90)
                    {
                        _ = Graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the top downsides has a weight of 90+",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.LawnGreen, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.OrangeRed, 3);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.LawnGreen, 2);
                        boxToClick = altar.BottomButton.Element;
                    }
                    else if (BottomDownside1Weight >= 90 || BottomDownside2Weight >= 90)
                    {
                        _ = Graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the bottom downsides has a weight of 90+",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.LawnGreen, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.LawnGreen, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.OrangeRed, 3);
                        boxToClick = altar.TopButton.Element;
                    }
                    else if (TopWeight > BottomWeight)
                    {
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.LawnGreen, 3);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.OrangeRed, 2);
                        boxToClick = altar.TopButton.Element;
                    }
                    else if (BottomWeight > TopWeight)
                    {
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.OrangeRed, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.LawnGreen, 3);
                        boxToClick = altar.BottomButton.Element;
                    }
                    else
                    {
                        _ = Graphics.DrawText("Mods have equal weight, you should choose.",
                            altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -25), Color.Orange, 30);
                        Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.Yellow, 2);
                        Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.Yellow, 2);
                    }

                    _ = Graphics.DrawText("Upside: " + TopUpsideWeight,
                        altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(5, -32), Color.LawnGreen, 14);
                    _ = Graphics.DrawText("Downside: " + TopDownsideWeight,
                        altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(5, -20), Color.OrangeRed, 14);
                    _ = Graphics.DrawText("Upside: " + BottomUpsideWeight,
                        altar.BottomMods.Element.GetClientRect().TopLeft + new Vector2(10, -32), Color.LawnGreen, 14);
                    _ = Graphics.DrawText("Downside: " + BottomDownsideWeight,
                        altar.BottomMods.Element.GetClientRect().TopLeft + new Vector2(10, -20), Color.OrangeRed, 14);
                    _ = Graphics.DrawText("" + TopWeight,
                        altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(10, 5),
                        TopWeight > BottomWeight ? Color.LawnGreen :
                        BottomWeight > TopWeight ? Color.OrangeRed : Color.Yellow, 18);
                    _ = Graphics.DrawText("" + BottomWeight,
                        altar.BottomMods.Element.GetClientRect().TopLeft + new Vector2(10, 5),
                        TopWeight > BottomWeight ? Color.OrangeRed :
                        BottomWeight > TopWeight ? Color.LawnGreen : Color.Yellow, 18);

                    if (((altar.AltarType == AltarType.EaterOfWorlds && Settings.ClickEaterAltars) || (altar.AltarType == AltarType.SearingExarch && Settings.ClickExarchAltars)) &&
                        PointIsInClickableArea(boxToClick.GetClientRect().Center))
                    {
                        if (boxToClick != null && boxToClick.IsVisible)
                        {
                            if (canClick())
                            {
                                Mouse.blockInput(true);
                                Input.SetCursorPos(boxToClick.GetClientRect().Center);
                                if (Settings.LeftHanded)
                                {
                                    Mouse.RightClick();
                                }
                                else
                                {
                                    Mouse.LeftClick();
                                }
                                Mouse.blockInput(false);
                            }

                        }
                    }
                }
            }

            _ = Core.ParallelRunner.Run(new Coroutine(ScanForAltarsLogic(), this, "ClickIt.ScanForAltarsLogic"));


            if (canClick())
            {
                _ = Core.ParallelRunner.Run(new Coroutine(ClickLabel(), this, "ClickIt.ClickLogic"));
            }
        }

        private bool canClick()
        {
            if (!Input.GetKeyState(Settings.ClickLabelKey.Value))
            {
                return false;
            }

            if (!IsPOEActive())
            {
                return false;
            }

            if (Settings.BlockOnOpenLeftRightPanel && IsPanelOpen())
            {
                return false;
            }

            return !InTownOrHideout() && !waitingForCorruption && !GameController.IngameState.IngameUi.ChatTitlePanel.IsVisible;
        }

        private IEnumerator ScanForAltarsLogic()
        {
            //the code below is taxing, we don't want to run it too often
            if (SecondTimer.ElapsedMilliseconds < 500)
            {
                yield break;
            }

            SecondTimer.Restart();
            List<LabelOnGround> altarLabels = new();
            if (Settings.HighlightExarchAltars)
            {
                altarLabels.AddRange(GetAltarLabels(AltarType.SearingExarch));
            }

            if (Settings.HighlightEaterAltars)
            {
                altarLabels.AddRange(GetAltarLabels(AltarType.EaterOfWorlds));
            }

            if (altarLabels.Count > 0)
            {
                foreach (LabelOnGround label in altarLabels.ToList())
                {
                    //altar mods start with <valuedefault> and also include <enchanted> before the positive mods
                    List<Element> elements = GetElementsByStringContains(label.Label, "valuedefault");
                    foreach (Element element in elements.ToList())
                    {
                        if (element != null && element.IsVisible)
                        {
                            if (label.ItemOnGround.Path.Contains("CleansingFireAltar"))
                            {
                                if (Settings.DebugMode)
                                {
                                    LogMessage("CleansingFireAltar");
                                }
                            }
                            else if (label.ItemOnGround.Path.Contains("TangleAltar"))
                            {
                                if (Settings.DebugMode)
                                {
                                    LogMessage("TangleAltar");
                                }
                            }

                            AltarType altarType = label.ItemOnGround.Path.Contains("CleansingFireAltar")
                                ?
                                AltarType.SearingExarch
                                :
                                label.ItemOnGround.Path.Contains("TangleAltar")
                                    ? AltarType.EaterOfWorlds
                                    :
                                    AltarType.Unknown;
                            PrimaryAltarComponent altarComponent = new(altarType,
                                new SecondaryAltarComponent(new Element(), "", "", "", "")
                                , new AltarButton(new Element()),
                                new SecondaryAltarComponent(new Element(), "", "", "", ""),
                                new AltarButton(new Element()));
                            Element altarParent = element.Parent.Parent;
                            Element? topAltarElement = altarParent.GetChildFromIndices(0, 1);
                            Element? bottomAltarElement = altarParent.GetChildFromIndices(1, 1);
                            if (topAltarElement != null)
                            {
                                updateComponentFromElementData(true, altarParent, altarComponent, topAltarElement,
                                    altarType);
                            }

                            if (bottomAltarElement != null)
                            {
                                updateComponentFromElementData(false, altarParent, altarComponent, bottomAltarElement,
                                    altarType);
                            }

                            if (altarComponent.TopMods != null && altarComponent.TopButton != null &&
                                altarComponent.BottomMods != null && altarComponent.BottomButton != null)
                            {
                                if (!altarComponents.Where(x =>
                                            x.TopMods.FirstUpside.Equals(altarComponent.TopMods.FirstUpside)
                                            && x.TopMods.SecondUpside.Equals(altarComponent.TopMods.SecondUpside)
                                            && x.TopMods.FirstDownside.Equals(altarComponent.TopMods.FirstDownside)
                                            && x.TopMods.SecondDownside.Equals(altarComponent.TopMods.SecondDownside)
                                            && x.BottomMods.FirstUpside.Equals(altarComponent.BottomMods.FirstUpside)
                                            && x.BottomMods.SecondUpside.Equals(altarComponent.BottomMods.SecondUpside)
                                            && x.BottomMods.FirstDownside.Equals(
                                                altarComponent.BottomMods.FirstDownside)
                                            && x.BottomMods.SecondDownside.Equals(altarComponent.BottomMods
                                                .SecondDownside))
                                        .Any())
                                {
                                    altarComponents.Add(altarComponent);
                                    LogMessage("New altar added to altarcomponents list");
                                }
                                else
                                {
                                    if (Settings.DebugMode)
                                    {
                                        LogMessage("Altar already added to altarcomponents list");
                                    }
                                }
                            }
                            else
                            {
                                if (Settings.DebugMode)
                                {
                                    LogError("Part of altarcomponent is null");
                                    LogError("part1: " + altarComponent.TopMods);
                                    LogError("part2: " + altarComponent.TopButton);
                                    LogError("part3: " + altarComponent.BottomMods);
                                    LogError("part4: " + altarComponent.BottomButton);
                                }
                            }
                        }
                        else
                        {
                            LogError("Element is null");
                        }
                    }
                }
            }
            else
            {
                altarComponents.Clear();
            }
            yield break;
        }

        private void ToggleCaching()
        {
            CachedLabels = Settings.CachingEnable.Value && CachedLabels == null
                ? new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, Settings.CacheInterval)
                : null;
        }

        private bool IsPOEActive()
        {
            if (ActiveWindowTitle().IndexOf("Path of Exile", 0, StringComparison.CurrentCultureIgnoreCase) == -1)
            {
                if (Settings.DebugMode)
                {
                    LogMessage("(ClickIt) Path of exile window not active");
                }

                return false;
            }

            return true;
        }

        private bool IsPanelOpen()
        {
            if (GameController.IngameState.IngameUi.OpenLeftPanel.Address != 0 ||
                GameController.IngameState.IngameUi.OpenRightPanel.Address != 0)
            {
                if (Settings.DebugMode)
                {
                    LogMessage("(ClickIt) Left or right panel is open");
                }

                return true;
            }

            return false;
        }

        private bool GroundItemsVisible()
        {
            if (GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Count < 1)
            {
                if (Settings.DebugMode)
                {
                    LogMessage("(ClickIt) No ground items found");
                }

                return false;
            }

            return true;
        }

        private Entity? GetShrine()
        {
            return GameController.EntityListWrapper.OnlyValidEntities.Find(x => x != null && PointIsInClickableArea(GameController.Game.IngameState.Camera.WorldToScreen(x.Pos)) &&
                                                                            x.HasComponent<Shrine>() && x.GetComponent<Shrine>() != null && x.GetComponent<Shrine>().IsAvailable &&
                                                                            !x.IsOpened && x.IsTargetable && !x.IsHidden && x.IsValid);
        }

        private bool InTownOrHideout()
        {
            if (GameController.Area.CurrentArea.IsHideout || GameController.Area.CurrentArea.IsTown)
            {
                if (Settings.DebugMode)
                {
                    LogMessage("(ClickIt) In hideout or town");
                }

                return true;
            }

            return false;
        }

        private static readonly List<Element> elementsByStringContainsList = new();

        public List<Element> GetElementsByStringContains(Element label, string str)
        {
            elementsByStringContainsList.Clear();
            if (label != null && label.GetText(512) != null && label.GetText(512).Contains(str))
            {
                elementsByStringContainsList.Add(label);
            }

            IEnumerable<Element> children = label.GetChildAtIndex(0).Children
                .Where(c => c != null && c.GetText(512) != null && c.GetText(512).Contains(str));
            if (children != null && children.Count() > 0)
            {
                elementsByStringContainsList.AddRange(children);
            }

            IEnumerable<Element> childrenOfChildren = label.GetChildAtIndex(1).Children
                .Where(c => c != null && c.GetText(512) != null && c.GetText(512).Contains(str));
            if (childrenOfChildren != null && childrenOfChildren.Count() > 0)
            {
                elementsByStringContainsList.AddRange(childrenOfChildren);
            }

            return elementsByStringContainsList;
        }

        private readonly List<PrimaryAltarComponent> altarComponents = new();

        private string? GetLine(string text, int lineNo)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length >= lineNo ? lines[lineNo] : null;
        }

        private int CountLines(string text)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length;
        }

        public override Job? Tick()
        {
            return null;
        }

        private void updateComponentFromElementData(bool top, Element altarParent, PrimaryAltarComponent altarComponent,
            Element ElementToExtractDataFrom, AltarType altarType)
        {
            string NegativeModType = "";
            List<string> mods = new();
            List<string> upsides = new();
            List<string> downsides = new();
            if (Settings.DebugMode)
            {
                LogMessage(ElementToExtractDataFrom.GetText(512));
            }

            string AltarMods = ElementToExtractDataFrom.GetText(512).Replace("<valuedefault>", "").Replace("{", "")
                .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "").Replace("gain:", "")
                .Replace("gains:", "");

            for (int i = 0; i < CountLines(ElementToExtractDataFrom.GetText(512)); i++)
            {
                if (i == 0)
                {
                    NegativeModType = GetLine(AltarMods, 0);
                }
                else if (GetLine(AltarMods, i) != null)
                {
                    mods.Add(GetLine(AltarMods, i));
                }

                if (Settings.DebugMode)
                {
                    LogMessage("Altarmods (" + i + ") Added: " + GetLine(AltarMods, i));
                }
            }

            foreach (string mod in mods.ToList())
            {
                bool found = false;
                string localmod = NegativeModType + new string(mod.Where(char.IsLetter).ToArray());
                foreach (FieldInfo field in typeof(ClickItSettings).GetFields(BindingFlags.Public |
                                                                              BindingFlags.NonPublic |
                                                                              BindingFlags.Instance |
                                                                              BindingFlags.Static).ToList())
                {
                    string FieldName = field.Name.Replace("<", "").Replace(">", "").Replace("k__BackingField", "");
                    if (FieldName.StartsWith("Exarch_") || FieldName.StartsWith("Eater_"))
                    {
                        string fieldToLower = FieldName.ToLower();
                        string localmodToLowerWithPrefix = (altarType == AltarType.SearingExarch ? "Exarch_" :
                            altarType == AltarType.EaterOfWorlds ? "Eater_" : "Unknown_") + localmod;
                        if (localmodToLowerWithPrefix.ToLower().Contains(fieldToLower))
                        {
                            //upside
                            if (localmod.ToLower().Contains("chancetodropanadditional") ||
                                localmod.ToLower().Contains("finalbossdrops") ||
                                localmod.ToLower().Contains("increasedquantityofitems") ||
                                localmod.ToLower().Contains("droppedbyslainenemieshave") ||
                                localmod.ToLower().Contains("chancetobeduplicated"))
                            {
                                upsides.Add(FieldName);
                                found = true;
                                if (Settings.DebugMode)
                                {
                                    LogMessage("Added " +
                                               (altarType == AltarType.SearingExarch ? "Exarch" :
                                                   altarType == AltarType.EaterOfWorlds ? "Eater" : "Unknown") +
                                               " upside: " + fieldToLower + " - " + localmodToLowerWithPrefix);
                                }
                            }
                            else //downside
                            {
                                downsides.Add(FieldName);
                                found = true;
                                if (Settings.DebugMode)
                                {
                                    LogMessage("Added " +
                                               (altarType == AltarType.SearingExarch ? "Exarch" :
                                                   altarType == AltarType.EaterOfWorlds ? "Eater" : "Unknown") +
                                               " downside: " + fieldToLower + " - " + localmodToLowerWithPrefix);
                                }
                            }
                        }
                    }
                }

                if (!found)
                {
                    try
                    {
                        if (Settings.DebugMode)
                        {
                            LogError(
                                "updateComponentFromElementData: Failed to match mod with field? Field may not be required - localmod:" +
                                localmod);
                        }
                    }
                    catch (Exception)
                    {
                        if (Settings.DebugMode)
                        {
                            LogError(
                                "updateComponentFromElementData: Failed to match mod with field? Field may not be required - unable to write field, sequence contains no elements");
                        }
                    }
                }
            }

            if (Settings.DebugMode)
            {
                LogMessage("Setting up altar component");
            }

            if (top)
            {
                string upside1 = "";
                string upside2 = "";
                string downside1 = "";
                string downside2 = "";
                altarComponent.TopButton = new AltarButton(ElementToExtractDataFrom.Parent);
                if (upsides.Count > 0)
                {
                    upside1 = upsides.First();
                }

                if (upsides.Count > 1)
                {
                    upside2 = upsides.Last();
                }

                if (downsides.Count > 0)
                {
                    downside1 = downsides.First();
                }

                if (downsides.Count > 1)
                {
                    downside2 = downsides.Last();
                }

                altarComponent.TopMods =
                    new SecondaryAltarComponent(ElementToExtractDataFrom, upside1, upside2, downside1, downside2);
                if (Settings.DebugMode)
                {
                    LogMessage("Updated top altar component: " + altarComponent.TopMods);
                    LogMessage("Upside1: " + altarComponent.TopMods.FirstUpside);
                    LogMessage("Upside2: " + altarComponent.TopMods.SecondUpside);
                    LogMessage("Downside1: " + altarComponent.TopMods.FirstDownside);
                    LogMessage("Downside2: " + altarComponent.TopMods.SecondDownside);
                }
            }
            else
            {
                string upside1 = "";
                string upside2 = "";
                string downside1 = "";
                string downside2 = "";
                altarComponent.BottomButton = new AltarButton(ElementToExtractDataFrom.Parent);
                if (upsides.Count > 0)
                {
                    upside1 = upsides.First();
                }

                if (upsides.Count > 1)
                {
                    upside2 = upsides.Last();
                }

                if (downsides.Count > 0)
                {
                    downside1 = downsides.First();
                }

                if (downsides.Count > 1)
                {
                    downside2 = downsides.Last();
                }

                altarComponent.BottomMods =
                    new SecondaryAltarComponent(ElementToExtractDataFrom, upside1, upside2, downside1, downside2);
                if (Settings.DebugMode)
                {
                    LogMessage("Updated bottom altar component: ");
                    LogMessage("Upside1: " + (string.IsNullOrEmpty(altarComponent.BottomMods.FirstUpside)
                        ? "null"
                        : altarComponent.BottomMods.FirstUpside));
                    LogMessage("Upside2: " + (string.IsNullOrEmpty(altarComponent.BottomMods.SecondUpside)
                        ? "null"
                        : altarComponent.BottomMods.SecondUpside));
                    LogMessage("Downside1: " + (string.IsNullOrEmpty(altarComponent.BottomMods.FirstDownside)
                        ? "Null"
                        : altarComponent.BottomMods.FirstDownside));
                    LogMessage("Downside2: " + (string.IsNullOrEmpty(altarComponent.BottomMods.SecondDownside)
                        ? "null"
                        : altarComponent.BottomMods.SecondDownside));
                }
            }
        }

        private List<LabelOnGround> GetHarvestLabels()
        {
            List<LabelOnGround> list = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Where(x =>
                x.ItemOnGround.Path != null
                && PointIsInClickableArea(x.Label.GetClientRect().Center)
            ).ToList();

            return list.FindAll(x =>
                x.ItemOnGround.Path.Contains("Harvest/Irrigator") || x.ItemOnGround.Path.Contains("Harvest/Extractor")).OrderBy(x => x.ItemOnGround.DistancePlayer).ToList();
        }

        private List<LabelOnGround> GetAltarLabels(AltarType type)
        {
            List<LabelOnGround> list = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Where(x =>
                x.ItemOnGround.Path != null
                && x.Label.GetClientRect().Center.PointInRectangle(FullScreenArea())
            ).ToList();

            return list.FindAll(x =>
                x.ItemOnGround.Path.Contains(type == AltarType.SearingExarch ? "CleansingFireAltar" : "TangleAltar"));
        }

        private static string ActiveWindowTitle()
        {
            const int nChar = 256;
            StringBuilder ss = new(nChar);
            IntPtr handle = GetForegroundWindow();
            return GetWindowText(handle, ss, nChar) > 0 ? ss.ToString() : "";
        }

        private List<LabelOnGround> UpdateLabelComponent()
        {
            return GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible
                .Where(x =>
                    x.ItemOnGround?.Path != null &&
                    //this check is probably unnecessary, lets just make sure
                    PointIsInClickableArea(x.Label.GetClientRect().Center) &&
                    (x.ItemOnGround.Type == EntityType.WorldItem ||
                     (x.ItemOnGround.Type == EntityType.Chest && !x.ItemOnGround.GetComponent<Chest>().OpenOnDamage) ||
                     x.ItemOnGround.Type == EntityType.AreaTransition ||
                     GetElementByString(x.Label, "The monster is imprisoned by powerful Essences.") != null))
                .OrderBy(x => x.ItemOnGround.DistancePlayer)
                .ToList();
        }

        private IEnumerator ClickLabel(Element? altar = null)
        {
            if (Timer.ElapsedMilliseconds < Settings.WaitTimeInMs.Value - 10 + Random.Next(0, 5))
            {
                yield break;
            }

            Timer.Restart();
            try
            {

                if (Settings.BlockUserInput)
                {
                    Mouse.blockInput(true);
                }

                LabelOnGround nextLabel = null;
                Entity? shrine = GetShrine();

                List<LabelOnGround> harvestLabels = GetHarvestLabels();

                Stopwatch timer = new();
                if (Settings.CachingEnable)
                {
                    timer.Start();
                    nextLabel = GetLabelCaching();
                    if (Settings.DebugMode)
                    {
                        LogMessage("Collecting cached ground labels took " + timer.ElapsedMilliseconds + " ms", 5);
                    }

                    timer.Stop();
                }
                else
                {
                    timer.Start();
                    nextLabel = GetLabelNoCaching();
                    if (Settings.DebugMode)
                    {
                        LogMessage("Collecting uncached ground labels took " + timer.ElapsedMilliseconds + " ms", 5);
                    }

                    timer.Stop();
                }

                if (Settings.NearestHarvest && harvestLabels.Count > 0)
                {
                    LabelOnGround harvestLabel = harvestLabels.FirstOrDefault();
                    if (harvestLabel != null && harvestLabel.IsVisible)
                    {
                        if (canClick())
                        {
                            Mouse.blockInput(true);
                            Input.SetCursorPos(harvestLabel.Label.GetClientRect().Center);
                            if (Settings.LeftHanded)
                            {
                                Mouse.RightClick();
                            }
                            else
                            {
                                Mouse.LeftClick();
                            }
                            Mouse.blockInput(false);
                        }
                        yield break;
                    }
                }

                else if (Settings.ClickShrines && shrine != null && !waitingForCorruption &&
                         GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)).X >
                         GameController.Window.GetWindowRectangle().TopLeft.X &&
                         GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)).X <
                         GameController.Window.GetWindowRectangle().TopRight.X &&
                         GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)).Y >
                         GameController.Window.GetWindowRectangle().TopLeft.Y + 150 &&
                         GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)).Y <
                         GameController.Window.GetWindowRectangle().BottomLeft.Y - 275)
                {
                    Input.SetCursorPos(
                        GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0)));
                    if (Settings.LeftHanded)
                    {
                        Mouse.RightClick();
                    }
                    else
                    {
                        Mouse.LeftClick();
                    }

                    Mouse.blockInput(false);
                }
                else if (nextLabel != null && GetElementByString(nextLabel.Label, "The monster is imprisoned by powerful Essences.") !=
                        null && Settings.ClickEssences && GroundItemsVisible())
                {
                    if (Settings.DebugMode)
                    {
                        LogMessage("Found an essence", 5);
                    }

                    Element label = nextLabel.Label.Parent;
                    bool MeetsCorruptCriteria = false;

                    Vector2? centerOfLabel = nextLabel?.Label?.GetClientRect().Center
                                        + GameController.Window.GetWindowRectangle().TopLeft
                                        + new Vector2(Random.Next(0, 2), Random.Next(0, 2));

                    if (nextLabel?.ItemOnGround.Type == EntityType.Chest)
                    {
                        centerOfLabel = nextLabel?.Label?.GetClientRect().Center
                                        + GameController.Window.GetWindowRectangle().TopLeft
                                        + new Vector2(Random.Next(0, 2),
                                            -Random.Next(Settings.ChestHeightOffset, Settings.ChestHeightOffset + 2));
                    }

                    if (!centerOfLabel.HasValue)
                    {
                        if (Settings.DebugMode)
                        {
                            LogMessage("(ClickIt) centerOfLabel has no Value | Cache: " + Settings.CachingEnable.Value);
                        }

                        yield break;
                    }
                    if (GetElementByString(label, "Corrupted") == null)
                    {
                        if (Settings.CorruptAllEssences)
                        {
                            MeetsCorruptCriteria = true;
                        }
                        else if (Settings.CorruptMEDSEssences &&
                            (GetElementByString(label, "Screaming Essence of Misery") != null ||
                            GetElementByString(label, "Screaming Essence of Envy") != null ||
                            GetElementByString(label, "Screaming Essence of Dread") != null ||
                            GetElementByString(label, "Screaming Essence of Scorn") != null ||
                            GetElementByString(label, "Shrieking Essence of Misery") != null ||
                            GetElementByString(label, "Shrieking Essence of Envy") != null ||
                            GetElementByString(label, "Shrieking Essence of Dread") != null ||
                            GetElementByString(label, "Shrieking Essence of Scorn") != null))
                        {
                            MeetsCorruptCriteria = true;
                        }
                        else if (Settings.CorruptProfitableEssences &&
                           ((GetElementByString(label, "Shrieking Essence of Contempt") != null) ||
                           (GetElementByString(label, "Shrieking Essence of Woe") != null) ||
                           (GetElementByString(label, "Shrieking Essence of Sorrow") != null) ||
                           (GetElementByString(label, "Shrieking Essence of Loathing") != null) ||
                           (GetElementByString(label, "Shrieking Essence of Zeal") != null) ||
                           (GetElementByString(label, "Shrieking Essence of Envy") != null)))
                        {
                            MeetsCorruptCriteria = true;
                        }
                        else if (Settings.CorruptAnyNonShrieking &&
                           GetElementByString(label, "Shrieking Essence of Greed") == null &&
                           GetElementByString(label, "Shrieking Essence of Contempt") == null &&
                           GetElementByString(label, "Shrieking Essence of Hatred") == null &&
                           GetElementByString(label, "Shrieking Essence of Woe") == null &&
                           GetElementByString(label, "Shrieking Essence of Fear") == null &&
                           GetElementByString(label, "Shrieking Essence of Anger") == null &&
                           GetElementByString(label, "Shrieking Essence of Torment") == null &&
                           GetElementByString(label, "Shrieking Essence of Sorrow") == null &&
                           GetElementByString(label, "Shrieking Essence of Rage") == null &&
                           GetElementByString(label, "Shrieking Essence of Suffering") == null &&
                           GetElementByString(label, "Shrieking Essence of Wrath") == null &&
                           GetElementByString(label, "Shrieking Essence of Doubt") == null &&
                           GetElementByString(label, "Shrieking Essence of Loathing") == null &&
                           GetElementByString(label, "Shrieking Essence of Zeal") == null &&
                           GetElementByString(label, "Shrieking Essence of Anguish") == null &&
                           GetElementByString(label, "Shrieking Essence of Spite") == null &&
                           GetElementByString(label, "Shrieking Essence of Scorn") == null &&
                           GetElementByString(label, "Shrieking Essence of Envy") == null &&
                           GetElementByString(label, "Shrieking Essence of Misery") == null &&
                           GetElementByString(label, "Shrieking Essence of Dread") == null)
                        {
                            MeetsCorruptCriteria = true;
                        }
                    }
                    if (MeetsCorruptCriteria)
                    {
                        if (Settings.DebugMode)
                        {
                            LogMessage("Essence is not corrupted and meets criteria, lets corrupt it", 5);
                        }
                        //we should corrupt this
                        if (Settings.DebugMode)
                        {
                            LogMessage("Starting corruption", 5);
                        }

                        waitingForCorruption = true;
                        new Thread(async () =>
                        {
                            LogMessage("Async corruption started", 5);
                            float latency = GameController.Game.IngameState.ServerData.Latency;

                            //we have to open the inventory first for inventoryItems to fetch items correctly
                            Keyboard.KeyPress(Settings.OpenInventoryKey);

                            if (Settings.DebugMode)
                            {
                                LogMessage("(ClickIt) Fetching inventory items");
                            }

                            List<NormalInventoryItem> inventoryItems;

                            Task<List<NormalInventoryItem>> task = FetchInventoryItemsTask();
                            if (await Task.WhenAny(task, Task.Delay(1000)) == task)
                            {
                                inventoryItems = FetchInventoryItems();
                            }
                            else
                            {
                                LogError(
                                    "(ClickIt) Inventory offsets are incorrect. You need to manually corrupt until the offsets are fixed in PoeHelper (this isn't an issue with the ClickIt plugin).",
                                    20);
                                waitingForCorruption = false;
                                Mouse.blockInput(false);
                                return;
                            }

                            NormalInventoryItem remnantOfCorruption = inventoryItems.FirstOrDefault(slot =>
                                slot.Item.Path == "Metadata/Items/Currency/CurrencyCorruptMonolith");

                            if (remnantOfCorruption == null)
                            {
                                LogError(
                                    "(ClickIt) Couldn't find remnant of corruption in inventory, make sure you have some.");
                                waitingForCorruption = false;
                                Mouse.blockInput(false);
                                return;
                            }

                            if (Settings.DebugMode)
                            {
                                LogMessage("(ClickIt) Found remnant");
                            }

                            Input.SetCursorPos(remnantOfCorruption.GetClientRectCache.Center +
                                               GameController.Window.GetWindowRectangle().TopLeft);
                            Thread.Sleep((int)(latency + Settings.WaitTimeInMs));

                            if (Settings.LeftHanded)
                            {
                                Mouse.LeftClick();
                            }
                            else
                            {
                                Mouse.RightClick();
                            }

                            Thread.Sleep((int)(latency + Settings.WaitTimeInMs));

                            centerOfLabel = nextLabel?.Label?.GetClientRect().Center
                                            + GameController.Window.GetWindowRectangle().TopLeft
                                            + new Vector2(Random.Next(0, 2), Random.Next(0, 2));
                            Input.SetCursorPos(centerOfLabel.Value);
                            Thread.Sleep((int)(latency + Settings.WaitTimeInMs));

                            if (Settings.LeftHanded)
                            {
                                Mouse.RightClick();
                            }
                            else
                            {
                                Mouse.LeftClick();
                            }

                            Thread.Sleep((int)(latency + Settings.WaitTimeInMs));

                            Keyboard.KeyPress(Settings.OpenInventoryKey);

                            Thread.Sleep((int)(latency + Settings.WaitTimeInMs));

                            Mouse.blockInput(false);
                            waitingForCorruption = false;
                        }).Start();
                    }
                    else
                    {
                        if (Settings.DebugMode)
                        {
                            LogMessage("Not corrupting essence", 5);
                        }

                        if (!waitingForCorruption)
                        {
                            if (Settings.DebugMode)
                            {
                                LogMessage("Clicking essence", 5);
                            }

                            Input.SetCursorPos(centerOfLabel.Value);
                            if (Settings.LeftHanded)
                            {
                                Mouse.RightClick();
                            }
                            else
                            {
                                Mouse.LeftClick();
                            }

                            Mouse.blockInput(false);
                        }
                    }
                }

                else if (Settings.ClickItems && GroundItemsVisible() && !waitingForCorruption)
                {

                    Vector2? centerOfLabel = nextLabel?.Label?.GetClientRect().Center
                                        + GameController.Window.GetWindowRectangle().TopLeft
                                        + new Vector2(Random.Next(0, 2), Random.Next(0, 2));

                    if (nextLabel?.ItemOnGround.Type == EntityType.Chest)
                    {
                        centerOfLabel = nextLabel?.Label?.GetClientRect().Center
                                        + GameController.Window.GetWindowRectangle().TopLeft
                                        + new Vector2(Random.Next(0, 2),
                                            -Random.Next(Settings.ChestHeightOffset, Settings.ChestHeightOffset + 2));
                    }

                    if (!centerOfLabel.HasValue)
                    {
                        if (Settings.DebugMode)
                        {
                            LogMessage("(ClickIt) centerOfLabel has no Value | Cache: " + Settings.CachingEnable.Value);
                        }

                        yield break;
                    }

                    Input.SetCursorPos(centerOfLabel.Value);
                    if (Settings.LeftHanded)
                    {
                        Mouse.RightClick();
                    }
                    else
                    {
                        Mouse.LeftClick();
                    }


                    if (Settings.ToggleItems && Random.Next(0, 20) == 0)
                    {
                        Keyboard.KeyPress(Settings.ToggleItemsHotkey, 20);
                        Keyboard.KeyPress(Settings.ToggleItemsHotkey, 20);
                    }
                    Mouse.blockInput(false);
                }
                else
                {
                    Mouse.blockInput(false);
                }
            }
            catch (Exception e)
            {
                Mouse.blockInput(false);
                waitingForCorruption = false;
                LogError(e.ToString());
            }
        }

        private async Task<List<NormalInventoryItem>> FetchInventoryItemsTask()
        {
            await Task.Run(() =>
            {
            Loopstart:
                try
                {
                    _ = FetchInventoryItems();
                }
                catch (Exception)
                {
                    goto Loopstart;
                }
            });
            return FetchInventoryItems();
        }

        private List<NormalInventoryItem> FetchInventoryItems()
        {
            ClickIt core = this;
            IList<NormalInventoryItem> pullItems = core.GameController.Game.IngameState.IngameUi
                .InventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems;
            return new List<NormalInventoryItem>(pullItems);
        }

        private bool IsBasicChest(LabelOnGround label)
        {
            return label.ItemOnGround.RenderName.ToLower() switch
            {
                "chest" or "tribal chest" or "cocoon" or "weapon rack" or "armour rack" or "trunk" => true,
                _ => false,
            };
        }

        private LabelOnGround GetLabelCaching()
        {
            LabelOnGround label = CachedLabels.Value.Find(x => x.ItemOnGround.DistancePlayer <= Settings.ClickDistance &&
                                                     ((Settings.ClickItems.Value &&
                                                      x.ItemOnGround.Type == EntityType.WorldItem &&
                                                      (!Settings.IgnoreUniques ||
                                                       x.ItemOnGround.GetComponent<WorldItem>()?.ItemEntity
                                                           .GetComponent<Mods>()?.ItemRarity != ItemRarity.Unique ||
                                                       x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path
                                                           .StartsWith("Metadata/Items/Metamorphosis/Metamorphosis"))) ||
                                                      (Settings.ClickBasicChests.Value &&
                                                       x.ItemOnGround.Type == EntityType.Chest && IsBasicChest(x)) ||
                                                      (Settings.ClickLeagueChests.Value &&
                                                       x.ItemOnGround.Type == EntityType.Chest && !IsBasicChest(x)) ||
                                                      (Settings.ClickAreaTransitions.Value &&
                                                      x.ItemOnGround.Type == EntityType.AreaTransition) ||
                                                      (Settings.ClickShrines.Value &&
                                                      x.ItemOnGround.Type == EntityType.Shrine) ||
                                                      (Settings.ClickEssences.Value && GetElementByString(x.Label,
                                                          "The monster is imprisoned by powerful Essences.") != null)));
            return label;
        }

        private LabelOnGround GetLabelNoCaching()
        {
            List<LabelOnGround> list = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.ToList().Where(x =>
                    x.ItemOnGround?.Path != null &&
                    PointIsInClickableArea(x.Label.GetClientRect().Center) &&
                    (x.ItemOnGround.Type == EntityType.WorldItem ||
                     (x.ItemOnGround.Type == EntityType.Chest && !x.ItemOnGround.GetComponent<Chest>().OpenOnDamage) ||
                     x.ItemOnGround.Type == EntityType.AreaTransition ||
                     GetElementByString(x.Label, "The monster is imprisoned by powerful Essences.") != null))
                .OrderBy(x => x.ItemOnGround.DistancePlayer).ToList();


            return list.Find(x => x.ItemOnGround.DistancePlayer <= Settings.ClickDistance &&
                                  ((Settings.ClickItems.Value &&
                                   x.ItemOnGround.Type == EntityType.WorldItem &&
                                   (!Settings.IgnoreUniques ||
                                    x.ItemOnGround.GetComponent<WorldItem>()?.ItemEntity.GetComponent<Mods>()
                                        ?.ItemRarity != ItemRarity.Unique ||
                                    x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path
                                        .StartsWith("Metadata/Items/Metamorphosis/Metamorphosis"))) ||
                                   (Settings.ClickBasicChests.Value && x.ItemOnGround.Type == EntityType.Chest &&
                                    IsBasicChest(x)) ||
                                   (Settings.ClickLeagueChests.Value && x.ItemOnGround.Type == EntityType.Chest &&
                                    !IsBasicChest(x)) ||
                                   (Settings.ClickAreaTransitions.Value &&
                                   x.ItemOnGround.Type == EntityType.AreaTransition) ||
                                   (Settings.ClickEssences.Value && GetElementByString(x.Label,
                                       "The monster is imprisoned by powerful Essences.") != null)));
        }

        public Element GetElementByString(Element label, string str)
        {
            Stopwatch timer = new();
            timer.Start();
            Element element = label.GetText(512) == str
                ? label
                : label.Children.Select(child => GetElementByString(child, str))
                    .FirstOrDefault(element => element != null);
            timer.Stop();

            if (Settings.DebugMode)
            {
                LogMessage("GetElementByString took " + timer.ElapsedMilliseconds + " ms", 5);
            }
            return element;
        }

        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}