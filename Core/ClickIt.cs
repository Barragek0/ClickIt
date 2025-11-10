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
        private readonly Queue<long> clickCoroutineTimings = new Queue<long>(10);
        private readonly Queue<long> altarCoroutineTimings = new Queue<long>(10);
        private readonly Queue<long> renderTimings = new Queue<long>(60);
        private readonly Stopwatch fpsTimer = new Stopwatch();
        private int frameCount = 0;
        private double currentFPS = 0;
        private Coroutine? altarCoroutine;
        private Coroutine? clickLabelCoroutine;
        private bool isInputCurrentlyBlocked = false;
        private readonly Stopwatch lastHotkeyReleaseTimer = new Stopwatch();
        private readonly Stopwatch lastRenderTimer = new Stopwatch();
        private readonly Stopwatch lastTickTimer = new Stopwatch();
        private const int HOTKEY_RELEASE_FAILSAFE_MS = 5000;
        private bool lastHotkeyState = false;
        private Services.AreaService? areaService;
        private Services.AltarService? altarService;
        private Services.LabelFilterService? labelFilterService;
        private Utils.InputHandler? inputHandler;
        private Rendering.DebugRenderer? debugRenderer;

        private Rendering.AltarDisplayRenderer? altarDisplayRenderer;
        private Services.ClickService? clickService;
        private RectangleF FullScreenRectangle { get; set; }
        private RectangleF HealthAndFlaskRectangle { get; set; }
        private RectangleF ManaAndSkillsRectangle { get; set; }
        private RectangleF BuffsAndDebuffsRectangle { get; set; }
        private readonly List<string> recentErrors = new List<string>();
        private const int MAX_ERRORS_TO_TRACK = 10;

        // Public accessors for performance metrics
        public double CurrentFPS => currentFPS;
        public Queue<long> RenderTimings => renderTimings;
        public override void OnLoad()
        {
            CanUseMultiThreading = true;
        }
        public override void OnClose()
        {
            ForceUnblockInput("Plugin closing");

            base.OnClose();
        }
        public override bool Initialise()
        {
            Settings.ReportBugButton.OnPressed += () => { _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues"); };
            CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, 50);
            areaService = new Services.AreaService();
            areaService.UpdateScreenAreas(GameController);
            altarService = new Services.AltarService(this, Settings, CachedLabels);
            labelFilterService = new Services.LabelFilterService(Settings);
            inputHandler = new Utils.InputHandler(Settings, SafeBlockInput);
            debugRenderer = new Rendering.DebugRenderer(this, Graphics, Settings, altarService, areaService);
            var weightCalculator = new Utils.WeightCalculator(Settings);
            altarDisplayRenderer = new Rendering.AltarDisplayRenderer(Graphics, Settings, GameController, weightCalculator, altarService, LogError);
            clickService = new Services.ClickService(
                Settings,
                GameController,
                LogMessage,
                LogError,
                altarService,
                weightCalculator,
                altarDisplayRenderer,
                Random,
                PointIsInClickableArea,
                SafeBlockInput,
                inputHandler,
                labelFilterService,
                GroundItemsVisible,
                CachedLabels);
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
            inputSafetyCoroutine.Priority = CoroutinePriority.High;

            Settings.EnsureAllModsHaveWeights();

            // Start monitoring timers
            lastRenderTimer.Start();
            lastTickTimer.Start();

            return true;
        }
        private bool PointIsInClickableArea(Vector2 point, string? path = null)
        {
            areaService?.UpdateScreenAreas(GameController);
            return areaService?.PointIsInClickableArea(point) ?? false;
        }
        public override void Render()
        {
            bool debugMode = Settings.DebugMode;
            bool renderDebug = Settings.RenderDebug;
            bool hasDebugRendering = debugMode && renderDebug;

            int altarCount = altarService?.GetAltarComponents()?.Count ?? 0;
            bool hasAltars = altarCount > 0;

            if (!hasDebugRendering && !hasAltars)
            {
                return; // Skip all timer operations for no-op renders
            }

            // Start timing only when actually rendering
            renderTimer.Restart();

            frameCount++;
            if (!fpsTimer.IsRunning)
            {
                fpsTimer.Start();
            }

            if (fpsTimer.ElapsedMilliseconds >= 1000)
            {
                currentFPS = frameCount / (fpsTimer.ElapsedMilliseconds / 1000.0);
                frameCount = 0;
                fpsTimer.Restart();
            }

            if (hasDebugRendering)
            {
                debugRenderer?.RenderDebugFrames(Settings);
                debugRenderer?.RenderDetailedDebugInfo(Settings, renderTimer);
            }

            if (hasAltars)
            {
                RenderAltarComponents();
            }

            renderTimer.Stop();
            renderTimings.Enqueue(renderTimer.ElapsedMilliseconds);
            if (renderTimings.Count > 60)
            {
                renderTimings.Dequeue();
            }
        }
        private void RenderAltarComponents()
        {
            altarDisplayRenderer?.RenderAltarComponents();
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
                LogErrorWithWrapping(message, frame);
                TrackError(message);
            }
        }
        public void LogError(bool localDebug, string message, int frame = 0)
        {
            if (localDebug && Settings.DebugMode)
            {
                LogErrorWithWrapping(message, frame);
                TrackError(message);
            }
        }
        private void LogErrorWithWrapping(string message, int frame = 0)
        {
            const int maxLineLength = 100; // Maximum characters per line to avoid going off screen

            if (message.Length <= maxLineLength)
            {
                base.LogError(message, frame);
                return;
            }

            // Split long message into multiple lines
            int startIndex = 0;
            int lineNumber = 1;

            while (startIndex < message.Length)
            {
                int remainingLength = message.Length - startIndex;
                int currentLineLength = Math.Min(maxLineLength, remainingLength);

                // Try to break at a space to avoid splitting words
                if (currentLineLength < remainingLength)
                {
                    int lastSpaceIndex = message.LastIndexOf(' ', startIndex + currentLineLength, currentLineLength);
                    if (lastSpaceIndex > startIndex)
                    {
                        currentLineLength = lastSpaceIndex - startIndex;
                    }
                }

                string line = message.Substring(startIndex, currentLineLength).TrimEnd();

                // Add line number prefix for continuation lines
                if (lineNumber == 1)
                {
                    base.LogError(line, frame);
                }
                else
                {
                    base.LogError($"  [{lineNumber}] {line}", frame);
                }

                startIndex += currentLineLength;
                // Skip leading spaces on continuation lines
                while (startIndex < message.Length && message[startIndex] == ' ')
                {
                    startIndex++;
                }

                lineNumber++;
            }
        }
        private void TrackError(string errorMessage)
        {
            string timestampedError = $"[{DateTime.Now:HH:mm:ss}] {errorMessage}";
            recentErrors.Add(timestampedError);
            if (recentErrors.Count > MAX_ERRORS_TO_TRACK)
            {
                recentErrors.RemoveAt(0);
            }
        }
        private bool canClick()
        {
            return inputHandler?.CanClick(GameController) ?? false;
        }
        private void SafeBlockInput(bool block)
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

        private void ForceUnblockInput(string reason)
        {
            Mouse.blockInput(false);
            isInputCurrentlyBlocked = false;
            LogError($"CRITICAL: Input forcibly unblocked. Reason: {reason}", 10);
        }

        private IEnumerator InputSafetyCoroutine()
        {
            while (Settings.Enable)
            {
                PerformSafetyChecks();
                yield return new WaitTime(1000);
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
            altarService?.ProcessAltarScanningLogic();
            altarCoroutineTimer.Stop();

            // Track timing for averaging
            altarCoroutineTimings.Enqueue(altarCoroutineTimer.ElapsedMilliseconds);
            if (altarCoroutineTimings.Count > 10)
            {
                altarCoroutineTimings.Dequeue();
            }

            altarCoroutine?.Pause();
            yield break;
        }
        private bool GroundItemsVisible()
        {
            // Avoid triggering cache refresh if not necessary
            var cachedValue = CachedLabels?.Value;
            if (cachedValue == null || cachedValue.Count < 1)
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
            bool hotkeyPressed = ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value);
#pragma warning restore CS0618

            if (hotkeyPressed)
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
            if (SecondTimer.ElapsedMilliseconds > 200)
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
                    path.Contains("ClosedDoorPast") ||
                    path.Contains("LegionInitiator"));
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
        private IEnumerator ClickLabel()
        {
            if (Timer.ElapsedMilliseconds < 60 + Random.Next(0, 11) || !canClick())
            {
                workFinished = true;
                yield break;
            }

            Timer.Restart();
            clickCoroutineTimer.Restart();
            if (clickService != null)
            {
                yield return clickService.ProcessRegularClick();
            }
            clickCoroutineTimer.Stop();

            // Track timing for averaging
            clickCoroutineTimings.Enqueue(clickCoroutineTimer.ElapsedMilliseconds);
            if (clickCoroutineTimings.Count > 10)
            {
                clickCoroutineTimings.Dequeue();
            }

            workFinished = true;
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
