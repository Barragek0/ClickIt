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
        private const string CleansingFireAltar = "CleansingFireAltar";
        private const string TangleAltar = "TangleAltar";
        private const string ReportBugMessage = "\nPlease report this as a bug on github";

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

            Settings.EnsureAllModsHaveWeights();

            return true;
        }

        private RectangleF FullScreenArea()
        {
            RectangleF winRect = GameController.Window.GetWindowRectangleTimeCache;
            if (FullScreenRectangle.Width != winRect.Width || FullScreenRectangle.Height != winRect.Height ||
                FullScreenRectangle.X != winRect.X || FullScreenRectangle.Y != winRect.Y)
            {
                FullScreenRectangle = new RectangleF(winRect.X, winRect.Y, winRect.Width, winRect.Height);
                HealthAndFlaskRectangle = new RectangleF(
                    (float)(winRect.BottomLeft.X / 3),
                    (float)(winRect.BottomLeft.Y / 5 * 3.92),
                    (float)(winRect.BottomLeft.X + (winRect.BottomRight.X / 3.4)),
                    winRect.BottomLeft.Y);

                ManaAndSkillsRectangle = new RectangleF(
                    (float)(winRect.BottomRight.X / 3 * 2.12),
                    (float)(winRect.BottomLeft.Y / 5 * 3.92),
                    winRect.BottomRight.X,
                    winRect.BottomRight.Y);

                BuffsAndDebuffsRectangle = new RectangleF(
                    winRect.TopLeft.X,
                    winRect.TopLeft.Y,
                    winRect.TopRight.X / 2,
                    winRect.TopLeft.Y + 120);
            }

            return FullScreenRectangle;
        }

        private bool PointIsInClickableArea(Vector2 point, string? path = null)
        {
            RectangleF full = FullScreenArea();
            return point.PointInRectangle(full) &&
                  !point.PointInRectangle(HealthAndFlaskRectangle) &&
                  !point.PointInRectangle(ManaAndSkillsRectangle) &&
                  !point.PointInRectangle(BuffsAndDebuffsRectangle);
        }

        public override void Render()
        {
            bool debugMode = Settings.DebugMode;
            bool renderDebug = Settings.RenderDebug;

            if (debugMode && renderDebug)
            {
                Graphics.DrawFrame(FullScreenRectangle, Color.Green, 1);
                Graphics.DrawFrame(HealthAndFlaskRectangle, Color.Orange, 1);
                Graphics.DrawFrame(ManaAndSkillsRectangle, Color.Cyan, 1);
                Graphics.DrawFrame(BuffsAndDebuffsRectangle, Color.Yellow, 1);
            }

            if (altarComponents.Count == 0)
            {
                return;
            }
            List<PrimaryAltarComponent> altarSnapshot = altarComponents.ToList();
            bool clickEater = Settings.ClickEaterAltars;
            bool clickExarch = Settings.ClickExarchAltars;
            bool leftHanded = Settings.LeftHanded;
            Vector2 windowTopLeft = GameController.Window.GetWindowRectangleTimeCache.TopLeft;
            Vector2 offset120_Minus60 = new(120, -70);
            Vector2 offset120_Minus25 = new(120, -25);
            Vector2 offset5_Minus32 = new(5, -32);
            Vector2 offset5_Minus20 = new(5, -20);
            Vector2 offset10_Minus32 = new(10, -32);
            Vector2 offset10_Minus20 = new(10, -20);
            Vector2 offset10_5 = new(10, 5);
            Color colorOrange = Color.Orange;
            Color colorYellow = Color.Yellow;
            Color colorLawnGreen = Color.LawnGreen;
            Color colorOrangeRed = Color.OrangeRed;
            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                string topFirstUpside = altar.TopMods.FirstUpside;
                string topSecondUpside = altar.TopMods.SecondUpside;
                string topFirstDownside = altar.TopMods.FirstDownside;
                string topSecondDownside = altar.TopMods.SecondDownside;
                string botFirstUpside = altar.BottomMods.FirstUpside;
                string botSecondUpside = altar.BottomMods.SecondUpside;
                string botFirstDownside = altar.BottomMods.FirstDownside;
                string botSecondDownside = altar.BottomMods.SecondDownside;
                decimal TopUpsideWeight = CalculateUpsideWeight(altar.TopMods.Upsides);
                decimal TopDownsideWeight = CalculateDownsideWeight(altar.TopMods.Downsides);
                decimal BottomUpsideWeight = CalculateUpsideWeight(altar.BottomMods.Upsides);
                decimal BottomDownsideWeight = CalculateDownsideWeight(altar.BottomMods.Downsides);

                decimal TopDownside1Weight = CalculateDownsideWeight([altar.TopMods.FirstDownside]);
                decimal TopDownside2Weight = CalculateDownsideWeight([altar.TopMods.SecondDownside]);
                decimal BottomDownside1Weight = CalculateDownsideWeight([altar.BottomMods.FirstDownside]);
                decimal BottomDownside2Weight = CalculateDownsideWeight([altar.BottomMods.SecondDownside]);
                decimal TopUpside1Weight = CalculateUpsideWeight([altar.TopMods.FirstUpside]);
                decimal TopUpside2Weight = CalculateUpsideWeight([altar.TopMods.SecondUpside]);
                decimal BottomUpside1Weight = CalculateUpsideWeight([altar.BottomMods.FirstUpside]);
                decimal BottomUpside2Weight = CalculateUpsideWeight([altar.BottomMods.SecondUpside]);

                decimal TopWeight = Math.Round(TopUpsideWeight / TopDownsideWeight, 2);
                decimal BottomWeight = Math.Round(BottomUpsideWeight / BottomDownsideWeight, 2);

                Element? boxToClick = null;

                RectangleF topModsRect = altar.TopMods.Element.GetClientRect();
                RectangleF bottomModsRect = altar.BottomMods.Element.GetClientRect();
                Vector2 topModsTopLeft = topModsRect.TopLeft;
                Vector2 bottomModsTopLeft = bottomModsRect.TopLeft;
                if (TopUpsideWeight <= 0)
                {
                    _ = Graphics.DrawText("Top upside weights couldn't be recognised " +
                        "\n1:" + topFirstUpside +
                        "\n2:" + topSecondUpside +
                        ReportBugMessage,
                        topModsTopLeft + offset120_Minus60, colorOrange, 30);
                    Graphics.DrawFrame(topModsRect, colorYellow, 2);
                    Graphics.DrawFrame(bottomModsRect, colorYellow, 2);
                }
                else if (TopDownsideWeight <= 0)
                {
                    _ = Graphics.DrawText("Top downside weights couldn't be recognised " +
                        "\n1:" + topFirstDownside +
                        "\n2:" + topSecondDownside +
                        ReportBugMessage,
                        topModsTopLeft + offset120_Minus60, colorOrange, 30);
                    Graphics.DrawFrame(topModsRect, colorYellow, 2);
                    Graphics.DrawFrame(bottomModsRect, colorYellow, 2);
                }
                else if (BottomUpsideWeight <= 0)
                {
                    _ = Graphics.DrawText("Bottom upside weights couldn't be recognised " +
                        "\n1:" + botFirstUpside +
                        "\n2:" + botSecondUpside +
                        ReportBugMessage,
                        topModsTopLeft + offset120_Minus60, colorOrange, 30);
                    Graphics.DrawFrame(topModsRect, colorYellow, 2);
                    Graphics.DrawFrame(bottomModsRect, colorYellow, 2);
                }
                else if (BottomDownsideWeight <= 0)
                {
                    _ = Graphics.DrawText("Bottom downside weights couldn't be recognised " +
                        "\n1:" + botFirstDownside +
                        "\n2:" + botSecondDownside +
                        ReportBugMessage,
                        topModsTopLeft + offset120_Minus60, colorOrange, 30);
                    Graphics.DrawFrame(topModsRect, colorYellow, 2);
                    Graphics.DrawFrame(bottomModsRect, colorYellow, 2);
                }
                else if ((TopDownside1Weight >= 90 || TopDownside2Weight >= 90) && (BottomDownside1Weight >= 90 || BottomDownside2Weight >= 90))
                {
                    _ = Graphics.DrawText("Weighting has been overridden\n\nBoth options have downsides with a weight of 90+ that may brick your build.",
                        topModsTopLeft + offset120_Minus60, colorOrange, 30);
                    Graphics.DrawFrame(topModsRect, colorOrangeRed, 2);
                    Graphics.DrawFrame(bottomModsRect, colorOrangeRed, 2);
                }
                else if (TopUpside1Weight >= 90 || TopUpside2Weight >= 90)
                {
                    _ = Graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of 90+",
                        topModsTopLeft + offset120_Minus60, colorLawnGreen, 30);
                    Graphics.DrawFrame(topModsRect, colorLawnGreen, 3);
                    Graphics.DrawFrame(bottomModsRect, colorOrangeRed, 2);
                    boxToClick = altar.TopButton.Element;
                }
                else if (BottomUpside1Weight >= 90 || BottomUpside2Weight >= 90)
                {
                    _ = Graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of 90+",
                        topModsTopLeft + offset120_Minus60, colorLawnGreen, 30);
                    Graphics.DrawFrame(topModsRect, colorOrangeRed, 2);
                    Graphics.DrawFrame(bottomModsRect, colorLawnGreen, 3);
                    boxToClick = altar.BottomButton.Element;
                }
                else if (TopDownside1Weight >= 90 || TopDownside2Weight >= 90)
                {
                    _ = Graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the top downsides has a weight of 90+",
                        topModsTopLeft + offset120_Minus60, colorLawnGreen, 30);
                    Graphics.DrawFrame(topModsRect, colorOrangeRed, 3);
                    Graphics.DrawFrame(bottomModsRect, colorLawnGreen, 2);
                    boxToClick = altar.BottomButton.Element;
                }
                else if (BottomDownside1Weight >= 90 || BottomDownside2Weight >= 90)
                {
                    _ = Graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the bottom downsides has a weight of 90+",
                        topModsTopLeft + offset120_Minus60, colorLawnGreen, 30);
                    Graphics.DrawFrame(topModsRect, colorLawnGreen, 2);
                    Graphics.DrawFrame(bottomModsRect, colorOrangeRed, 3);
                    boxToClick = altar.TopButton.Element;
                }
                else if (TopWeight > BottomWeight)
                {
                    Graphics.DrawFrame(topModsRect, colorLawnGreen, 3);
                    Graphics.DrawFrame(bottomModsRect, colorOrangeRed, 2);
                    boxToClick = altar.TopButton.Element;
                }
                else if (BottomWeight > TopWeight)
                {
                    Graphics.DrawFrame(topModsRect, colorOrangeRed, 2);
                    Graphics.DrawFrame(bottomModsRect, colorLawnGreen, 3);
                    boxToClick = altar.BottomButton.Element;
                }
                else
                {
                    _ = Graphics.DrawText("Mods have equal weight, you should choose.",
                        topModsTopLeft + offset120_Minus25, colorOrange, 30);
                    Graphics.DrawFrame(topModsRect, colorYellow, 2);
                    Graphics.DrawFrame(bottomModsRect, colorYellow, 2);
                }

                _ = Graphics.DrawText("Upside: " + TopUpsideWeight,
                    topModsTopLeft + offset5_Minus32, colorLawnGreen, 14);
                _ = Graphics.DrawText("Downside: " + TopDownsideWeight,
                    topModsTopLeft + offset5_Minus20, colorOrangeRed, 14);
                _ = Graphics.DrawText("Upside: " + BottomUpsideWeight,
                    bottomModsTopLeft + offset10_Minus32, colorLawnGreen, 14);
                _ = Graphics.DrawText("Downside: " + BottomDownsideWeight,
                    bottomModsTopLeft + offset10_Minus20, colorOrangeRed, 14);

                Color topWeightColor;
                if (TopWeight > BottomWeight) topWeightColor = colorLawnGreen;
                else if (BottomWeight > TopWeight) topWeightColor = colorOrangeRed;
                else topWeightColor = colorYellow;

                _ = Graphics.DrawText("" + TopWeight,
                    topModsTopLeft + offset10_5,
                    topWeightColor, 18);

                Color bottomWeightColor;
                if (BottomWeight > TopWeight) bottomWeightColor = colorLawnGreen;
                else if (TopWeight > BottomWeight) bottomWeightColor = colorOrangeRed;
                else bottomWeightColor = colorYellow;
                _ = Graphics.DrawText("" + BottomWeight,
                    bottomModsTopLeft + offset10_5,
                    bottomWeightColor, 18);

                if (((altar.AltarType == AltarType.EaterOfWorlds && clickEater) || (altar.AltarType == AltarType.SearingExarch && clickExarch)) &&
                    boxToClick != null && PointIsInClickableArea(boxToClick.GetClientRect().Center, altar.AltarType.ToString()) && boxToClick.IsVisible && canClick())
                {
                    Mouse.blockInput(true);
                    LogMessage("Moving mouse for altar", 5);
                    Input.SetCursorPos(boxToClick.GetClientRect().Center + windowTopLeft);
                    if (leftHanded)
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
                altarComponents.Clear();
                altarCoroutine.Pause();
                yield break;
            }

            bool debug = Settings.DebugMode;

            for (int i = 0; i < altarLabels.Count; i++)
            {
                LabelOnGround label = altarLabels[i];
                if (label == null)
                {
                    continue;
                }

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

                    if (path.Contains(CleansingFireAltar))
                    {
                        if (debug)
                        {
                            LogMessage("CleansingFireAltar");
                        }
                    }
                    else if (path.Contains(TangleAltar))
                    {
                        if (debug)
                        {
                            LogMessage("TangleAltar");
                        }
                    }

                    AltarType altarType;
                    if (path.Contains(CleansingFireAltar))
                        altarType = AltarType.SearingExarch;
                    else if (path.Contains(TangleAltar))
                        altarType = AltarType.EaterOfWorlds;
                    else
                        altarType = AltarType.Unknown;

                    PrimaryAltarComponent altarComponent = new(altarType,
                        new SecondaryAltarComponent(new Element(), new List<string>(), new List<string>()), new AltarButton(new Element()),
                        new SecondaryAltarComponent(new Element(), new List<string>(), new List<string>()), new AltarButton(new Element()));

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

            altarCoroutine.Pause();
            yield break;
        }

        private static string BuildAltarKey(PrimaryAltarComponent comp)
        {
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
            elementsByStringContainsList.Clear();

            if (label == null)
            {
                return elementsByStringContainsList;
            }

            try
            {
                string rootText = label.GetText(512);
                if (!string.IsNullOrEmpty(rootText) && rootText.Contains(str))
                {
                    elementsByStringContainsList.Add(label);
                }

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
                string cleanedMod = new string(mod.Where(char.IsLetter).ToArray());
                string cleanedNegativeModType = new string(NegativeModType.Where(char.IsLetter).ToArray());

                var searchLists = new[]
                {
                    new { List = AltarModsConstants.UpsideMods, IsUpside = true },
                    new { List = AltarModsConstants.DownsideMods, IsUpside = false }
                };

                foreach (var searchList in searchLists)
                {
                    foreach (var (Id, _, Type, _) in searchList.List)
                    {
                        string cleanedId = new string(Id.Where(char.IsLetter).ToArray());
                        if (cleanedId.Equals(cleanedMod, StringComparison.OrdinalIgnoreCase))
                        {
                            string modTarget = "";
                            if (cleanedNegativeModType.Contains("Mapboss")) modTarget = "Boss";
                            else if (cleanedNegativeModType.Contains("EldritchMinions")) modTarget = "Minion";
                            else if (cleanedNegativeModType.Contains("Player")) modTarget = "Player";

                            if (Type.Equals(modTarget, StringComparison.OrdinalIgnoreCase))
                            {
                                if (searchList.IsUpside)
                                {
                                    upsides.Add(Id);
                                }
                                else
                                {
                                    downsides.Add(Id);
                                }
                                found = true;

                                if (Settings.DebugMode)
                                {
                                    LogMessage($"Added {(searchList.IsUpside ? "upside" : "downside")}: {Id} (Type: {Type})");
                                }
                                break;
                            }
                        }
                    }
                    if (found) break;
                }

                if (!found)
                {
                    if (Settings.DebugMode)
                    {
                        LogError($"updateComponentFromElementData: Failed to match mod: '{mod}' (Cleaned: '{cleanedMod}') with NegativeModType: '{NegativeModType}'", 10);
                    }
                }
            }

            if (Settings.DebugMode)
            {
                LogMessage("Setting up altar component");
            }

            if (top)
            {
                altarComponent.TopButton = new AltarButton(ElementToExtractDataFrom.Parent);
                altarComponent.TopMods =
                    new SecondaryAltarComponent(ElementToExtractDataFrom, upsides, downsides);
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
                altarComponent.BottomButton = new AltarButton(ElementToExtractDataFrom.Parent);
                altarComponent.BottomMods =
                    new SecondaryAltarComponent(ElementToExtractDataFrom, upsides, downsides);
                if (Settings.DebugMode)
                {
                    LogMessage("Updated bottom altar component: " + altarComponent.BottomMods);
                    LogMessage("Upside1: " + altarComponent.BottomMods.FirstUpside);
                    LogMessage("Upside2: " + altarComponent.BottomMods.SecondUpside);
                    LogMessage("Downside1: " + altarComponent.BottomMods.FirstDownside);
                    LogMessage("Downside2: " + altarComponent.BottomMods.SecondDownside);
                }
            }
        }

        private static string GetLine(string text, int lineNo)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length >= lineNo ? lines[lineNo] : "ERROR: Could not read line.";
        }

        private static int CountLines(string text)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length;
        }

        private List<LabelOnGround> GetHarvestLabels()
        {
            // Skip creating unnecessary list and avoid LINQ allocations 
            List<LabelOnGround> result = [];
            List<LabelOnGround>? cachedLabels = CachedLabels?.Value;

            if (cachedLabels == null)
            {
                return result;
            }

            for (int i = 0; i < cachedLabels.Count; i++)
            {
                LabelOnGround label = cachedLabels[i];
                if (label.ItemOnGround?.Path == null ||
                    !PointIsInClickableArea(label.Label.GetClientRect().Center))
                {
                    continue;
                }

                string path = label.ItemOnGround.Path;
                if (path.Contains("Harvest/Irrigator") || path.Contains("Harvest/Extractor"))
                {
                    result.Add(label);
                }
            }

            // Sort by distance without LINQ
            if (result.Count > 1)
            {
                result.Sort((a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));
            }

            return result;
        }

        private List<LabelOnGround> GetAltarLabels(AltarType type)
        {
            // Preallocate result list and avoid LINQ
            List<LabelOnGround> result = [];
            List<LabelOnGround>? cachedLabels = CachedLabels?.Value;

            if (cachedLabels == null)
            {
                return result;
            }

            string typeStr = type == AltarType.SearingExarch ? CleansingFireAltar : TangleAltar;
            RectangleF fullScreen = FullScreenArea();

            for (int i = 0; i < cachedLabels.Count; i++)
            {
                LabelOnGround label = cachedLabels[i];
                if (label.ItemOnGround?.Path == null ||
                    !label.Label.IsVisible ||
                    !label.Label.GetClientRect().Center.PointInRectangle(fullScreen))
                {
                    continue;
                }

                if (label.ItemOnGround.Path.Contains(typeStr))
                {
                    result.Add(label);
                }
            }

            return result;
        }

        private List<LabelOnGround> UpdateLabelComponent()
        {
            List<LabelOnGround> result = [];
            IList<LabelOnGround>? groundLabels = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabels;

            if (groundLabels == null)
            {
                return result;
            }

            RectangleF clickableArea = FullScreenArea();

            result.Capacity = Math.Min(groundLabels.Count, 1000);

            for (int i = 0; i < groundLabels.Count; i++)
            {
                LabelOnGround label = groundLabels[i];
                if (label == null || label.ItemOnGround?.Path == null ||
                    !label.IsVisible || !label.Label.IsVisible)
                {
                    continue;
                }

                // Cache frequently accessed values
                Vector2 labelCenter = label.Label.GetClientRect().Center;
                if (!PointIsInClickableArea(labelCenter))
                {
                    continue;
                }

                Entity item = label.ItemOnGround;
                EntityType type = item.Type;
                string path = item.Path;

                // Check type and path conditions efficiently
                bool isValidType = type == EntityType.WorldItem ||
                                 (type == EntityType.Chest && !item.GetComponent<Chest>().OpenOnDamage) ||
                                 type == EntityType.AreaTransition;

                bool isValidPath = !string.IsNullOrEmpty(path) && (
                    path.Contains("DelveMineral") ||
                    path.Contains("AzuriteEncounterController") ||
                    path.Contains("Harvest/Irrigator") ||
                    path.Contains("Harvest/Extractor") ||
                    path.Contains(CleansingFireAltar) ||
                    path.Contains(TangleAltar) ||
                    path.Contains("CraftingUnlocks") ||
                    path.Contains("Brequel"));

                if (isValidType || isValidPath || GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null)
                {
                    result.Add(label);
                }
            }

            // Sort by distance if we have items
            if (result.Count > 1)
            {
                result.Sort((a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));
            }

            return result;
        }

        private IEnumerator ClickLabel(Element? altar = null)
        {
            Stopwatch ClickLabelTimer = Stopwatch.StartNew();

            try
            {
                if (Timer.ElapsedMilliseconds < 60 + Random.Next(0, 10) || !canClick())
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
                    if (nextLabel.ItemOnGround.Path.Contains(CleansingFireAltar) || nextLabel.ItemOnGround.Path.Contains(TangleAltar))
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
        private static bool ElementContainsAnyStrings(Element? root, IEnumerable<string> patterns)
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

        private static bool IsBasicChest(LabelOnGround label)
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
                if ((highlightEater || highlightExarch || clickEater || clickExarch) && !string.IsNullOrEmpty(path) && (path.Contains(CleansingFireAltar) || path.Contains(TangleAltar)))
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

                if (Settings.LeftHanded)
                {
                    Mouse.RightClick();
                }
                else
                {
                    Mouse.LeftClick();
                }

                Mouse.blockInput(false);
                waitingForCorruption = false;
            }).Start();
        }

        private decimal CalculateUpsideWeight(List<string> upsides)
        {
            decimal totalWeight = 0;
            if (upsides == null) return totalWeight;

            foreach (string upside in upsides)
            {
                if (string.IsNullOrEmpty(upside)) continue;

                // Use the ModTiers dictionary from settings instead of reflection
                int weight = Settings.GetModTier(upside);
                totalWeight += weight;
            }
            return totalWeight;
        }

        private decimal CalculateDownsideWeight(List<string> downsides)
        {
            decimal totalWeight = 1; // Start with 1 to avoid division by zero
            if (downsides == null) return totalWeight;

            foreach (string downside in downsides)
            {
                if (string.IsNullOrEmpty(downside)) continue;

                // Use the ModTiers dictionary from settings instead of reflection
                int weight = Settings.GetModTier(downside);
                totalWeight += weight;
            }
            return totalWeight;
        }

        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
