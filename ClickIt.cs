using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using SharpDX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ClickIt
{
#nullable enable
    public class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        private const string CleansingFireAltar = "CleansingFireAltar";
        private const string TangleAltar = "TangleAltar";
        private const string Brequel = "Brequel";
        private const string CrimsonIron = "CrimsonIron";
        private const string CopperAltar = "copper_altar";
        private const string Verisium = "Verisium";
        private const string ReportBugMessage = "\nPlease report this as a bug on github";

        private Stopwatch Timer { get; } = new Stopwatch();
        private Stopwatch SecondTimer { get; } = new Stopwatch();
        private Random Random { get; } = new Random();
        private TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder ss, int count);



        private Coroutine? altarCoroutine;
        private Coroutine? clickLabelCoroutine;

        // Verisium hold click state
        private bool isHoldingVerisiumClick = false;
        private readonly Stopwatch verisiumHoldTimer = new Stopwatch();
        private const int VERISIUM_HOLD_FAILSAFE_MS = 10000; // 10 seconds

        // Services
        private Services.AreaService? areaService;
        private Services.AltarService? altarService;
        private Services.LabelFilterService? labelFilterService;
        private Input.InputHandler? inputHandler;

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

            // Initialize services
            areaService = new Services.AreaService();
            areaService.UpdateScreenAreas(GameController);

            altarService = new Services.AltarService(Settings, CachedLabels);
            labelFilterService = new Services.LabelFilterService(Settings);
            inputHandler = new Input.InputHandler(Settings);

            // Initialize legacy rectangles for backward compatibility (to be removed)
            FullScreenRectangle = areaService.FullScreenRectangle;
            HealthAndFlaskRectangle = areaService.HealthAndFlaskRectangle;
            ManaAndSkillsRectangle = areaService.ManaAndSkillsRectangle;
            BuffsAndDebuffsRectangle = areaService.BuffsAndDebuffsRectangle;

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



        private bool PointIsInClickableArea(Vector2 point, string? path = null)
        {
            areaService?.UpdateScreenAreas(GameController);
            return areaService?.PointIsInClickableArea(point) ?? false;
        }

        public override void Render()
        {
            RenderDebugFrames();

            var altarComps = altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
            if (altarComps.Count == 0)
            {
                return;
            }

            RenderAltarComponents();
        }

        private void RenderDebugFrames()
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
        }

        private void RenderAltarComponents()
        {
            List<PrimaryAltarComponent> altarSnapshot = altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
            bool clickEater = Settings.ClickEaterAltars;
            bool clickExarch = Settings.ClickExarchAltars;
            bool leftHanded = Settings.LeftHanded;
            Vector2 windowTopLeft = GameController.Window.GetWindowRectangleTimeCache.TopLeft;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                RenderSingleAltar(altar, clickEater, clickExarch, leftHanded, windowTopLeft);
            }
        }

        private void RenderSingleAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch, bool leftHanded, Vector2 windowTopLeft)
        {
            var altarWeights = CalculateAltarWeights(altar);
            RectangleF topModsRect = altar.TopMods.Element.GetClientRect();
            RectangleF bottomModsRect = altar.BottomMods.Element.GetClientRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;
            Vector2 bottomModsTopLeft = bottomModsRect.TopLeft;

            Element? boxToClick = DetermineAltarChoice(altar, altarWeights, topModsRect, bottomModsRect, topModsTopLeft);

            DrawWeightTexts(altarWeights, topModsTopLeft, bottomModsTopLeft);

            HandleAltarClick(altar, boxToClick, clickEater, clickExarch, leftHanded, windowTopLeft);
        }

        private AltarWeights CalculateAltarWeights(PrimaryAltarComponent altar)
        {
            decimal TopUpsideWeight = CalculateUpsideWeight(altar.TopMods.Upsides);
            decimal TopDownsideWeight = CalculateDownsideWeight(altar.TopMods.Downsides);
            decimal BottomUpsideWeight = CalculateUpsideWeight(altar.BottomMods.Upsides);
            decimal BottomDownsideWeight = CalculateDownsideWeight(altar.BottomMods.Downsides);

            return new AltarWeights
            {
                TopUpsideWeight = TopUpsideWeight,
                TopDownsideWeight = TopDownsideWeight,
                BottomUpsideWeight = BottomUpsideWeight,
                BottomDownsideWeight = BottomDownsideWeight,
                TopDownside1Weight = CalculateDownsideWeight([altar.TopMods.FirstDownside]),
                TopDownside2Weight = CalculateDownsideWeight([altar.TopMods.SecondDownside]),
                BottomDownside1Weight = CalculateDownsideWeight([altar.BottomMods.FirstDownside]),
                BottomDownside2Weight = CalculateDownsideWeight([altar.BottomMods.SecondDownside]),
                TopUpside1Weight = CalculateUpsideWeight([altar.TopMods.FirstUpside]),
                TopUpside2Weight = CalculateUpsideWeight([altar.TopMods.SecondUpside]),
                BottomUpside1Weight = CalculateUpsideWeight([altar.BottomMods.FirstUpside]),
                BottomUpside2Weight = CalculateUpsideWeight([altar.BottomMods.SecondUpside]),
                TopWeight = Math.Round(TopUpsideWeight / TopDownsideWeight, 2),
                BottomWeight = Math.Round(BottomUpsideWeight / BottomDownsideWeight, 2)
            };
        }

        private Element? DetermineAltarChoice(PrimaryAltarComponent altar, AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 offset120_Minus60 = new(120, -70);
            Vector2 offset120_Minus25 = new(120, -25);

            if (weights.TopUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top upside", altar.TopMods.FirstUpside, altar.TopMods.SecondUpside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.TopDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top downside", altar.TopMods.FirstDownside, altar.TopMods.SecondDownside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.BottomUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom upside", altar.BottomMods.FirstUpside, altar.BottomMods.SecondUpside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.BottomDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom downside", altar.BottomMods.FirstDownside, altar.BottomMods.SecondDownside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            return EvaluateAltarWeights(weights, altar, topModsRect, bottomModsRect, topModsTopLeft + offset120_Minus60, topModsTopLeft + offset120_Minus25);
        }

        private Element? EvaluateAltarWeights(AltarWeights weights, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos1, Vector2 textPos2)
        {

            if ((weights.TopDownside1Weight >= 90 || weights.TopDownside2Weight >= 90) && (weights.BottomDownside1Weight >= 90 || weights.BottomDownside2Weight >= 90))
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nBoth options have downsides with a weight of 90+ that may brick your build.", textPos1, Color.Orange, 30);
                Graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                Graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return null;
            }

            if (weights.TopUpside1Weight >= 90 || weights.TopUpside2Weight >= 90)
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                Graphics.DrawFrame(topModsRect, Color.LawnGreen, 3);
                Graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return altar.TopButton.Element;
            }

            if (weights.BottomUpside1Weight >= 90 || weights.BottomUpside2Weight >= 90)
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                Graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                Graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 3);
                return altar.BottomButton.Element;
            }

            if (weights.TopDownside1Weight >= 90 || weights.TopDownside2Weight >= 90)
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the top downsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                Graphics.DrawFrame(topModsRect, Color.OrangeRed, 3);
                Graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 2);
                return altar.BottomButton.Element;
            }

            if (weights.BottomDownside1Weight >= 90 || weights.BottomDownside2Weight >= 90)
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the bottom downsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                Graphics.DrawFrame(topModsRect, Color.LawnGreen, 2);
                Graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 3);
                return altar.TopButton.Element;
            }

            if (weights.TopWeight > weights.BottomWeight)
            {
                Graphics.DrawFrame(topModsRect, Color.LawnGreen, 3);
                Graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return altar.TopButton.Element;
            }

            if (weights.BottomWeight > weights.TopWeight)
            {
                Graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                Graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 3);
                return altar.BottomButton.Element;
            }

            _ = Graphics.DrawText("Mods have equal weight, you should choose.", textPos2, Color.Orange, 30);
            DrawYellowFrames(topModsRect, bottomModsRect);
            return null;
        }

        private void DrawUnrecognizedWeightText(string weightType, string mod1, string mod2, Vector2 position)
        {
            _ = Graphics.DrawText($"{weightType} weights couldn't be recognised\n1:{mod1}\n2:{mod2}{ReportBugMessage}", position, Color.Orange, 30);
        }

        private void DrawYellowFrames(RectangleF topModsRect, RectangleF bottomModsRect)
        {
            Graphics.DrawFrame(topModsRect, Color.Yellow, 2);
            Graphics.DrawFrame(bottomModsRect, Color.Yellow, 2);
        }

        private void DrawWeightTexts(AltarWeights weights, Vector2 topModsTopLeft, Vector2 bottomModsTopLeft)
        {
            Vector2 offset5_Minus32 = new(5, -32);
            Vector2 offset5_Minus20 = new(5, -20);
            Vector2 offset10_Minus32 = new(10, -32);
            Vector2 offset10_Minus20 = new(10, -20);
            Vector2 offset10_5 = new(10, 5);
            Color colorLawnGreen = Color.LawnGreen;
            Color colorOrangeRed = Color.OrangeRed;
            Color colorYellow = Color.Yellow;

            _ = Graphics.DrawText("Upside: " + weights.TopUpsideWeight, topModsTopLeft + offset5_Minus32, colorLawnGreen, 14);
            _ = Graphics.DrawText("Downside: " + weights.TopDownsideWeight, topModsTopLeft + offset5_Minus20, colorOrangeRed, 14);
            _ = Graphics.DrawText("Upside: " + weights.BottomUpsideWeight, bottomModsTopLeft + offset10_Minus32, colorLawnGreen, 14);
            _ = Graphics.DrawText("Downside: " + weights.BottomDownsideWeight, bottomModsTopLeft + offset10_Minus20, colorOrangeRed, 14);

            Color topWeightColor = GetWeightColor(weights.TopWeight, weights.BottomWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            Color bottomWeightColor = GetWeightColor(weights.BottomWeight, weights.TopWeight, colorLawnGreen, colorOrangeRed, colorYellow);

            _ = Graphics.DrawText("" + weights.TopWeight, topModsTopLeft + offset10_5, topWeightColor, 18);
            _ = Graphics.DrawText("" + weights.BottomWeight, bottomModsTopLeft + offset10_5, bottomWeightColor, 18);
        }

        private static Color GetWeightColor(decimal weight1, decimal weight2, Color winColor, Color loseColor, Color tieColor)
        {
            if (weight1 > weight2) return winColor;
            if (weight2 > weight1) return loseColor;
            return tieColor;
        }

        private void HandleAltarClick(PrimaryAltarComponent altar, Element? boxToClick, bool clickEater, bool clickExarch, bool leftHanded, Vector2 windowTopLeft)
        {
            bool shouldClick = ((altar.AltarType == AltarType.EaterOfWorlds && clickEater) || (altar.AltarType == AltarType.SearingExarch && clickExarch)) &&
                               boxToClick != null &&
                               PointIsInClickableArea(boxToClick.GetClientRect().Center, altar.AltarType.ToString()) &&
                               boxToClick.IsVisible &&
                               canClick();

            if (shouldClick && boxToClick != null)
            {
                Mouse.blockInput(true);
                LogMessage("Moving mouse for altar", 5);
                ExileCore.Input.SetCursorPos(boxToClick.GetClientRect().Center + windowTopLeft);
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

        private struct AltarWeights
        {
            public decimal TopUpsideWeight;
            public decimal TopDownsideWeight;
            public decimal BottomUpsideWeight;
            public decimal BottomDownsideWeight;
            public decimal TopDownside1Weight;
            public decimal TopDownside2Weight;
            public decimal BottomDownside1Weight;
            public decimal BottomDownside2Weight;
            public decimal TopUpside1Weight;
            public decimal TopUpside2Weight;
            public decimal BottomUpside1Weight;
            public decimal BottomUpside2Weight;
            public decimal TopWeight;
            public decimal BottomWeight;
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
            return inputHandler?.CanClick(GameController) ?? false;
        }

        private IEnumerator ScanForAltarsLogic()
        {
            altarService?.ProcessAltarScanningLogic(LogMessage, LogError);
            altarCoroutine?.Pause();
            yield break;
        }





        private bool GroundItemsVisible()
        {
            if (CachedLabels?.Value?.Count < 1)
            {
                LogMessage("(ClickIt) No ground items found");

                return false;
            }

            return true;
        }





        public static List<Element> GetElementsByStringContains(Element label, string str)
        {
            return Services.ElementService.GetElementsByStringContains(label, str);
        }

        private bool workFinished;

        public override Job? Tick()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value))
#pragma warning restore CS0618 // Type or member is obsolete
            {

                if (clickLabelCoroutine?.IsDone == true)
                {
                    Coroutine firstOrDefault = Core.ParallelRunner.Coroutines.FirstOrDefault(x => x.Name == "ClickIt.ClickLogic");

                    if (firstOrDefault != null)
                    {
                        clickLabelCoroutine = firstOrDefault;
                    }
                }

                clickLabelCoroutine?.Resume();
                workFinished = false;
            }
            else
            {
                if (workFinished)
                {
                    clickLabelCoroutine?.Pause();
                }
            }
            if (SecondTimer.ElapsedMilliseconds > 500)
            {
                altarCoroutine?.Resume();
                SecondTimer.Restart();
            }
            return null;
        }

        // we need these here to keep the coroutines alive after finishing the work
        private IEnumerator MainClickLabelCoroutine()
        {
            while (Settings.Enable)
            {
                yield return ClickLabel();
            }
        }

        private IEnumerator MainScanForAltarsLogic()
        {
            while (Settings.Enable)
            {
                yield return ScanForAltarsLogic();
            }
        }







        private List<LabelOnGround> UpdateLabelComponent()
        {
            List<LabelOnGround> result = [];
            IList<LabelOnGround>? groundLabels = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabels;

            if (groundLabels == null)
            {
                return result;
            }

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
                    path.Contains(Brequel) ||
                    path.Contains(CrimsonIron) ||
                    path.Contains(CopperAltar) ||
                    path.Contains(Verisium));

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
            if (Timer.ElapsedMilliseconds < 60 + Random.Next(0, 10) || !canClick())
            {
                workFinished = true;
                yield break;
            }

            Timer.Restart();

            if (altar != null)
            {
                // Handle altar clicks through the service
                yield return ProcessAltarClickSimple(altar);
            }
            else
            {
                // Handle regular clicks
                yield return ProcessRegularClickSimple();
            }

            workFinished = true;
        }

        private IEnumerator ProcessAltarClickSimple(Element altar)
        {
            if (!(inputHandler?.CanClick(GameController) ?? false))
            {
                yield break;
            }

            RectangleF windowArea = GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 clickPos = altar.GetClientRect().Center + windowTopLeft;

            if (Settings.BlockUserInput.Value)
            {
                Mouse.blockInput(true);
            }

            ExileCore.Input.SetCursorPos(clickPos);
            if (Settings.LeftHanded.Value)
            {
                Mouse.RightClick();
            }
            else
            {
                Mouse.LeftClick();
            }

            Mouse.blockInput(false);
            yield return new WaitTime(Random.Next(50, 150));
        }

        private IEnumerator ProcessRegularClickSimple()
        {
            if (!(inputHandler?.CanClick(GameController) ?? false))
            {
                yield break;
            }

            if (!GroundItemsVisible())
            {
                yield break;
            }

            LabelOnGround? nextLabel = labelFilterService?.GetNextLabelToClick(CachedLabels?.Value ?? new List<LabelOnGround>());
            if (nextLabel == null)
            {
                yield break;
            }

            Entity item = nextLabel.ItemOnGround;
            if (item.DistancePlayer > Settings.ClickDistance)
            {
                yield break;
            }

            // Check if this is Verisium and handle it specially
            string path = item.Path ?? "";
            bool isVerisium = Settings.ClickVerisium.Value && path.Contains(Verisium);

            RectangleF windowArea = GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 clickPos = nextLabel.Label.GetClientRect().Center + windowTopLeft;

            if (isVerisium)
            {
                yield return ProcessVerisiumHoldClick(clickPos);
            }
            else
            {
                // Regular click logic
                if (Settings.BlockUserInput.Value)
                {
                    Mouse.blockInput(true);
                }

                ExileCore.Input.SetCursorPos(clickPos);
                if (Settings.LeftHanded.Value)
                {
                    Mouse.RightClick();
                }
                else
                {
                    Mouse.LeftClick();
                }

                Mouse.blockInput(false);
                yield return new WaitTime(Random.Next(50, 150));
            }
        }

        private IEnumerator ProcessVerisiumHoldClick(Vector2 clickPos)
        {
            // Start holding if not already holding
            if (!isHoldingVerisiumClick)
            {
                LogMessage("Starting Verisium hold click", 3);

                if (Settings.BlockUserInput.Value)
                {
                    Mouse.blockInput(true);
                }

                ExileCore.Input.SetCursorPos(clickPos);

                // Start holding left click (press down but don't release)
                Mouse.LeftMouseDown();

                isHoldingVerisiumClick = true;
                verisiumHoldTimer.Restart();
            }

            // Check if we should continue holding (hotkey still pressed and within failsafe time)
#pragma warning disable CS0618 // Type or member is obsolete
            bool hotkeyPressed = ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value);
#pragma warning restore CS0618 // Type or member is obsolete
            bool withinFailsafeTime = verisiumHoldTimer.ElapsedMilliseconds < VERISIUM_HOLD_FAILSAFE_MS;
            bool hasVerisiumOnScreen = labelFilterService?.HasVerisiumOnScreen(CachedLabels?.Value ?? new List<LabelOnGround>()) ?? false;

            if ((!hotkeyPressed || !withinFailsafeTime || !hasVerisiumOnScreen) && isHoldingVerisiumClick)
            {
                // Stop holding
                LogMessage($"Stopping Verisium hold click - Hotkey: {hotkeyPressed}, Time: {withinFailsafeTime}, HasVerisium: {hasVerisiumOnScreen}", 3);

                Mouse.LeftMouseUp();

                if (Settings.BlockUserInput.Value)
                {
                    Mouse.blockInput(false);
                }

                isHoldingVerisiumClick = false;
                verisiumHoldTimer.Stop();
            }

            yield return new WaitTime(50); // Short delay for responsiveness
        }

        public static Element? GetElementByString(Element? root, string str)
        {
            return Services.ElementService.GetElementByString(root, str);
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
