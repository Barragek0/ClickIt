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

        private bool PointIsInClickableArea(Vector2 point, string? path = null)
        {
            /*LogMessage("Checking if point: x:" + point.X + " y:" + point.Y + " is in rectangle for: " + (path ?? "unknown path"));

            LogMessage(point.PointInRectangle(HealthAndFlaskRectangle)
                ? "Point is in orange - health globe / flasks"
                : point.PointInRectangle(ManaAndSkillsRectangle) ? "Point is in blue - mana globe / skills"
                : point.PointInRectangle(BuffsAndDebuffsRectangle) ? "Point is in blue - mana globe / skills"
                : string.Empty);*/
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

            // Ensure fields are cached only once
            if (fields.Count == 0)
            {
                fields.AddRange(typeof(ClickItSettings).GetFields(BindingFlags.Public |
                                                                      BindingFlags.NonPublic |
                                                                      BindingFlags.Instance |
                                                                      BindingFlags.Static));
            }

            // Take a snapshot to avoid enumeration issues if the collection is modified elsewhere
            List<PrimaryAltarComponent> altarSnapshot = altarComponents.ToList();

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                // local weight variables
                decimal TopUpside1Weight = 0, TopUpside2Weight = 0;
                decimal TopDownside1Weight = 0, TopDownside2Weight = 0;
                decimal BottomUpside1Weight = 0, BottomUpside2Weight = 0;
                decimal BottomDownside1Weight = 0, BottomDownside2Weight = 0;

                LogMessage(Settings.RenderDebug, "Render 1");

                LogMessage(Settings.RenderDebug, "Render 2");

                // Build lookup names once to avoid repeated allocations / ToLower calls in the inner loop
                string topFirstUpsideName = string.IsNullOrEmpty(altar.TopMods.FirstUpside) ? string.Empty : (altar.TopMods.FirstUpside + "_weight").ToLowerInvariant();
                string topSecondUpsideName = string.IsNullOrEmpty(altar.TopMods.SecondUpside) ? string.Empty : (altar.TopMods.SecondUpside + "_weight").ToLowerInvariant();
                string topFirstDownName = string.IsNullOrEmpty(altar.TopMods.FirstDownside) ? string.Empty : (altar.TopMods.FirstDownside + "_weight").ToLowerInvariant();
                string topSecondDownName = string.IsNullOrEmpty(altar.TopMods.SecondDownside) ? string.Empty : (altar.TopMods.SecondDownside + "_weight").ToLowerInvariant();

                string botFirstUpsideName = string.IsNullOrEmpty(altar.BottomMods.FirstUpside) ? string.Empty : (altar.BottomMods.FirstUpside + "_weight").ToLowerInvariant();
                string botSecondUpsideName = string.IsNullOrEmpty(altar.BottomMods.SecondUpside) ? string.Empty : (altar.BottomMods.SecondUpside + "_weight").ToLowerInvariant();
                string botFirstDownName = string.IsNullOrEmpty(altar.BottomMods.FirstDownside) ? string.Empty : (altar.BottomMods.FirstDownside + "_weight").ToLowerInvariant();
                string botSecondDownName = string.IsNullOrEmpty(altar.BottomMods.SecondDownside) ? string.Empty : (altar.BottomMods.SecondDownside + "_weight").ToLowerInvariant();

                // single traversal of fields
                foreach (FieldInfo field in fields)
                {
                    string FieldName = field.Name.Replace("<", "").Replace(">", "").Replace("k__BackingField", "");
                    string FieldNameLower = FieldName.ToLowerInvariant();

                    LogMessage(Settings.RenderDebug, "Render 3");

                    try
                    {
                        // top upsides
                        if (!string.IsNullOrEmpty(topFirstUpsideName) && FieldNameLower.Equals(topFirstUpsideName, StringComparison.Ordinal))
                        {
                            LogMessage(Settings.RenderDebug, "Render 4-1-1 - " + topFirstUpsideName + " = " + ((RangeNode<int>)field.GetValue(Settings)).Value);

                            TopUpside1Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(topSecondUpsideName) && FieldNameLower.Equals(topSecondUpsideName, StringComparison.Ordinal))
                        {
                            LogMessage(Settings.RenderDebug, "Render 4-1-2 - " + topSecondUpsideName);

                            TopUpside2Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            continue;
                        }

                        // top downsides
                        if (!string.IsNullOrEmpty(topFirstDownName) && FieldNameLower.Equals(topFirstDownName, StringComparison.Ordinal))
                        {
                            TopDownside1Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(topSecondDownName) && FieldNameLower.Equals(topSecondDownName, StringComparison.Ordinal))
                        {
                            TopDownside2Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            continue;
                        }

                        // bottom upsides
                        if (!string.IsNullOrEmpty(botFirstUpsideName) && FieldNameLower.Equals(botFirstUpsideName, StringComparison.Ordinal))
                        {
                            BottomUpside1Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(botSecondUpsideName) && FieldNameLower.Equals(botSecondUpsideName, StringComparison.Ordinal))
                        {
                            BottomUpside2Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            continue;
                        }

                        // bottom downsides
                        if (!string.IsNullOrEmpty(botFirstDownName) && FieldNameLower.Equals(botFirstDownName, StringComparison.Ordinal))
                        {
                            BottomDownside1Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(botSecondDownName) && FieldNameLower.Equals(botSecondDownName, StringComparison.Ordinal))
                        {
                            BottomDownside2Weight = ((RangeNode<int>)field.GetValue(Settings)).Value;
                            continue;
                        }

                        // if none matched, log debug info as original behaviour
                        LogMessage(Settings.RenderDebug, "Render 4-5-1: " + altar.TopMods.FirstUpside + "_Weight");
                        LogMessage(Settings.RenderDebug, "Render 4-5-2: " + altar.TopMods.SecondUpside + "_Weight");
                        LogMessage(Settings.RenderDebug, "Render 4-5-3: " + altar.TopMods.FirstDownside + "_Weight");
                        LogMessage(Settings.RenderDebug, "Render 4-5-4: " + altar.TopMods.SecondDownside + "_Weight");
                        LogMessage(Settings.RenderDebug, "Render 4-5-5: " + altar.BottomMods.FirstUpside + "_Weight");
                        LogMessage(Settings.RenderDebug, "Render 4-5-6: " + altar.BottomMods.SecondUpside + "_Weight");
                        LogMessage(Settings.RenderDebug, "Render 4-5-7: " + altar.BottomMods.FirstDownside + "_Weight");
                        LogMessage(Settings.RenderDebug, "Render 4-5-8: " + altar.BottomMods.SecondDownside + "_Weight");
                    }
                    catch (Exception ex)
                    {
                        LogError(Settings.DebugMode, "Render: error reading setting field '" + field.Name + "': " + ex.Message, 10);
                    }
                }

                // compute weights once after scanning fields
                decimal TopUpsideWeight = TopUpside1Weight + TopUpside2Weight;
                decimal TopDownsideWeight = TopDownside1Weight + TopDownside2Weight;
                decimal BottomUpsideWeight = BottomUpside1Weight + BottomUpside2Weight;
                decimal BottomDownsideWeight = BottomDownside1Weight + BottomDownside2Weight;

                LogMessage(Settings.RenderDebug, "Render 9 - top - " + TopWeightPlaceholder() + " = " + TopUpsideWeight + " / " + TopDownsideWeight);

                decimal TopWeight = Math.Round((TopUpsideWeight <= 0 ? 1 : TopUpsideWeight) / (TopDownsideWeight <= 0 ? 1 : TopDownsideWeight), 2);

                LogMessage(Settings.RenderDebug, "Render 9 - bot - " + BottomWeightPlaceholder() + " = " + BottomUpsideWeight + " / " + BottomDownsideWeight);

                decimal BottomWeight = Math.Round((BottomUpsideWeight <= 0 ? 1 : BottomUpsideWeight) / (BottomDownsideWeight <= 0 ? 1 : BottomDownsideWeight), 2);

                // preserve original debug error checks
                LogError(Settings.RenderDebug, "Could not match top upside 1 with field - " + altar.TopMods.FirstUpside.ToLower() + "_weight", 10);
                LogError(Settings.RenderDebug, "Could not match top upside 2 with field - " + altar.TopMods.SecondUpside.ToLower() + "_weight", 10);
                LogError(Settings.RenderDebug, "Could not match top downside 1 with field - " + altar.TopMods.FirstDownside.ToLower() + "_weight", 10);
                LogError(Settings.RenderDebug, "Could not match top downside 2 with field - " + altar.TopMods.SecondDownside.ToLower() + "_weight", 10);
                LogError(Settings.RenderDebug, "Could not match bottom upside 1 with field - " + altar.BottomMods.FirstUpside.ToLower() + "_weight", 10);
                LogError(Settings.RenderDebug, "Could not match bottom upside 2 with field - " + altar.BottomMods.SecondUpside.ToLower() + "_weight", 10);
                LogError(Settings.RenderDebug, "Could not match bottom downside 1 with field - " + altar.BottomMods.FirstDownside.ToLower() + "_weight", 10);
                LogError(Settings.RenderDebug, "Could not match bottom downside 2 with field - " + altar.BottomMods.SecondDownside.ToLower() + "_weight", 10);

                Element? boxToClick = null;

                // original decision tree kept intact, using the cached weight values
                if (TopUpsideWeight <= 0)
                {
                    _ = Graphics.DrawText("Top upside weights couldn't be recognised " +
                        "\n1:" + (string.IsNullOrEmpty(altar.TopMods.FirstUpside) ? "null" : string.IsNullOrEmpty(altar.TopMods.FirstUpside).ToString()) +
                        "\n2:" + (string.IsNullOrEmpty(altar.TopMods.SecondUpside) ? "null" : string.IsNullOrEmpty(altar.TopMods.SecondUpside).ToString()) +
                        "\nPlease report this as a bug on github",
                        altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.Orange, 30);
                    Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.Yellow, 2);
                    Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.Yellow, 2);
                }
                else if (TopDownsideWeight <= 0)
                {
                    _ = Graphics.DrawText("Top downside weights couldn't be recognised " +
                        "\n1:" + (string.IsNullOrEmpty(altar.TopMods.FirstDownside) ? "null" : string.IsNullOrEmpty(altar.TopMods.FirstDownside).ToString()) +
                        "\n2:" + (string.IsNullOrEmpty(altar.TopMods.SecondDownside) ? "null" : string.IsNullOrEmpty(altar.TopMods.SecondDownside).ToString()) +
                        "\nPlease report this as a bug on github",
                        altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.Orange, 30);
                    Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.Yellow, 2);
                    Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.Yellow, 2);
                }
                else if (BottomUpsideWeight <= 0)
                {
                    _ = Graphics.DrawText("Bottom upside weights couldn't be recognised " +
                        "\n1:" + (string.IsNullOrEmpty(altar.BottomMods.FirstUpside) ? "null" : string.IsNullOrEmpty(altar.BottomMods.FirstUpside).ToString()) +
                        "\n2:" + (string.IsNullOrEmpty(altar.BottomMods.SecondUpside) ? "null" : string.IsNullOrEmpty(altar.BottomMods.SecondUpside).ToString()) +
                        "\nPlease report this as a bug on github",
                        altar.TopMods.Element.GetClientRect().TopLeft + new Vector2(120, -60), Color.Orange, 30);
                    Graphics.DrawFrame(altar.TopMods.Element.GetClientRect(), Color.Yellow, 2);
                    Graphics.DrawFrame(altar.BottomMods.Element.GetClientRect(), Color.Yellow, 2);
                }
                else if (BottomDownsideWeight <= 0)
                {
                    _ = Graphics.DrawText("Bottom downside weights couldn't be recognised " +
                        "\n1:" + (string.IsNullOrEmpty(altar.BottomMods.FirstDownside) ? "null" : string.IsNullOrEmpty(altar.BottomMods.FirstDownside).ToString()) +
                        "\n2:" + (string.IsNullOrEmpty(altar.BottomMods.SecondDownside) ? "null" : string.IsNullOrEmpty(altar.BottomMods.SecondDownside).ToString()) +
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
                    boxToClick != null && PointIsInClickableArea(boxToClick.GetClientRect().Center, altar.AltarType.ToString()))
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

        // small helpers used only for debug messages (avoid unused variable warnings)
        private string TopWeightPlaceholder()
        {
            return string.Empty;
        }

        private string BottomWeightPlaceholder()
        {
            return string.Empty;
        }

        // Centralized LogMessage helpers: new overloads ensure Settings.DebugMode is checked in one place
        public void LogMessage(string message, int frame = 0)
        {
            if (Settings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }

        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            if (localDebug && Settings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }

        public void LogError(string message, int frame = 0)
        {
            if (Settings.DebugMode)
            {
                base.LogError(message, frame);
            }
        }

        public void LogError(bool localDebug, string message, int frame = 0)
        {
            if (localDebug && Settings.DebugMode)
            {
                base.LogError(message, frame);
            }
        }

        private bool canClick()
        {
            return Input.GetKeyState(Settings.ClickLabelKey.Value) && IsPOEActive() && (!Settings.BlockOnOpenLeftRightPanel || !IsPanelOpen())
                    && !InTownOrHideout() && !waitingForCorruption && !GameController.IngameState.IngameUi.ChatTitlePanel.IsVisible;
        }

        private IEnumerator ScanForAltarsLogic()
        {
            // collect labels based on settings (avoid intermediate ToList allocations)
            List<LabelOnGround> altarLabels = [];
            bool highlightExarch = Settings.HighlightExarchAltars;
            bool highlightEater = Settings.HighlightEaterAltars;

            if (highlightExarch)
            {
                List<LabelOnGround> l = GetAltarLabels(AltarType.SearingExarch);
                if (l != null && l.Count > 0)
                {
                    altarLabels.AddRange(l);
                }
            }

            if (highlightEater)
            {
                List<LabelOnGround> l = GetAltarLabels(AltarType.EaterOfWorlds);
                if (l != null && l.Count > 0)
                {
                    altarLabels.AddRange(l);
                }
            }

            if (altarLabels.Count == 0)
            {
                // no altars found -> clear cache and pause
                altarComponents.Clear();
                altarCoroutine.Pause();
                yield break;
            }

            bool debug = Settings.DebugMode;

            // iterate without creating extra copies
            for (int i = 0; i < altarLabels.Count; i++)
            {
                LabelOnGround label = altarLabels[i];
                if (label == null)
                {
                    continue;
                }

                // Get elements that likely contain altar mods; this uses a shared list internally
                List<Element> elements = GetElementsByStringContains(label.Label, "valuedefault");
                if (elements == null || elements.Count == 0)
                {
                    continue;
                }

                string path = label.ItemOnGround?.Path ?? string.Empty;

                for (int j = 0; j < elements.Count; j++)
                {
                    Element element = elements[j];
                    if (element == null || !element.IsVisible)
                    {
                        if (debug)
                        {
                            LogError("Element is null", 10);
                        }

                        continue;
                    }

                    if (path.Contains("CleansingFireAltar"))
                    {
                        if (debug)
                        {
                            LogMessage("CleansingFireAltar");
                        }
                    }
                    else if (path.Contains("TangleAltar"))
                    {
                        if (debug)
                        {
                            LogMessage("TangleAltar");
                        }
                    }

                    AltarType altarType = path.Contains("CleansingFireAltar")
                        ? AltarType.SearingExarch
                        : path.Contains("TangleAltar") ? AltarType.EaterOfWorlds : AltarType.Unknown;

                    // build a new component and populate it
                    PrimaryAltarComponent altarComponent = new(altarType,
                        new SecondaryAltarComponent(new Element(), "", "", "", ""), new AltarButton(new Element()),
                        new SecondaryAltarComponent(new Element(), "", "", "", ""), new AltarButton(new Element()));

                    Element altarParent = element.Parent.Parent;
                    Element? topAltarElement = altarParent.GetChildFromIndices(0, 1);
                    Element? bottomAltarElement = altarParent.GetChildFromIndices(1, 1);

                    if (topAltarElement != null)
                    {
                        updateComponentFromElementData(true, altarParent, altarComponent, topAltarElement, altarType);
                    }

                    if (bottomAltarElement != null)
                    {
                        updateComponentFromElementData(false, altarParent, altarComponent, bottomAltarElement, altarType);
                    }

                    // verify component completeness
                    if (altarComponent.TopMods == null || altarComponent.TopButton == null || altarComponent.BottomMods == null || altarComponent.BottomButton == null)
                    {
                        if (debug)
                        {
                            LogError("Part of altarcomponent is null", 10);
                            LogError("part1: " + altarComponent.TopMods, 10);
                            LogError("part2: " + altarComponent.TopButton, 10);
                            LogError("part3: " + altarComponent.BottomMods, 10);
                            LogError("part4: " + altarComponent.BottomButton, 10);
                        }

                        continue;
                    }

                    // compute a simple key for quick equality check instead of expensive LINQ
                    string newKey = BuildAltarKey(altarComponent);
                    bool exists = false;
                    for (int k = 0; k < altarComponents.Count; k++)
                    {
                        if (BuildAltarKey(altarComponents[k]) == newKey)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        altarComponents.Add(altarComponent);
                        if (debug)
                        {
                            LogMessage("New altar added to altarcomponents list");
                        }
                    }
                    else
                    {
                        if (debug)
                        {
                            LogMessage("Altar already added to altarcomponents list");
                        }
                    }
                }
            }

            // pause coroutine to save CPU until resumed by Tick()
            altarCoroutine.Pause();
            yield break;
        }

        // small deterministic key for altar comparison, cheap string concat
        private string BuildAltarKey(PrimaryAltarComponent comp)
        {
            // protect against nulls
            string t1 = comp.TopMods?.FirstUpside ?? string.Empty;
            string t2 = comp.TopMods?.SecondUpside ?? string.Empty;
            string td1 = comp.TopMods?.FirstDownside ?? string.Empty;
            string td2 = comp.TopMods?.SecondDownside ?? string.Empty;
            string b1 = comp.BottomMods?.FirstUpside ?? string.Empty;
            string b2 = comp.BottomMods?.SecondUpside ?? string.Empty;
            string bd1 = comp.BottomMods?.FirstDownside ?? string.Empty;
            string bd2 = comp.BottomMods?.SecondDownside ?? string.Empty;

            return string.Concat(t1, "|", t2, "|", td1, "|", td2, "|", b1, "|", b2, "|", bd1, "|", bd2);
        }

        private bool IsPOEActive()
        {
            if (!GameController.Window.IsForeground())
            {
                LogMessage("(ClickIt) Path of exile window not active");

                return false;
            }

            return true;
        }

        private bool IsPanelOpen()
        {
            if (GameController.IngameState.IngameUi.OpenLeftPanel.Address != 0 ||
                GameController.IngameState.IngameUi.OpenRightPanel.Address != 0)
            {
                LogMessage("(ClickIt) Left or right panel is open");

                return true;
            }

            return false;
        }

        private bool GroundItemsVisible()
        {
            if (CachedLabels.Value.Count < 1)
            {
                LogMessage("(ClickIt) No ground items found");

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
                LogMessage("(ClickIt) In hideout or town");

                return true;
            }

            return false;
        }

        private static readonly List<Element> elementsByStringContainsList = [];

        public List<Element> GetElementsByStringContains(Element label, string str)
        {
            // reuse the shared list to avoid allocations
            elementsByStringContainsList.Clear();

            if (label == null)
            {
                return elementsByStringContainsList;
            }

            try
            {
                // check the root label once
                string rootText = label.GetText(512);
                if (!string.IsNullOrEmpty(rootText) && rootText.Contains(str))
                {
                    elementsByStringContainsList.Add(label);
                }

                // check first-level and second-level child containers (indices 0 and 1)
                for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
                {
                    try
                    {
                        Element container = label.GetChildAtIndex(containerIndex);
                        if (container == null)
                        {
                            continue;
                        }

                        IList<Element> children = container.Children;
                        if (children == null)
                        {
                            continue;
                        }

                        // iterate using index to avoid LINQ allocations
                        for (int i = 0; i < children.Count; i++)
                        {
                            Element? child = children[i];
                            if (child == null)
                            {
                                continue;
                            }

                            string childText = child.GetText(512);
                            if (!string.IsNullOrEmpty(childText) && childText.Contains(str))
                            {
                                elementsByStringContainsList.Add(child);
                            }
                        }
                    }
                    catch
                    {
                        // ignore per-element read errors
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(Settings.DebugMode, "GetElementsByStringContains error: " + ex.Message, 5);
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

            // Read raw text once
            string rawText = ElementToExtractDataFrom.GetText(512) ?? string.Empty;
            LogMessage(Settings.DebugMode, rawText);

            // Normalize and clean up once
            string AltarMods = rawText.Replace("<valuedefault>", "").Replace("{", "")
                .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "").Replace("gain:", "")
                .Replace("gains:", "");

            AltarMods = Regex.Replace(AltarMods, @"<rgb\(\d+,\d+,\d+\)>", "");

            // Split into lines once
            string[] lines = AltarMods.Replace("\r", "").Split('\n');
            if (lines.Length > 0)
            {
                NegativeModType = lines[0];
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!string.IsNullOrEmpty(line))
                {
                    mods.Add(line);
                }

                LogMessage(Settings.DebugMode, "Altarmods (" + i + ") Added: " + line);
            }

            // Cache ClickItSettings fields once and build candidate list (only Exarch_/Eater_ fields)
            FieldInfo[] settingsFields = typeof(ClickItSettings).GetFields(BindingFlags.Public |
                                                                           BindingFlags.NonPublic |
                                                                           BindingFlags.Instance |
                                                                           BindingFlags.Static);
            List<(FieldInfo field, string nameLower)> candidateFields = [];
            for (int f = 0; f < settingsFields.Length; f++)
            {
                FieldInfo fi = settingsFields[f];
                string FieldName = fi.Name.Replace("<", "").Replace(">", "").Replace("k__BackingField", "");
                if (FieldName.StartsWith("Exarch_") || FieldName.StartsWith("Eater_"))
                {
                    candidateFields.Add((fi, FieldName.ToLowerInvariant()));
                }
            }

            // Process each mod and attempt to match to a candidate field
            string prefix = altarType == AltarType.SearingExarch ? "Exarch_" : altarType == AltarType.EaterOfWorlds ? "Eater_" : "Unknown_";

            for (int mi = 0; mi < mods.Count; mi++)
            {
                string mod = mods[mi];
                bool found = false;

                // Extract letters only and lower-case once
                string localmodLetters = new(mod.Where(char.IsLetter).ToArray());
                string localmodLower = localmodLetters.ToLowerInvariant();
                string localmodToLowerWithPrefix = prefix + localmodLower;

                // Compare against candidate fields
                for (int c = 0; c < candidateFields.Count; c++)
                {
                    (FieldInfo field, string fieldToLower) = candidateFields[c];

                    if (localmodToLowerWithPrefix.Contains(fieldToLower))
                    {
                        // determine upside vs downside based on known tokens
                        bool isUpside = localmodLower.Contains("chancetodropanadditional") ||
                                        localmodLower.Contains("finalbossdrops") ||
                                        localmodLower.Contains("increasedquantityofitems") ||
                                        localmodLower.Contains("droppedbyslainenemieshave") ||
                                        localmodLower.Contains("chancetobeduplicated");

                        string FieldName = field.Name.Replace("<", "").Replace(">", "").Replace("k__BackingField", "");

                        if (isUpside)
                        {
                            upsides.Add(FieldName);
                        }
                        else
                        {
                            downsides.Add(FieldName);
                        }

                        found = true;

                        LogMessage(Settings.DebugMode, "Added " + (altarType == AltarType.SearingExarch ? "Exarch" : altarType == AltarType.EaterOfWorlds ? "Eater" : "Unknown") +
                                       (isUpside ? " upside: " : " downside: ") + fieldToLower + " - " + localmodToLowerWithPrefix);

                        // note: do not break to allow multiple field matches per mod (preserve original behavior)
                    }
                }

                if (!found)
                {
                    try
                    {
                        LogError(Settings.DebugMode, "updateComponentFromElementData: Failed to match mod with field? Field may not be required - localmod:" +
                                localmodLetters, 10);
                    }
                    catch (Exception)
                    {
                        LogError(Settings.DebugMode, "updateComponentFromElementData: Failed to match mod with field? Field may not be required - unable to write field, sequence contains no elements", 10);
                    }
                }
            }

            LogMessage(Settings.DebugMode, "Setting up altar component");

            // populate the component (preserve original selection logic)
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
                LogMessage(Settings.DebugMode, "Updated top altar component: " + altarComponent.TopMods);
                LogMessage(Settings.DebugMode, "Upside1: " + altarComponent.TopMods.FirstUpside);
                LogMessage(Settings.DebugMode, "Upside2: " + altarComponent.TopMods.SecondUpside);
                LogMessage(Settings.DebugMode, "Downside1: " + altarComponent.TopMods.FirstDownside);
                LogMessage(Settings.DebugMode, "Downside2: " + altarComponent.TopMods.SecondDownside);
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
                LogMessage(Settings.DebugMode, "Updated bottom altar component: ");
                LogMessage(Settings.DebugMode, "Upside1: " + (string.IsNullOrEmpty(altarComponent.BottomMods.FirstUpside)
                    ? "null"
                    : altarComponent.BottomMods.FirstUpside));
                LogMessage(Settings.DebugMode, "Upside2: " + (string.IsNullOrEmpty(altarComponent.BottomMods.SecondUpside)
                    ? "null"
                    : altarComponent.BottomMods.SecondUpside));
                LogMessage(Settings.DebugMode, "Downside1: " + (string.IsNullOrEmpty(altarComponent.BottomMods.FirstDownside)
                    ? "Null"
                    : altarComponent.BottomMods.FirstDownside));
                LogMessage(Settings.DebugMode, "Downside2: " + (string.IsNullOrEmpty(altarComponent.BottomMods.SecondDownside)
                    ? "null"
                    : altarComponent.BottomMods.SecondDownside));
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
                    x.ItemOnGround.Path.Contains("CraftingUnlocks") ||
                    x.ItemOnGround.Path.Contains("Brequel")
                    ))
                .OrderBy(x => x.ItemOnGround.DistancePlayer)
                .ToList();
        }

        private IEnumerator ClickLabel(Element? altar = null)
        {
            Stopwatch ClickLabelTimer = Stopwatch.StartNew();

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

                if (timer.ElapsedMilliseconds > 10)
                {
                    LogMessage("Collecting ground labels took " + timer.ElapsedMilliseconds + " ms", 5);
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

                // Handle Breach Nodes
                if (nextLabel.ItemOnGround.Path.Contains("Brequel") && Settings.ClickBreachNodes)
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
                    if (!PointIsInClickableArea(labelElement.GetClientRect().Center + windowTopLeft, nextLabel.ItemOnGround.Path))
                    {
                        LogMessage("(ClickIt) nextLabel is not in clickable area");

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
                        !(Settings.ClickBreachNodes && nextLabel.ItemOnGround.Path.Contains("Brequel")) &&
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
                LogError(e.ToString(), 10);
                yield break;
            }
            finally
            {
                ClickLabelTimer.Stop();
                if (ClickLabelTimer.ElapsedMilliseconds > 10)
                {
                    LogMessage("ClickLabel took " + ClickLabelTimer.ElapsedMilliseconds + " ms", 5);
                }
            }
        }

        private void ProcessEssenceLabel(LabelOnGround nextLabel, Vector2 windowTopLeft)
        {
            Element? label = nextLabel.Label.Parent;
            if (label == null)
            {
                // fallback click if parent not available
                Vector2 clickPosFallback = nextLabel.Label.GetClientRect().Center + windowTopLeft;
                Input.SetCursorPos(clickPosFallback);
                if (Settings.LeftHanded)
                {
                    Mouse.RightClick();
                }
                else
                {
                    Mouse.LeftClick();
                }

                Mouse.blockInput(false);
                return;
            }

            // If already corrupted, just click (unless waiting for corrupt)
            if (ElementContainsAnyStrings(label, new[] { "Corrupted" }))
            {
                if (!waitingForCorruption)
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
                return;
            }

            bool meetsCorruptCriteria = false;

            if (Settings.CorruptAllEssences)
            {
                meetsCorruptCriteria = true;
            }
            else if (Settings.CorruptMEDSEssences)
            {
                string[] meds = new[]
                {
                    "Screaming Essence of Misery",
                    "Screaming Essence of Envy",
                    "Screaming Essence of Dread",
                    "Screaming Essence of Scorn",
                    "Shrieking Essence of Misery",
                    "Shrieking Essence of Envy",
                    "Shrieking Essence of Dread",
                    "Shrieking Essence of Scorn",
                    "Deafening Essence of Misery",
                    "Deafening Essence of Envy",
                    "Deafening Essence of Dread",
                    "Deafening Essence of Scorn"
                };
                meetsCorruptCriteria = ElementContainsAnyStrings(label, meds);
            }
            else if (Settings.CorruptAnyNonShrieking)
            {
                meetsCorruptCriteria = !CheckForAnyShriekingEssenceOptimized(label);
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

        // Helper: search element tree once for any of the provided patterns (case-sensitive Contains)
        private bool ElementContainsAnyStrings(Element? root, IEnumerable<string> patterns)
        {
            if (root == null)
            {
                return false;
            }

            string[] patList = patterns as string[] ?? patterns.ToArray();
            if (patList.Length == 0)
            {
                return false;
            }

            Stack<Element> stack = new();
            stack.Push(root);

            while (stack.Count > 0)
            {
                Element el = stack.Pop();
                try
                {
                    string text = el.GetText(512);
                    if (!string.IsNullOrEmpty(text))
                    {
                        for (int i = 0; i < patList.Length; i++)
                        {
                            if (text.Contains(patList[i]))
                            {
                                return true;
                            }
                        }
                    }

                    IList<Element> children = el.Children;
                    if (children != null)
                    {
                        foreach (Element? c in children)
                        {
                            if (c != null)
                            {
                                stack.Push(c);
                            }
                        }
                    }
                }
                catch
                {
                    // ignore read errors
                }
            }

            return false;
        }

        private bool CheckForAnyShriekingEssenceOptimized(Element label)
        {
            string[] shrieking =
            [
                "Shrieking Essence of Greed",
                "Shrieking Essence of Contempt",
                "Shrieking Essence of Hatred",
                "Shrieking Essence of Woe",
                "Shrieking Essence of Fear",
                "Shrieking Essence of Anger",
                "Shrieking Essence of Torment",
                "Shrieking Essence of Sorrow",
                "Shrieking Essence of Rage",
                "Shrieking Essence of Suffering",
                "Shrieking Essence of Wrath",
                "Shrieking Essence of Doubt",
                "Shrieking Essence of Loathing",
                "Shrieking Essence of Zeal",
                "Shrieking Essence of Anguish",
                "Shrieking Essence of Spite",
                "Shrieking Essence of Scorn",
                "Shrieking Essence of Envy",
                "Shrieking Essence of Misery",
                "Shrieking Essence of Dread"
            ];

            return ElementContainsAnyStrings(label, shrieking);
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
            bool ClickBreach = s.ClickBreachNodes.Value;

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

                // Breach Nodes
                if (ClickBreach && !string.IsNullOrEmpty(path) && path.Contains("Brequel"))
                {
                    return label;
                }
            }

            return null;
        }

        // Optimized: iterative DFS, nullable return, avoid LINQ and recursion
        public Element? GetElementByString(Element? root, string str)
        {
            Stopwatch sw = Stopwatch.StartNew();

            if (root == null)
            {
                sw.Stop();
                if (sw.ElapsedMilliseconds > 10)
                {
                    LogMessage("GetElementByString took " + sw.ElapsedMilliseconds + " ms", 5);
                }

                return null;
            }

            Element? found = null;
            Stack<Element> stack = new();
            stack.Push(root);

            while (stack.Count > 0)
            {
                Element el = stack.Pop();
                try
                {
                    string text = el.GetText(512);
                    if (text != null && text.Equals(str))
                    {
                        found = el;
                        break;
                    }

                    IList<Element> children = el.Children;
                    if (children != null)
                    {
                        // push children onto stack
                        foreach (Element? c in children)
                        {
                            if (c != null)
                            {
                                stack.Push(c);
                            }
                        }
                    }
                }
                catch
                {
                    // ignore read errors and continue
                }
            }

            sw.Stop();
            if (sw.ElapsedMilliseconds > 10)
            {
                LogMessage("GetElementByString took " + sw.ElapsedMilliseconds + " ms", 5);
            }

            return found;
        }

        private void StartEssenceCorruption(LabelOnGround nextLabel, Vector2 windowTopLeft)
        {
            LogMessage("Essence is not corrupted and meets criteria, lets corrupt it", 5);
            LogMessage("Starting corruption", 5);

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

        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
