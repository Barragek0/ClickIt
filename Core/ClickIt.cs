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
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;
using RectangleF = SharpDX.RectangleF;
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
        private Services.ShrineService? shrineService;
        private Utils.InputHandler? inputHandler;
        private Rendering.DebugRenderer? debugRenderer;

        private Rendering.AltarDisplayRenderer? altarDisplayRenderer;
        private Services.ClickService clickService = null!;
        private RectangleF FullScreenRectangle { get; set; }
        private RectangleF HealthAndFlaskRectangle { get; set; }
        private RectangleF ManaAndSkillsRectangle { get; set; }
        private RectangleF BuffsAndDebuffsRectangle { get; set; }
        private readonly List<string> recentErrors = new List<string>();
        private const int MAX_ERRORS_TO_TRACK = 10;

        public double CurrentFPS => currentFPS;
        public Services.ShrineService? ShrineService => shrineService;
        public Queue<long> RenderTimings => renderTimings;

        private Camera? camera;

        public override void OnLoad()
        {
            // Register global error handlers with instance context
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LogError($"Unhandled exception ({ex.GetType().Name}): {ex.Message}", 10);
                    LogError($"Stack: {ex.StackTrace}", 10);
                }
                LogError($"UnhandledException Event: IsTerminating={e.IsTerminating}", 10);
                ForceUnblockInput("Unhandled exception");
            };

            // Catch unobserved task exceptions so they don't get lost
            TaskScheduler.UnobservedTaskException += (s, evt) =>
            {
                evt.SetObserved();
                var ex = evt.Exception;
                if (ex != null)
                {
                    LogError($"Unobserved Task Exception: {ex.GetType().Name}: {ex.Message}", 10);
                    LogError($"Stack: {ex.StackTrace}", 10);
                }
                ForceUnblockInput("Unobserved task exception");
            };

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
            camera = GameController?.Game?.IngameState?.Camera;
            altarService = new Services.AltarService(this, Settings, CachedLabels);
            var labelFilterService = new Services.LabelFilterService(Settings, new Services.EssenceService(Settings));
            shrineService = new Services.ShrineService(GameController, camera);
            inputHandler = new Utils.InputHandler(Settings, SafeBlockInput, (msg, f) => LogMessage(true, msg, f));
            var weightCalculator = new Utils.WeightCalculator(Settings);
            debugRenderer = new Rendering.DebugRenderer(this, Graphics, Settings, altarService, areaService, weightCalculator);
            altarDisplayRenderer = new Rendering.AltarDisplayRenderer(Graphics, Settings, GameController ?? throw new InvalidOperationException("GameController is null @ altarDisplayRenderer initialize"), weightCalculator, altarService, LogMessage);
            LockManager.Instance = new Utils.LockManager(Settings);
            clickService = new Services.ClickService(
                Settings,
                GameController,
                msg => LogMessage(msg),
                LogError,
                altarService,
                weightCalculator,
                altarDisplayRenderer,
                PointIsInClickableArea,
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

            var shrineCoroutine = new Coroutine(MainShrineCoroutine(), this, "ClickIt.ShrineLogic", true);
            _ = Core.ParallelRunner.Run(shrineCoroutine);
            shrineCoroutine.Priority = CoroutinePriority.High;

            // Start input safety coroutine to enforce unblock failsafes
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
            EnqueueTiming(renderTimings, renderTimer.ElapsedMilliseconds, 60);
        }
        private void RenderAltarComponents()
        {
            altarDisplayRenderer?.RenderAltarComponents();
        }
        public void LogMessage(string message, int frame = 5)
        {
            LogMessage(false, true, message, frame);
        }

        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            LogMessage(true, localDebug, message, frame);
        }
        public void LogError(string message, int frame = 0)
        {
            if (Settings.DebugMode)
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

        private void LogMessage(bool requireLocalDebug, bool localDebugFlag, string message, int frame)
        {
            if (requireLocalDebug)
            {
                if (localDebugFlag && Settings.DebugMode && Settings.LogMessages)
                {
                    base.LogMessage(message, frame);
                }
            }
            else
            {
                if (Settings.DebugMode && Settings.LogMessages)
                {
                    base.LogMessage(message, frame);
                }
            }
        }


        // Helper to enqueue timing measurements and keep a fixed-length queue
        private void EnqueueTiming(Queue<long> queue, long value, int maxLength)
        {
            queue.Enqueue(value);
            if (queue.Count > maxLength)
            {
                queue.Dequeue();
            }
        }

        // Helper to execute an action while holding the click-element access lock (if LockManager enabled)
        private void ExecuteWithElementAccessLock(Action action)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(clickService.GetElementAccessLock()))
                {
                    action();
                }
            }
            else
            {
                action();
            }
        }
        private bool CanClick()
        {
            return inputHandler?.CanClick(GameController) ?? false;
        }
        private void SafeBlockInput(bool block)
        {
            if (block)
            {
                if (!isInputCurrentlyBlocked)
                {
                    bool result = false;
                    result = Mouse.blockInput(true);
                    if (!result)
                    {
                        LogMessage("SafeBlockInput: BlockInput(true) returned false - input may not be blocked", 10);
                    }
                    isInputCurrentlyBlocked = true;
                    LogMessage(true, "SafeBlockInput: input blocked", 5);
                }
            }
            else
            {
                if (isInputCurrentlyBlocked)
                {
                    bool result = Mouse.blockInput(false);
                    if (!result)
                    {
                        LogMessage("SafeBlockInput: BlockInput(false) returned false - input may still be blocked", 10);
                    }
                    isInputCurrentlyBlocked = false;
                    LogMessage(true, "SafeBlockInput: input unblocked", 5);
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
            bool currentHotkeyState = IsClickHotkeyPressed();
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
            EnqueueTiming(altarCoroutineTimings, altarCoroutineTimer.ElapsedMilliseconds, 10);

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
            return LabelUtils.GetElementsByStringContains(label, str);
        }
        private bool workFinished;
        public override Job? Tick()
        {
            bool hotkeyPressed = IsClickHotkeyPressed();

            if (hotkeyPressed)
            {
                HandleHotkeyPressed();
            }
            else
            {
                HandleHotkeyReleased();
            }

            ResumeAltarScanningIfDue();

            return null;
        }

        private bool IsClickHotkeyPressed()
        {
#pragma warning disable CS0618
            return Input.GetKeyState(Settings.ClickLabelKey.Value);
#pragma warning restore CS0618
        }

        private void HandleHotkeyPressed()
        {
            if (clickLabelCoroutine?.IsDone == true)
            {
                clickLabelCoroutine = FindExistingClickLogicCoroutine();
            }

            clickLabelCoroutine?.Resume();
            workFinished = false;
        }

        private void HandleHotkeyReleased()
        {
            if (workFinished)
            {
                clickLabelCoroutine?.Pause();
            }
        }

        private Coroutine FindExistingClickLogicCoroutine()
        {
            return Core.ParallelRunner.Coroutines.FirstOrDefault(x => x.Name == "ClickIt.ClickLogic");
        }

        /// <summary>
        /// Resumes altar scanning coroutine if sufficient time has elapsed.
        /// Implements a throttling mechanism to prevent excessive scanning.
        /// </summary>
        private void ResumeAltarScanningIfDue()
        {
            if (SecondTimer.ElapsedMilliseconds > 200)
            {
                altarCoroutine?.Resume();
                SecondTimer.Restart();
            }
        }
        private IEnumerator MainClickLabelCoroutine()
        {
            while (Settings.Enable)
            {
                yield return ClickLabel();
            }
        }

        private IEnumerator MainShrineCoroutine()
        {
            while (Settings.Enable)
            {
                yield return HandleShrine();
            }
        }

        private IEnumerator MainScanForAltarsLogic()
        {
            while (Settings.Enable)
            {
                yield return ScanForAltarsLogic();
            }
        }
        /// <summary>
        /// Updates and filters the list of ground labels to find valid clickable items.
        /// Filters by visibility, position, entity type, and path patterns to identify
        /// items that should be processed for clicking.
        /// </summary>
        /// <returns>Filtered and sorted list of valid ground labels</returns>
        private List<LabelOnGround> UpdateLabelComponent()
        {
            IList<LabelOnGround>? groundLabels = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabels;

            if (groundLabels == null || groundLabels.Count == 0)
            {
                return new List<LabelOnGround>();
            }

            // Traditional loop for better performance - no LINQ overhead
            List<LabelOnGround> validLabels = new List<LabelOnGround>(Math.Min(groundLabels.Count, 1000));

            // Filter valid labels
            for (int i = 0; i < groundLabels.Count && validLabels.Count < 1000; i++)
            {
                LabelOnGround label = groundLabels[i];
                if (LabelUtils.IsValidClickableLabel(label, point => PointIsInClickableArea(point)))
                {
                    validLabels.Add(label);
                }
            }

            // Sort by distance using insertion sort for small lists, quicksort for larger ones
            LabelUtils.SortLabelsByDistance(validLabels);

            return validLabels;
        }

        /// <summary>
        /// Sorts labels by distance to player using efficient sorting algorithm
        /// </summary>
        // Label helpers and path checks were moved to ClickIt.Utils.LabelUtils to keep ClickIt.cs focused.

        private IEnumerator ClickLabel()
        {
            if (Settings.ClickShrines.Value && shrineService != null && shrineService.AreShrinesPresentInClickableArea((pos) => PointIsInClickableArea(pos)))
            {
                yield return new WaitTime(25);
                yield break;
            }

            // self adjusting delay based on average click time
            // clicks will consistently aim for 70ms intervals
            if (Timer.ElapsedMilliseconds < 70 - (clickCoroutineTimings.Count > 0 ? clickCoroutineTimings.Average() : 0) + Random.Next(0, 6) || !CanClick())
            {
                workFinished = true;
                yield break;
            }

            if (Settings.DebugMode.Value)
            {
                LogMessage($"Starting click process...");
            }

            Timer.Restart();
            clickCoroutineTimer.Restart();
            yield return clickService.ProcessRegularClick();
            clickCoroutineTimer.Stop();

            // Track timing for averaging
            EnqueueTiming(clickCoroutineTimings, clickCoroutineTimer.ElapsedMilliseconds, 10);

            workFinished = true;
        }
        private IEnumerator HandleShrine()
        {
            if (!Settings.ClickShrines.Value || shrineService == null)
            {
                yield return new WaitTime(500); // Check less frequently when disabled
                yield break;
            }

            yield return ProcessShrineClicking();
        }

        private IEnumerator ProcessShrineClicking()
        {
            if (shrineService == null || inputHandler == null)
            {
                yield break;
            }

            var nearestShrine = shrineService.GetNearestShrineInRange(Settings.ClickDistance.Value, point => PointIsInClickableArea(point));
            if (nearestShrine == null)
            {
                yield break;
            }

            if (!inputHandler.CanClick(GameController))
            {
                yield break;
            }

            LogMessage($"Clicking shrine at distance: {nearestShrine.DistancePlayer:F1}");

            if (camera == null)
            {
                yield break;
            }

            var screen = camera.WorldToScreen(nearestShrine.PosNum);
            Vector2 clickPos = new Vector2(screen.X, screen.Y);

            // Thread-safe clicking via helper that acquires the click element access lock when LockManager enabled
            ExecuteWithElementAccessLock(() => inputHandler.PerformClick(clickPos));

            yield return new WaitTime(60 + Random.Next(0, 6));
        }
        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
