using ClickIt.Utils;
using ClickIt.Components;
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
        private Stopwatch Timer { get; } = new Stopwatch();
        private Stopwatch SecondTimer { get; } = new Stopwatch();
        private Random Random { get; } = new Random();
        private TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }
        private readonly Stopwatch renderTimer = new Stopwatch();
        private readonly Stopwatch altarCoroutineTimer = new Stopwatch();
        private readonly Stopwatch clickCoroutineTimer = new Stopwatch();
        private Coroutine? altarCoroutine;
        private Coroutine? clickLabelCoroutine;
        private bool isInputCurrentlyBlocked = false;
        private readonly Stopwatch lastHotkeyReleaseTimer = new Stopwatch();
        private const int HOTKEY_RELEASE_FAILSAFE_MS = 5000;
        private bool lastHotkeyState = false;
        private Services.AreaService? areaService;
        private Services.AltarService? altarService;
        private Services.LabelFilterService? labelFilterService;
        private Utils.InputHandler? inputHandler;
        private Rendering.DebugRenderer? debugRenderer;
        private Utils.WeightCalculator? weightCalculator;
        private Rendering.AltarDisplayRenderer? altarDisplayRenderer;
        private RectangleF FullScreenRectangle { get; set; }
        private RectangleF HealthAndFlaskRectangle { get; set; }
        private RectangleF ManaAndSkillsRectangle { get; set; }
        private RectangleF BuffsAndDebuffsRectangle { get; set; }
        private readonly List<string> recentErrors = new List<string>();
        private const int MAX_ERRORS_TO_TRACK = 10;
        public override void OnLoad()
        {
            CanUseMultiThreading = true;
        }
        public override void OnClose()
        {
            try
            {
                ForceUnblockInput("Plugin closing");
            }
            catch (Exception ex)
            {
                Mouse.blockInput(false);
                LogError($"Error during plugin shutdown: {ex.Message}");
            }
            base.OnClose();
        }
        public override bool Initialise()
        {
            Settings.ReportBugButton.OnPressed += () => { _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues"); };
            CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, 200);
            areaService = new Services.AreaService();
            areaService.UpdateScreenAreas(GameController);
            altarService = new Services.AltarService(Settings, CachedLabels);
            labelFilterService = new Services.LabelFilterService(Settings);
            inputHandler = new Utils.InputHandler(Settings);
            debugRenderer = new Rendering.DebugRenderer(this, Graphics, Settings, altarService);
            weightCalculator = new Utils.WeightCalculator(Settings);
            altarDisplayRenderer = new Rendering.AltarDisplayRenderer(Graphics, Settings);
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
            var inputSafetyCoroutine = new Coroutine(InputSafetyCoroutine(), this, "ClickIt.InputSafety");
            _ = Core.ParallelRunner.Run(inputSafetyCoroutine);
            inputSafetyCoroutine.Priority = CoroutinePriority.Critical;
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
            renderTimer.Restart();
            RenderDebugFrames();
            if (Settings.DebugMode && Settings.RenderDebug)
            {
                RenderDetailedDebugInfo();
            }
            RenderAltarComponents();
            renderTimer.Stop();
        }
        private void RenderDetailedDebugInfo()
        {
            int startY = 120;
            int lineHeight = 18;
            int columnWidth = 300;
            int col1X = 10;
            int yPos = startY;
            yPos = debugRenderer?.RenderPluginStatusDebug(col1X, yPos, lineHeight) ?? yPos;
            yPos = debugRenderer?.RenderPerformanceDebug(col1X, yPos, lineHeight) ?? yPos;
            yPos = debugRenderer?.RenderInputDebug(col1X, yPos, lineHeight) ?? yPos;
            yPos = debugRenderer?.RenderGameStateDebug(col1X, yPos, lineHeight) ?? yPos;
            debugRenderer?.RenderAltarServiceDebug(col1X, yPos, lineHeight);
            int col2X = col1X + columnWidth;
            yPos = startY;
            yPos = debugRenderer?.RenderAltarDebug(col2X, yPos, lineHeight) ?? yPos;
            yPos = debugRenderer?.RenderLabelsDebug(col2X, yPos, lineHeight) ?? yPos;
            debugRenderer?.RenderErrorsDebug(col2X, yPos, lineHeight);
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
            var altarWeights = weightCalculator?.CalculateAltarWeights(altar) ?? new Utils.AltarWeights();
            RectangleF topModsRect = altar.TopMods.Element.GetClientRect();
            RectangleF bottomModsRect = altar.BottomMods.Element.GetClientRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;
            Vector2 bottomModsTopLeft = bottomModsRect.TopLeft;
            altarDisplayRenderer?.DetermineAltarChoice(altar, altarWeights, topModsRect, bottomModsRect, topModsTopLeft);
            altarDisplayRenderer?.DrawWeightTexts(altarWeights, topModsTopLeft, bottomModsTopLeft);
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
                TrackError(message);
            }
        }
        public void LogError(bool localDebug, string message, int frame = 0)
        {
            if (localDebug && Settings.DebugMode)
            {
                base.LogError(message, frame);
                TrackError(message);
            }
        }
        private void TrackError(string errorMessage)
        {
            try
            {
                string timestampedError = $"[{DateTime.Now:HH:mm:ss}] {errorMessage}";
                recentErrors.Add(timestampedError);
                if (recentErrors.Count > MAX_ERRORS_TO_TRACK)
                {
                    recentErrors.RemoveAt(0);
                }
            }
            catch (Exception)
            {
            }
        }
        private bool canClick()
        {
            return inputHandler?.CanClick(GameController) ?? false;
        }
        private void SafeBlockInput(bool block)
        {
            try
            {
                if (block)
                {
                    if (!isInputCurrentlyBlocked)
                    {
                        Mouse.blockInput(true);
                        isInputCurrentlyBlocked = true;
                    }
                }
                else
                {
                    if (isInputCurrentlyBlocked)
                    {
                        Mouse.blockInput(false);
                        isInputCurrentlyBlocked = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ForceUnblockInput($"SafeBlockInput exception: {ex.Message}");
            }
        }
        private void ForceUnblockInput(string reason)
        {
            try
            {
                Mouse.blockInput(false);
                isInputCurrentlyBlocked = false;
                LogError($"EMERGENCY INPUT UNBLOCK: {reason} at {DateTime.Now:HH:mm:ss.fff}", 1);
            }
            catch (Exception ex)
            {
                LogError($"CRITICAL: Failed to force unblock input: {ex.Message}", 1);
            }
        }
        private IEnumerator InputSafetyCoroutine()
        {
            while (Settings.Enable)
            {
                bool shouldWaitLong = false;
                try
                {
                    PerformSafetyChecks();
                }
                catch (Exception ex)
                {
                    ForceUnblockInput($"InputSafetyCoroutine exception: {ex.Message}");
                    shouldWaitLong = true;
                }
                if (shouldWaitLong)
                {
                    yield return new WaitTime(1000);
                }
                else
                {
                    yield return new WaitTime(100);
                }
            }
            ForceUnblockInput("Plugin disabled - cleanup");
        }
        private void PerformSafetyChecks()
        {
#pragma warning disable CS0618
            bool currentHotkeyState = ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value);
#pragma warning restore CS0618
            if (currentHotkeyState != lastHotkeyState)
            {
                if (!currentHotkeyState)
                {
                    lastHotkeyReleaseTimer.Restart();
                }
                else
                {
                    lastHotkeyReleaseTimer.Stop();
                }
                lastHotkeyState = currentHotkeyState;
            }
            if (!currentHotkeyState && isInputCurrentlyBlocked &&
                lastHotkeyReleaseTimer.IsRunning &&
                lastHotkeyReleaseTimer.ElapsedMilliseconds > HOTKEY_RELEASE_FAILSAFE_MS)
            {
                ForceUnblockInput($"Hotkey released for {lastHotkeyReleaseTimer.ElapsedMilliseconds}ms");
            }
            if (isInputCurrentlyBlocked && GameController?.Game?.IngameState?.InGame != true)
            {
                ForceUnblockInput("Not in game");
            }
        }
        private IEnumerator ScanForAltarsLogic()
        {
            altarCoroutineTimer.Restart();
            altarService?.ProcessAltarScanningLogic(LogMessage, LogError);
            altarCoroutineTimer.Stop();
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
#pragma warning disable CS0618
            if (ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value))
#pragma warning restore CS0618
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
                Vector2 labelCenter = label.Label.GetClientRect().Center;
                if (!PointIsInClickableArea(labelCenter))
                {
                    continue;
                }
                Entity item = label.ItemOnGround;
                EntityType type = item.Type;
                string path = item.Path;
                bool isValidType = type == EntityType.WorldItem ||
                                 (type == EntityType.Chest && !item.GetComponent<Chest>().OpenOnDamage) ||
                                 type == EntityType.AreaTransition;
                bool isValidPath = !string.IsNullOrEmpty(path) && (
                    path.Contains("DelveMineral") ||
                    path.Contains("AzuriteEncounterController") ||
                    path.Contains("Harvest/Irrigator") ||
                    path.Contains("Harvest/Extractor") ||
                    path.Contains("CleansingFireAltar") ||
                    path.Contains("TangleAltar") ||
                    path.Contains("CraftingUnlocks") ||
                    path.Contains("Brequel") ||
                    path.Contains("CrimsonIron") ||
                    path.Contains("copper_altar") ||
                    path.Contains("PetrifiedWood") ||
                    path.Contains("Bismuth") ||
                    path.Contains("Verisium") ||
                    path.Contains("ClosedDoorPast"));
                if (isValidType || isValidPath || GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null)
                {
                    result.Add(label);
                }
            }
            if (result.Count > 1)
            {
                result.Sort((a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));
            }
            return result;
        }
        private IEnumerator ClickLabel(Element? altar = null)
        {
            if (Timer.ElapsedMilliseconds < 30 + Random.Next(0, 20) || !canClick())
            {
                workFinished = true;
                yield break;
            }
            Timer.Restart();
            clickCoroutineTimer.Restart();
            if (altar != null)
            {
                yield return ProcessAltarClickSimple(altar);
            }
            else
            {
                yield return ProcessRegularClickSimple();
            }
            clickCoroutineTimer.Stop();
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
                SafeBlockInput(true);
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
            SafeBlockInput(false);
            yield return new WaitTime(Random.Next(50, 150));
        }
        private IEnumerator ProcessRegularClickSimple()
        {
            if (!(inputHandler?.CanClick(GameController) ?? false))
            {
                yield break;
            }
            yield return ProcessAltarClicking();
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
            string path = item.Path ?? "";
            bool isAltar = path.Contains("CleansingFireAltar") || path.Contains("TangleAltar");
            if (isAltar)
            {
                yield break;
            }
            RectangleF windowArea = GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 clickPos = nextLabel.Label.GetClientRect().Center + windowTopLeft;
            if (Settings.BlockUserInput.Value)
            {
                SafeBlockInput(true);
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
            SafeBlockInput(false);
            yield return new WaitTime(Random.Next(50, 150));
        }
        private IEnumerator ProcessAltarClicking()
        {
            List<PrimaryAltarComponent> altarSnapshot = altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
            bool clickEater = Settings.ClickEaterAltars;
            bool clickExarch = Settings.ClickExarchAltars;
            bool leftHanded = Settings.LeftHanded;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                if (!ShouldClickAltar(altar, clickEater, clickExarch))
                {
                    continue;
                }

                Element? boxToClick = GetAltarElementToClick(altar);
                if (boxToClick != null)
                {
                    yield return ClickAltarElement(boxToClick, leftHanded);
                    yield break;
                }
            }
        }

        private bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
        {
            return (altar.AltarType == AltarType.EaterOfWorlds && clickEater) ||
                   (altar.AltarType == AltarType.SearingExarch && clickExarch);
        }

        private Element? GetAltarElementToClick(PrimaryAltarComponent altar)
        {
            var altarWeights = weightCalculator?.CalculateAltarWeights(altar) ?? new Utils.AltarWeights();
            RectangleF topModsRect = altar.TopMods.Element.GetClientRect();
            RectangleF bottomModsRect = altar.BottomMods.Element.GetClientRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;

            Element? boxToClick = altarDisplayRenderer?.DetermineAltarChoice(altar, altarWeights, topModsRect, bottomModsRect, topModsTopLeft);

            if (boxToClick != null &&
                PointIsInClickableArea(boxToClick.GetClientRect().Center, altar.AltarType.ToString()) &&
                boxToClick.IsVisible)
            {
                return boxToClick;
            }

            return null;
        }

        private IEnumerator ClickAltarElement(Element element, bool leftHanded)
        {
            RectangleF windowArea = GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 clickPos = element.GetClientRect().Center + windowTopLeft;

            if (Settings.BlockUserInput.Value)
            {
                SafeBlockInput(true);
            }

            ExileCore.Input.SetCursorPos(clickPos);
            yield return new WaitTime(20);

            if (leftHanded)
            {
                Mouse.RightClick();
            }
            else
            {
                Mouse.LeftClick();
            }

            SafeBlockInput(false);
            yield return new WaitTime(Random.Next(200, 300));
        }
        public static Element? GetElementByString(Element? root, string str)
        {
            return Services.ElementService.GetElementByString(root, str);
        }
        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
