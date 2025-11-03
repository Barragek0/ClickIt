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
using System.Text.RegularExpressions;
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

        private Coroutine? altarCoroutine;
        private Coroutine? clickLabelCoroutine;

        private RectangleF FullScreenRectangle { get; set; }
        private RectangleF HealthAndFlaskRectangle { get; set; }
        private RectangleF ManaAndSkillsRectangle { get; set; }
        private RectangleF BuffsAndDebuffsRectangle { get; set; }

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
        }

        public override bool Initialise()
        {
            Settings.ReportBugButton.OnPressed += () => { _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues"); };

            CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, 200);

            FullScreenRectangle = new RectangleF(
                    GameController.Window.GetWindowRectangleTimeCache.TopLeft.X,
                    GameController.Window.GetWindowRectangleTimeCache.TopLeft.Y,
                    GameController.Window.GetWindowRectangleTimeCache.Width,
                    GameController.Window.GetWindowRectangleTimeCache.Height);

            HealthAndFlaskRectangle = new RectangleF(
                    (float)(GameController.Window.GetWindowRectangleTimeCache.BottomLeft.X / 3),
                    (float)(GameController.Window.GetWindowRectangleTimeCache.BottomLeft.Y / 5 * 3.92),
                    (float)(GameController.Window.GetWindowRectangleTimeCache.BottomLeft.X +
                            (GameController.Window.GetWindowRectangleTimeCache.BottomRight.X / 3.4)),
                    GameController.Window.GetWindowRectangleTimeCache.BottomLeft.Y);

            ManaAndSkillsRectangle = new RectangleF(
                    (float)(GameController.Window.GetWindowRectangleTimeCache.BottomRight.X / 3 * 2.12),
                    (float)(GameController.Window.GetWindowRectangleTimeCache.BottomLeft.Y / 5 * 3.92),
                    GameController.Window.GetWindowRectangleTimeCache.BottomRight.X,
                    GameController.Window.GetWindowRectangleTimeCache.BottomRight.Y);

            BuffsAndDebuffsRectangle = new RectangleF(
                    GameController.Window.GetWindowRectangleTimeCache.TopLeft.X,
                    GameController.Window.GetWindowRectangleTimeCache.TopLeft.Y,
                    GameController.Window.GetWindowRectangleTimeCache.TopRight.X / 2,
                    GameController.Window.GetWindowRectangleTimeCache.TopLeft.Y + 120);

            Timer.Start();
            SecondTimer.Start();

            altarCoroutine = new Coroutine(MainScanForAltarsLogic(), this, "ClickIt.ScanForAltarsLogic", false);
            _ = Core.ParallelRunner.Run(altarCoroutine);
            altarCoroutine.Priority = CoroutinePriority.High;

            clickLabelCoroutine = new Coroutine(MainClickLabelCoroutine(), this, "ClickIt.ClickLogic", false);
            _ = Core.ParallelRunner.Run(clickLabelCoroutine);
            clickLabelCoroutine.Priority = CoroutinePriority.High;

            return true;
        }

        private RectangleF FullScreenArea()
        {
            return new RectangleF(GameController.Window.GetWindowRectangleTimeCache.X, GameController.Window.GetWindowRectangleTimeCache.Y, GameController.Window.GetWindowRectangleTimeCache.Width,
                GameController.Window.GetWindowRectangleTimeCache.Height);
        }

        private bool PointIsInClickableArea(Vector2 point, bool debug = false, string? path = null)
        {
            if (Settings.DebugMode && debug)
            {
                LogMessage("Checking if point: x:" + point.X + " y:" + point.Y + " is in rectangle for: " + (path ?? "unknown path"));
            }

            if (Settings.DebugMode && debug)
            {
                if (point.PointInRectangle(HealthAndFlaskRectangle))
                {
                    LogMessage("Point is in orange - health globe / flasks");
                }
                else if (point.PointInRectangle(ManaAndSkillsRectangle))
                {
                    LogMessage("Point is in blue - mana globe / skills");
                }
                else if (point.PointInRectangle(BuffsAndDebuffsRectangle))
                {
                    LogMessage("Point is in blue - mana globe / skills");
                }
            }
            return point.PointInRectangle(FullScreenArea()) &&
                  !point.PointInRectangle(HealthAndFlaskRectangle) &&
                  !point.PointInRectangle(ManaAndSkillsRectangle) &&
                  !point.PointInRectangle(BuffsAndDebuffsRectangle);
        }

        public List<FieldInfo> fields = [];

        public override void Render()
        {
            if (Settings.DebugMode && Settings.RenderDebug)
            {
                Graphics.DrawFrame(FullScreenRectangle, Color.Green, 1);
                Graphics.DrawFrame(HealthAndFlaskRectangle, Color.Orange, 1);
                Graphics.DrawFrame(ManaAndSkillsRectangle, Color.Cyan, 1);
                Graphics.DrawFrame(BuffsAndDebuffsRectangle, Color.Yellow, 1);
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

                if (Settings.DebugMode && Settings.RenderDebug)
                {
                    if (TopUpside1Weight <= 0 && !string.IsNullOrEmpty(altar.TopMods.FirstUpside.ToLower()))
                    {
                        LogError("Could not match top upside 1 with field - " + altar.TopMods.FirstUpside.ToLower() +
                                 "_weight", 10);
                    }

                    if (TopUpside2Weight <= 0 && !string.IsNullOrEmpty(altar.TopMods.SecondUpside.ToLower()))
                    {
                        LogError("Could not match top upside 2 with field - " + altar.TopMods.SecondUpside.ToLower() +
                                 "_weight", 10);
                    }

                    if (TopDownside1Weight <= 0 && !string.IsNullOrEmpty(altar.TopMods.FirstDownside.ToLower()))
                    {
                        LogError("Could not match top downside 1 with field - " + altar.TopMods.FirstDownside.ToLower() +
                                 "_weight", 10);
                    }

                    if (TopDownside2Weight <= 0 && !string.IsNullOrEmpty(altar.TopMods.SecondDownside.ToLower()))
                    {
                        LogError("Could not match top downside 2 with field - " + altar.TopMods.SecondDownside.ToLower() +
                                 "_weight", 10);
                    }

                    if (BottomUpside1Weight <= 0 && !string.IsNullOrEmpty(altar.BottomMods.FirstUpside.ToLower()))
                    {
                        LogError("Could not match bottom upside 1 with field - " + altar.BottomMods.FirstUpside.ToLower() +
                                 "_weight", 10);
                    }

                    if (BottomUpside2Weight <= 0 && !string.IsNullOrEmpty(altar.BottomMods.SecondUpside.ToLower()))
                    {
                        LogError("Could not match bottom upside 2 with field - " + altar.BottomMods.SecondUpside.ToLower() +
                                 "_weight", 10);
                    }

                    if (BottomDownside1Weight <= 0 && !string.IsNullOrEmpty(altar.BottomMods.FirstDownside.ToLower()))
                    {
                        LogError("Could not match bottom downside 1 with field - " +
                                 altar.BottomMods.FirstDownside.ToLower() + "_weight", 10);
                    }

                    if (BottomDownside2Weight <= 0 && !string.IsNullOrEmpty(altar.BottomMods.SecondDownside.ToLower()))
                    {
                        LogError("Could not match bottom downside 2 with field - " +
                                 altar.BottomMods.SecondDownside.ToLower() + "_weight", 10);
                    }
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
                    boxToClick != null && PointIsInClickableArea(boxToClick.GetClientRect().Center, true, altar.AltarType.ToString()))
                {
                    if (boxToClick.IsVisible)
                    {
                        if (canClick())
                        {
                            Mouse.blockInput(true);
                            LogMessage("Moving mouse for altar", 5);
                            Input.SetCursorPos(boxToClick.GetClientRect().Center +
                                               GameController.Window.GetWindowRectangleTimeCache.TopLeft);
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

        private bool canClick()
        {
            return Input.GetKeyState(Settings.ClickLabelKey.Value) && IsPOEActive() && (!Settings.BlockOnOpenLeftRightPanel || !IsPanelOpen())
                    && !InTownOrHideout() && !waitingForCorruption && !GameController.IngameState.IngameUi.ChatTitlePanel.IsVisible;
        }

        private IEnumerator ScanForAltarsLogic()
        {
            List<LabelOnGround> altarLabels = [];
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
                                    LogError("Part of altarcomponent is null", 10);
                                    LogError("part1: " + altarComponent.TopMods, 10);
                                    LogError("part2: " + altarComponent.TopButton, 10);
                                    LogError("part3: " + altarComponent.BottomMods, 10);
                                    LogError("part4: " + altarComponent.BottomButton, 10);
                                }
                            }
                        }
                        else
                        {
                            LogError("Element is null", 10);
                        }
                    }
                }
            }
            else
            {
                altarComponents.Clear();
            }
            //just to make sure this list is refreshed, it should be anyway.
            altarLabels.Clear();
            //pause to save resources
            altarCoroutine.Pause();
            yield break;
        }

        private bool IsPOEActive()
        {
            if (!GameController.Window.IsForeground())
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
            if (CachedLabels.Value.Count < 1)
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

        private static readonly List<Element> elementsByStringContainsList = [];

        public List<Element> GetElementsByStringContains(Element label, string str)
        {
            elementsByStringContainsList.Clear();
            if (label != null)
            {
                if (label.GetText(512) != null && label.GetText(512).Contains(str))
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
            }

            return elementsByStringContainsList;
        }

        private readonly List<PrimaryAltarComponent> altarComponents = [];

        private string GetLine(string text, int lineNo)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length >= lineNo ? lines[lineNo] : "ERROR: Could not read line.";
        }

        private int CountLines(string text)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length;
        }

        private bool workFinished;

        public override Job? Tick()
        {
            if (Input.GetKeyState(Settings.ClickLabelKey.Value))
            {

                if (clickLabelCoroutine.IsDone)
                {
                    Coroutine firstOrDefault = Core.ParallelRunner.Coroutines.FirstOrDefault(x => x.Name == "ClickIt.ClickLogic");

                    if (firstOrDefault != null)
                    {
                        clickLabelCoroutine = firstOrDefault;
                    }
                }

                clickLabelCoroutine.Resume();
                workFinished = false;
            }
            else
            {
                if (workFinished)
                {
                    clickLabelCoroutine.Pause();
                }
            }
            if (SecondTimer.ElapsedMilliseconds > 500)
            {
                altarCoroutine.Resume();
                SecondTimer.Restart();
            }
            return null;
        }

        // we need these here to keep the coroutines alive after finishing the work
        private IEnumerator MainClickLabelCoroutine()
        {
            while (true)
            {
                yield return ClickLabel();
            }
        }

        private IEnumerator MainScanForAltarsLogic()
        {
            while (true)
            {
                yield return ScanForAltarsLogic();
            }
        }

        private void updateComponentFromElementData(bool top, Element altarParent, PrimaryAltarComponent altarComponent,
            Element ElementToExtractDataFrom, AltarType altarType)
        {
            string NegativeModType = "";
            List<string> mods = [];
            List<string> upsides = [];
            List<string> downsides = [];
            if (Settings.DebugMode)
            {
                LogMessage(ElementToExtractDataFrom.GetText(512));
            }

            string AltarMods = ElementToExtractDataFrom.GetText(512).Replace("<valuedefault>", "").Replace("{", "")
                .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "").Replace("gain:", "")
                .Replace("gains:", "");

            AltarMods = Regex.Replace(AltarMods, @"<rgb\(\d+,\d+,\d+\)>", "");

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
                                localmod, 10);
                        }
                    }
                    catch (Exception)
                    {
                        if (Settings.DebugMode)
                        {
                            LogError(
                                "updateComponentFromElementData: Failed to match mod with field? Field may not be required - unable to write field, sequence contains no elements", 10);
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
            List<LabelOnGround> list = CachedLabels.Value.Where(x =>
                x.ItemOnGround.Path != null
                && PointIsInClickableArea(x.Label.GetClientRect().Center)
            ).ToList();

            return list.FindAll(x => x.ItemOnGround.Path.Contains("Harvest/Irrigator") || x.ItemOnGround.Path.Contains("Harvest/Extractor")
                ).OrderBy(x => x.ItemOnGround.DistancePlayer).ToList();
        }

        private List<LabelOnGround> GetAltarLabels(AltarType type)
        {
            List<LabelOnGround> list = CachedLabels.Value.Where(x =>
                x.ItemOnGround.Path != null && x.Label.IsVisible
                && x.Label.GetClientRect().Center.PointInRectangle(FullScreenArea())
            ).ToList();

            return list.FindAll(x =>
                x.ItemOnGround.Path.Contains(type == AltarType.SearingExarch ? "CleansingFireAltar" : "TangleAltar"));
        }

        private List<LabelOnGround> UpdateLabelComponent()
        {
            IList<LabelOnGround> list = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabels;
            return list
                .Where(x =>
                    x.ItemOnGround?.Path != null &&
                    x.IsVisible &&
                    x.Label.IsVisible &&
                    PointIsInClickableArea(x.Label.GetClientRect().Center) &&
                    (x.ItemOnGround.Type == EntityType.WorldItem ||
                    (x.ItemOnGround.Type == EntityType.Chest && !x.ItemOnGround.GetComponent<Chest>().OpenOnDamage) ||
                    x.ItemOnGround.Type == EntityType.AreaTransition ||
                    GetElementByString(x.Label, "The monster is imprisoned by powerful Essences.") != null ||
                    x.ItemOnGround.Path.Contains("DelveMineral") ||
                    x.ItemOnGround.Path.Contains("AzuriteEncounterController") ||
                    x.ItemOnGround.Path.Contains("Harvest/Irrigator") || x.ItemOnGround.Path.Contains("Harvest/Extractor") ||
                    x.ItemOnGround.Path.Contains("CleansingFireAltar") || x.ItemOnGround.Path.Contains("TangleAltar") ||
                    x.ItemOnGround.Path.Contains("CraftingUnlocks")
                    ))
                .OrderBy(x => x.ItemOnGround.DistancePlayer)
                .ToList();
        }

        private IEnumerator ClickLabel(Element? altar = null)
        {
            Stopwatch ClickLabelTimer = Stopwatch.StartNew();
            bool isDebugMode = Settings.DebugMode;

            try
            {
                if (Timer.ElapsedMilliseconds < 50 + Random.Next(0, 10) || !canClick())
                {
                    workFinished = true;
                    yield break;
                }

                Timer.Restart();

                Vector2 windowTopLeft = GameController.Window.GetWindowRectangleTimeCache.TopLeft;

                if (Settings.BlockUserInput)
                {
                    Mouse.blockInput(true);
                }

                Entity? shrine = GetShrine();

                Stopwatch timer = Stopwatch.StartNew();
                LabelOnGround? nextLabel = GetCachedLabels();
                timer.Stop();

                if (isDebugMode)
                {
                    if (timer.ElapsedMilliseconds > 10)
                    {
                        LogMessage("Collecting ground labels took " + timer.ElapsedMilliseconds + " ms", 5);
                    }
                }

                // Process Harvest Labels
                if (Settings.NearestHarvest)
                {
                    List<LabelOnGround> harvestLabels = GetHarvestLabels();
                    if (harvestLabels.Count > 0)
                    {
                        LabelOnGround harvestLabel = harvestLabels.FirstOrDefault();
                        if (harvestLabel != null && harvestLabel.IsVisible)
                        {
                            Vector2 clickPos = harvestLabel.Label.GetClientRect().Center + windowTopLeft;
                            Input.SetCursorPos(clickPos);
                            if (Settings.LeftHanded)
                            {
                                Mouse.RightClick();
                            }
                            else
                            {
                                Mouse.LeftClick();
                            }

                            Mouse.blockInput(false);
                            workFinished = true;
                            yield break;
                        }
                    }
                }

                // Process Shrine
                if (Settings.ClickShrines && shrine != null && !waitingForCorruption)
                {
                    Vector2 shrinePos = GameController.Game.IngameState.Camera.WorldToScreen(shrine.Pos.Translate(0, 0, 0));
                    RectangleF window = GameController.Window.GetWindowRectangleTimeCache;

                    if (shrinePos.X > window.TopLeft.X &&
                        shrinePos.X < window.TopRight.X &&
                        shrinePos.Y > window.TopLeft.Y + 150 &&
                        shrinePos.Y < window.BottomLeft.Y - 275)
                    {
                        Input.SetCursorPos(shrinePos);
                        if (Settings.LeftHanded)
                        {
                            Mouse.RightClick();
                        }
                        else
                        {
                            Mouse.LeftClick();
                        }

                        Mouse.blockInput(false);
                        workFinished = true;
                        yield break;
                    }
                }

                if (nextLabel == null || !GroundItemsVisible())
                {
                    Mouse.blockInput(false);
                    workFinished = true;
                    yield break;
                }

                // Cache commonly accessed values
                Element labelElement = nextLabel.Label;
                bool isEssence = Settings.ClickEssences && GetElementByString(labelElement, "The monster is imprisoned by powerful Essences.") != null;
                bool isSulphite = Settings.ClickSulphiteVeins && GetElementByString(labelElement, "Interact to acquire Voltaxic Sulphite") != null;

                // Handle Sulphite
                if (isSulphite)
                {
                    Vector2 clickPos = labelElement.GetClientRect().Center;
                    Input.SetCursorPos(clickPos);
                    if (Settings.LeftHanded)
                    {
                        Mouse.RightClick();
                    }
                    else
                    {
                        Mouse.LeftClick();
                    }

                    Mouse.blockInput(false);
                    workFinished = true;
                    yield break;
                }

                // Handle Essence
                if (isEssence)
                {
                    ProcessEssenceLabel(nextLabel, windowTopLeft);
                    workFinished = true;
                    yield break;
                }

                // Handle Crafting Recipes
                if (nextLabel.ItemOnGround.Path.Contains("CraftingUnlocks") && Settings.ClickCraftingRecipes)
                {
                    Vector2 clickPos = labelElement.GetClientRect().Center;
                    Input.SetCursorPos(clickPos);
                    if (Settings.LeftHanded)
                    {
                        Mouse.RightClick();
                    }
                    else
                    {
                        Mouse.LeftClick();
                    }

                    Mouse.blockInput(false);
                    workFinished = true;
                    yield break;
                }

                // Handle regular items
                if (!waitingForCorruption)
                {
                    if (!PointIsInClickableArea(labelElement.GetClientRect().Center + windowTopLeft, isDebugMode, nextLabel.ItemOnGround.Path))
                    {
                        if (isDebugMode)
                        {
                            LogMessage("(ClickIt) nextLabel is not in clickable area");
                        }

                        workFinished = true;
                        yield break;
                    }

                    // Skip altar handling as it's done in Render()
                    if (nextLabel.ItemOnGround.Path.Contains("CleansingFireAltar") || nextLabel.ItemOnGround.Path.Contains("TangleAltar"))
                    {
                        workFinished = true;
                        yield break;
                    }

                    if (!(Settings.ClickSulphiteVeins && nextLabel.ItemOnGround.Path.Contains("DelveMineral")) &&
                        !(Settings.ClickAzuriteVeins && nextLabel.ItemOnGround.Path.Contains("AzuriteEncounterController")) &&
                        !(Settings.ClickCraftingRecipes && nextLabel.ItemOnGround.Path.Contains("CraftUnlocks")) &&
                        !Settings.ClickItems)
                    {
                        workFinished = true;
                        yield break;
                    }

                    Vector2 offset = new(Random.Next(0, 5), nextLabel.ItemOnGround.Type == EntityType.Chest ?
                        -Random.Next(Settings.ChestHeightOffset, Settings.ChestHeightOffset + 2) :
                        Random.Next(0, 5));

                    Vector2 clickPos = labelElement.GetClientRect().Center + windowTopLeft + offset;
                    Input.SetCursorPos(clickPos);

                    if (Settings.LeftHanded)
                    {
                        Mouse.RightClick();
                    }
                    else
                    {
                        Mouse.LeftClick();
                    }

                    if (Settings.ToggleItems && Random.Next(0, 10) == 0)
                    {
                        Keyboard.KeyPress(Settings.ToggleItemsHotkey, 20);
                        Keyboard.KeyPress(Settings.ToggleItemsHotkey, 20);
                    }
                }

                Mouse.blockInput(false);
                workFinished = true;
                yield break;
            }
            catch (Exception e)
            {
                Mouse.blockInput(false);
                workFinished = true;
                waitingForCorruption = false;
                if (Settings.DebugMode)
                {
                    LogError(e.ToString(), 10);
                }
                yield break;
            }
            finally
            {
                ClickLabelTimer.Stop();
                if (isDebugMode && ClickLabelTimer.ElapsedMilliseconds > 10)
                {
                    LogMessage("ClickLabel took " + ClickLabelTimer.ElapsedMilliseconds + " ms", 5);
                }
            }
        }

        private void ProcessEssenceLabel(LabelOnGround nextLabel, Vector2 windowTopLeft)
        {
            Element label = nextLabel.Label.Parent;
            bool meetsCorruptCriteria = false;

            if (GetElementByString(label, "Corrupted") == null)
            {
                if (Settings.CorruptAllEssences)
                {
                    meetsCorruptCriteria = true;
                }
                else if (Settings.CorruptMEDSEssences)
                {
                    meetsCorruptCriteria = GetElementByString(label, "Screaming Essence of Misery") != null ||
                                          GetElementByString(label, "Screaming Essence of Envy") != null ||
                                          GetElementByString(label, "Screaming Essence of Dread") != null ||
                                          GetElementByString(label, "Screaming Essence of Scorn") != null ||
                                          GetElementByString(label, "Shrieking Essence of Misery") != null ||
                                          GetElementByString(label, "Shrieking Essence of Envy") != null ||
                                          GetElementByString(label, "Shrieking Essence of Dread") != null ||
                                          GetElementByString(label, "Shrieking Essence of Scorn") != null;
                }
                else if (Settings.CorruptProfitableEssences)
                {
                    meetsCorruptCriteria = GetElementByString(label, "Shrieking Essence of Contempt") != null ||
                                          GetElementByString(label, "Shrieking Essence of Woe") != null ||
                                          GetElementByString(label, "Shrieking Essence of Sorrow") != null ||
                                          GetElementByString(label, "Shrieking Essence of Loathing") != null ||
                                          GetElementByString(label, "Shrieking Essence of Zeal") != null ||
                                          GetElementByString(label, "Shrieking Essence of Envy") != null;
                }
                else if (Settings.CorruptAnyNonShrieking)
                {
                    meetsCorruptCriteria = !CheckForAnyShriekingEssence(label);
                }
            }

            if (meetsCorruptCriteria)
            {
                StartEssenceCorruption(nextLabel, windowTopLeft);
            }
            else if (!waitingForCorruption)
            {
                Vector2 clickPos = nextLabel.Label.GetClientRect().Center + windowTopLeft;
                Input.SetCursorPos(clickPos);
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

        private bool CheckForAnyShriekingEssence(Element label)
        {
            return GetElementByString(label, "Shrieking Essence of Greed") != null ||
                   GetElementByString(label, "Shrieking Essence of Contempt") != null ||
                   GetElementByString(label, "Shrieking Essence of Hatred") != null ||
                   GetElementByString(label, "Shrieking Essence of Woe") != null ||
                   GetElementByString(label, "Shrieking Essence of Fear") != null ||
                   GetElementByString(label, "Shrieking Essence of Anger") != null ||
                   GetElementByString(label, "Shrieking Essence of Torment") != null ||
                   GetElementByString(label, "Shrieking Essence of Sorrow") != null ||
                   GetElementByString(label, "Shrieking Essence of Rage") != null ||
                   GetElementByString(label, "Shrieking Essence of Suffering") != null ||
                   GetElementByString(label, "Shrieking Essence of Wrath") != null ||
                   GetElementByString(label, "Shrieking Essence of Doubt") != null ||
                   GetElementByString(label, "Shrieking Essence of Loathing") != null ||
                   GetElementByString(label, "Shrieking Essence of Zeal") != null ||
                   GetElementByString(label, "Shrieking Essence of Anguish") != null ||
                   GetElementByString(label, "Shrieking Essence of Spite") != null ||
                   GetElementByString(label, "Shrieking Essence of Scorn") != null ||
                   GetElementByString(label, "Shrieking Essence of Envy") != null ||
                   GetElementByString(label, "Shrieking Essence of Misery") != null ||
                   GetElementByString(label, "Shrieking Essence of Dread") != null;
        }

        private void StartEssenceCorruption(LabelOnGround nextLabel, Vector2 windowTopLeft)
        {
            if (Settings.DebugMode)
            {
                LogMessage("Essence is not corrupted and meets criteria, lets corrupt it", 5);
                LogMessage("Starting corruption", 5);
            }

            waitingForCorruption = true;
            new Thread(() =>
            {
                LogMessage("Corruption started", 5);
                float latency = GameController.Game.IngameState.ServerData.Latency;

                if (nextLabel?.Label?.GetChildAtIndex(2)?.GetChildAtIndex(0)?.GetChildAtIndex(0) == null)
                {
                    LogError("(ClickIt) You need vaal orbs in your inventory to corrupt essences.", 20);
                    waitingForCorruption = false;
                    Mouse.blockInput(false);
                    workFinished = true;
                    return;
                }

                Vector2 offset = new(Random.Next(0, 2), Random.Next(0, 2));
                Vector2 centerOfLabel = nextLabel.Label.GetChildAtIndex(2).GetChildAtIndex(0).GetChildAtIndex(0).GetClientRect().Center + windowTopLeft + offset;

                LogMessage("Moved mouse to vaal", 5);
                Input.SetCursorPos(centerOfLabel);

                Thread.Sleep((int)(latency + 100));

                if (Settings.LeftHanded)
                {
                    Mouse.RightClick();
                }
                else
                {
                    Mouse.LeftClick();
                }

                Thread.Sleep((int)(latency + 100));

                Mouse.blockInput(false);
                waitingForCorruption = false;
            }).Start();
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
            IList<NormalInventoryItem> pullItems = GameController.Game.IngameState.IngameUi
                .InventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems;
            return [.. pullItems];
        }

        private bool IsBasicChest(LabelOnGround label)
        {
            return label.ItemOnGround.RenderName.ToLower() switch
            {
                "chest" or "tribal chest" or "golden chest" or "cocoon" or "weapon rack" or "armour rack" or "trunk" => true,
                _ => false,
            };
        }

        private LabelOnGround? GetCachedLabels()
        {
            List<LabelOnGround>? cached = CachedLabels?.Value;
            if (cached == null || cached.Count == 0)
            {
                return null;
            }

            ClickItSettings s = Settings;
            RangeNode<int> clickDistance = s.ClickDistance;
            bool clickItems = s.ClickItems.Value;
            bool ignoreUniques = s.IgnoreUniques;
            bool clickBasicChests = s.ClickBasicChests.Value;
            bool clickLeagueChests = s.ClickLeagueChests.Value;
            bool clickAreaTransitions = s.ClickAreaTransitions.Value;
            bool clickShrines = s.ClickShrines.Value;
            bool nearestHarvest = s.NearestHarvest.Value;
            bool clickSulphite = s.ClickSulphiteVeins.Value;
            bool clickAzurite = s.ClickAzuriteVeins.Value;
            bool highlightEater = s.HighlightEaterAltars.Value;
            bool highlightExarch = s.HighlightExarchAltars.Value;
            bool clickEater = s.ClickEaterAltars.Value;
            bool clickExarch = s.ClickExarchAltars.Value;
            bool clickEssences = s.ClickEssences.Value;
            bool clickCrafting = s.ClickCraftingRecipes.Value;

            for (int i = 0; i < cached.Count; i++)
            {
                LabelOnGround label = cached[i];
                Entity item = label.ItemOnGround;
                if (item == null)
                {
                    continue;
                }

                if (item.DistancePlayer > clickDistance)
                {
                    continue;
                }

                string path = item.Path;
                EntityType type = item.Type;

                // World items
                if (clickItems && type == EntityType.WorldItem)
                {
                    if (ignoreUniques)
                    {
                        try
                        {
                            WorldItem worldItemComp = item.GetComponent<WorldItem>();
                            Entity? itemEntity = worldItemComp?.ItemEntity;
                            Mods? mods = itemEntity?.GetComponent<Mods>();
                            if (mods?.ItemRarity == ItemRarity.Unique && !(itemEntity?.Path?.StartsWith("Metadata/Items/Metamorphosis/") ?? false))
                            {
                                continue; // skip uniques when ignoring
                            }
                        }
                        catch
                        {
                            // ignore exceptions and treat as not-unique
                        }
                    }

                    return label;
                }

                // Chests
                if (type == EntityType.Chest)
                {
                    if (clickBasicChests && IsBasicChest(label))
                    {
                        return label;
                    }

                    if (clickLeagueChests && !IsBasicChest(label))
                    {
                        return label;
                    }
                }

                // Area transitions
                if (clickAreaTransitions && type == EntityType.AreaTransition)
                {
                    return label;
                }

                // Shrines
                if (clickShrines && type == EntityType.Shrine)
                {
                    return label;
                }

                // Harvest
                if (nearestHarvest && !string.IsNullOrEmpty(path) && (path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor")))
                {
                    return label;
                }

                // Sulphite
                if (clickSulphite && !string.IsNullOrEmpty(path) && path.Contains("DelveMineral"))
                {
                    return label;
                }

                // Azurite
                if (clickAzurite && !string.IsNullOrEmpty(path) && path.Contains("AzuriteEncounterController"))
                {
                    return label;
                }

                // Altars
                if ((highlightEater || highlightExarch || clickEater || clickExarch) && !string.IsNullOrEmpty(path) && (path.Contains("CleansingFireAltar") || path.Contains("TangleAltar")))
                {
                    return label;
                }

                // Essences
                if (clickEssences && GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null)
                {
                    return label;
                }

                // Crafting recipes
                if (clickCrafting && !string.IsNullOrEmpty(path) && path.Contains("CraftingUnlocks"))
                {
                    return label;
                }
            }

            return null;
        }

        // Optimized: iterative DFS, nullable return, avoid LINQ and recursion
        public Element? GetElementByString(Element? root, string str)
        {
            var sw = Stopwatch.StartNew();

            if (root == null)
            {
                sw.Stop();
                if (Settings.DebugMode && sw.ElapsedMilliseconds > 10)
                    LogMessage("GetElementByString took " + sw.ElapsedMilliseconds + " ms", 5);
                return null;
            }

            Element? found = null;
            var stack = new Stack<Element>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var el = stack.Pop();
                try
                {
                    var text = el.GetText(512);
                    if (text != null && text.Equals(str))
                    {
                        found = el;
                        break;
                    }

                    var children = el.Children;
                    if (children != null)
                    {
                        // push children onto stack
                        foreach (var c in children)
                        {
                            if (c != null)
                                stack.Push(c);
                        }
                    }
                }
                catch
                {
                    // ignore read errors and continue
                }
            }

            sw.Stop();
            if (Settings.DebugMode && sw.ElapsedMilliseconds > 10)
            {
                LogMessage("GetElementByString took " + sw.ElapsedMilliseconds + " ms", 5);
            }

            return found;
        }

        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
