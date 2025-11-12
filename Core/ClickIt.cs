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
        private Coroutine? shrineCoroutine;
        private bool isInputCurrentlyBlocked = false;
        private readonly Stopwatch lastHotkeyReleaseTimer = new Stopwatch();
        private readonly Stopwatch lastRenderTimer = new Stopwatch();
        private readonly Stopwatch lastTickTimer = new Stopwatch();
        private const int HOTKEY_RELEASE_FAILSAFE_MS = 5000;
        private bool lastHotkeyState = false;
        private Services.AreaService? areaService;
        private Services.AltarService? altarService;
        private Services.LabelFilterService? labelFilterService;
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
            labelFilterService = new Services.LabelFilterService(Settings, new Services.EssenceService(Settings));
            shrineService = new Services.ShrineService(GameController, camera);
            inputHandler = new Utils.InputHandler(Settings, SafeBlockInput, (msg, f) => LogMessage(true, msg, f));
            var weightCalculator = new Utils.WeightCalculator(Settings);
            debugRenderer = new Rendering.DebugRenderer(this, Graphics, Settings, altarService, areaService, weightCalculator);
            altarDisplayRenderer = new Rendering.AltarDisplayRenderer(Graphics, Settings, GameController, weightCalculator, altarService, LogMessage);
            // Initialize global lock manager (locks are disabled by default via settings)
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
            shrineCoroutine = new Coroutine(MainShrineCoroutine(), this, "ClickIt.ShrineLogic", true);
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
        public void LogMessage(string message, int frame = 5)
        {
            if (Settings.DebugMode && Settings.LogMessages)
            {
                base.LogMessage(message, frame);
            }
        }
        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            if (localDebug && Settings.DebugMode && Settings.LogMessages)
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
                    bool result = false;
                    result = Mouse.blockInput(false);
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
            return ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value);
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
                if (IsValidClickableLabel(label))
                {
                    validLabels.Add(label);
                }
            }

            // Sort by distance using insertion sort for small lists, quicksort for larger ones
            SortLabelsByDistance(validLabels);

            return validLabels;
        }

        /// <summary>
        /// Sorts labels by distance to player using efficient sorting algorithm
        /// </summary>
        /// <param name="labels">List of labels to sort</param>
        private void SortLabelsByDistance(List<LabelOnGround> labels)
        {
            int n = labels.Count;
            if (n <= 1) return;

            // Use insertion sort for small arrays, quicksort approach for larger
            if (n <= 50)
            {
                InsertionSortByDistance(labels, n);
            }
            else
            {
                QuickSortByDistance(labels, 0, n - 1);
            }
        }

        /// <summary>
        /// Efficient insertion sort for small lists
        /// </summary>
        private void InsertionSortByDistance(List<LabelOnGround> labels, int n)
        {
            for (int i = 1; i < n; i++)
            {
                LabelOnGround key = labels[i];
                int j = i - 1;

                // Move elements that are farther than key one position ahead
                while (j >= 0 && labels[j].ItemOnGround.DistancePlayer > key.ItemOnGround.DistancePlayer)
                {
                    labels[j + 1] = labels[j];
                    j--;
                }
                labels[j + 1] = key;
            }
        }

        /// <summary>
        /// Quick sort implementation for larger lists
        /// </summary>
        private void QuickSortByDistance(List<LabelOnGround> labels, int low, int high)
        {
            if (low < high)
            {
                int pivotIndex = PartitionByDistance(labels, low, high);
                QuickSortByDistance(labels, low, pivotIndex - 1);
                QuickSortByDistance(labels, pivotIndex + 1, high);
            }
        }

        private int PartitionByDistance(List<LabelOnGround> labels, int low, int high)
        {
            Entity pivot = labels[high].ItemOnGround;
            int i = low - 1;

            for (int j = low; j < high; j++)
            {
                if (labels[j].ItemOnGround.DistancePlayer <= pivot.DistancePlayer)
                {
                    i++;
                    SwapLabels(labels, i, j);
                }
            }
            SwapLabels(labels, i + 1, high);
            return i + 1;
        }

        private void SwapLabels(List<LabelOnGround> labels, int i, int j)
        {
            if (i != j)
            {
                LabelOnGround temp = labels[i];
                labels[i] = labels[j];
                labels[j] = temp;
            }
        }

        /// <summary>
        /// Validates if a ground label is suitable for clicking based on multiple criteria.
        /// Checks visibility, validity, position, and type/path matching.
        /// </summary>
        /// <param name="label">The ground label to validate</param>
        /// <returns>True if the label is valid for clicking, false otherwise</returns>
        private bool IsValidClickableLabel(LabelOnGround label)
        {
            // Basic null and visibility checks
            if (label == null || label.ItemOnGround == null ||
                !label.IsVisible || !IsLabelElementValid(label))
            {
                return false;
            }

            // Position validation
            if (!IsLabelInClickableArea(label))
            {
                return false;
            }

            // Entity validation
            return IsValidEntityType(label.ItemOnGround) || IsValidEntityPath(label.ItemOnGround) || HasEssenceImprisonmentText(label);
        }

        /// <summary>
        /// Checks if the label's UI element is valid and accessible.
        /// Uses null-conditional operators to prevent memory access violations.
        /// </summary>
        /// <param name="label">The ground label to check</param>
        /// <returns>True if the label element is valid</returns>
        private bool IsLabelElementValid(LabelOnGround label)
        {
            RectangleF? labelRect = label.Label?.GetClientRect();
            return labelRect is RectangleF rect &&
                   label.Label?.IsValid == true &&
                   label.Label?.IsVisible == true &&
                   PointIsInClickableArea(rect.Center);
        }

        /// <summary>
        /// Validates if a label is positioned within clickable areas of the screen.
        /// </summary>
        /// <param name="label">The ground label to check</param>
        /// <returns>True if the label is in a clickable area</returns>
        private bool IsLabelInClickableArea(LabelOnGround label)
        {
            RectangleF? labelRect = label.Label?.GetClientRect();
            return labelRect is RectangleF rect && PointIsInClickableArea(rect.Center);
        }

        /// <summary>
        /// Checks if an entity matches valid types for clicking.
        /// Includes world items, chests, area transitions, and essence-encased monsters.
        /// </summary>
        /// <param name="item">The entity to check</param>
        /// <returns>True if the entity type is valid for clicking</returns>
        private bool IsValidEntityType(Entity item)
        {
            EntityType type = item.Type;

            return type == EntityType.WorldItem ||
                   type == EntityType.AreaTransition ||
                   (type == EntityType.Chest && !item.GetComponent<Chest>().OpenOnDamage);
        }

        /// <summary>
        /// Checks if an entity matches valid path patterns for clicking.
        /// Includes various game objects like altars, harvest nodes, ores, and shrines.
        /// </summary>
        /// <param name="item">The entity to check</param>
        /// <returns>True if the entity path matches valid patterns</returns>
        private bool IsValidEntityPath(Entity item)
        {
            string path = item.Path ?? "";

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return IsPathForClickableObject(path);
        }

        /// <summary>
        /// Checks if a path string matches any of the known clickable object patterns.
        /// </summary>
        /// <param name="path">The entity path to check</param>
        /// <returns>True if the path matches a clickable object pattern</returns>
        private bool IsPathForClickableObject(string path)
        {
            return path.Contains("DelveMineral") ||
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
                   path.Contains("LegionInitiator") ||
                   path.Contains("DarkShrine");
        }

        /// <summary>
        /// Checks if a ground label has essence imprisonment text.
        /// This is the proper way to detect essence-encased monsters.
        /// </summary>
        /// <param name="label">The ground label to check</param>
        /// <returns>True if the label contains essence imprisonment text</returns>
        private bool HasEssenceImprisonmentText(LabelOnGround label)
        {
            // Check for the specific essence imprisonment text using GetElementByString
            return GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null;
        }

        private IEnumerator ClickLabel()
        {
            if (Settings.ClickShrines.Value && shrineService != null && shrineService.AreShrinesPresentInClickableArea((pos) => PointIsInClickableArea(pos)))
            {
                yield return new WaitTime(25);
                yield break;
            }

            // self adjusting delay based on average click time
            // clicks will consistently aim for 70ms intervals
            if (Timer.ElapsedMilliseconds < 70 - (clickCoroutineTimings.Count > 0 ? clickCoroutineTimings.Average() : 0) + Random.Next(0, 6) || !canClick())
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
            clickCoroutineTimings.Enqueue(clickCoroutineTimer.ElapsedMilliseconds);
            if (clickCoroutineTimings.Count > 10)
            {
                clickCoroutineTimings.Dequeue();
            }

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

            Vector2 clickPos = new Vector2(camera.WorldToScreen(nearestShrine.PosNum).X, camera.WorldToScreen(nearestShrine.PosNum).Y);

            // Thread-safe locking to prevent race conditions with other clicking
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(clickService.GetElementAccessLock()))
                {
                    inputHandler.PerformClick(clickPos);
                }
            }
            else
            {
                inputHandler.PerformClick(clickPos);
            }

            yield return new WaitTime(60 + Random.Next(0, 6));
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
