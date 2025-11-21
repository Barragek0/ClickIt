using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;
using System.Collections;
using System.Diagnostics;
using RectangleF = SharpDX.RectangleF;
namespace ClickIt
{
#nullable enable
    public class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        private PluginContext State { get; } = new PluginContext();
        private Action? _reportBugHandler;

        public double CurrentFPS => State.PerformanceMonitor?.CurrentFPS ?? 0;
        public Services.ShrineService? ShrineService => State.ShrineService;
        public Queue<long> RenderTimings => State.PerformanceMonitor?.RenderTimings ?? new Queue<long>();
        public IReadOnlyList<string> RecentErrors => State.ErrorHandler?.RecentErrors ?? new List<string>();

        public override void OnLoad()
        {
            // Register global error handlers
            State.ErrorHandler?.RegisterGlobalExceptionHandlers();

            CanUseMultiThreading = true;
        }
        public override void OnClose()
        {
            // Remove event handlers to prevent issues during DLL reload
            if (_reportBugHandler != null)
            {
                Settings.ReportBugButton.OnPressed -= _reportBugHandler;
            }

            // Clear static instances
            Utils.LockManager.Instance = null;

            // Clear ThreadLocal storage
            Utils.LabelUtils.ClearThreadLocalStorage();

            // Clear cached data
            State.CachedLabels = null;

            // Clear service references
            State.PerformanceMonitor = null;
            State.ErrorHandler = null;
            State.InputSafetyManager = null;
            State.AreaService = null;
            State.AltarService = null;
            State.ShrineService = null;
            State.InputHandler = null;
            State.DebugRenderer = null;
            State.DeferredTextQueue = null;
            State.DeferredFrameQueue = null;
            State.AltarDisplayRenderer = null;

            // Stop coroutines to prevent issues during DLL reload
            State.AltarCoroutine?.Done();
            State.ClickLabelCoroutine?.Done();
            State.DelveFlareCoroutine?.Done();
            State.ShrineCoroutine?.Done();
            State.InputSafetyCoroutine?.Done();

            State.InputSafetyManager?.ForceUnblockInput("Plugin closing");

            base.OnClose();
        }
        public override bool Initialise()
        {
            _reportBugHandler = () => { _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues"); };
            Settings.ReportBugButton.OnPressed += _reportBugHandler;
            State.PerformanceMonitor = new Utils.PerformanceMonitor(Settings);
            State.ErrorHandler = new Utils.ErrorHandler(Settings, LogError, LogMessage, (block) => State.InputSafetyManager?.SafeBlockInput(block), (reason) => State.InputSafetyManager?.ForceUnblockInput(reason));
            State.InputSafetyManager = new Utils.InputSafetyManager(Settings, State, (debug, msg, frame) => LogMessage(debug, msg, frame), LogError);
            State.CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, 50);
            State.AreaService = new Services.AreaService();
            State.AreaService.UpdateScreenAreas(GameController);
            State.Camera = GameController?.Game?.IngameState?.Camera;
            State.AltarService = new Services.AltarService(this, Settings, State.CachedLabels);
            var labelFilterService = new Services.LabelFilterService(Settings, new Services.EssenceService(Settings));
            State.LabelFilterService = labelFilterService;
            State.ShrineService = new Services.ShrineService(GameController!, State.Camera!);
            State.InputHandler = new Utils.InputHandler(Settings, (block) => State.InputSafetyManager?.SafeBlockInput(block), (msg, f) => LogMessage(true, msg, f));
            var weightCalculator = new Utils.WeightCalculator(Settings);
            State.DeferredTextQueue = new Utils.DeferredTextQueue();
            State.DeferredFrameQueue = new Utils.DeferredFrameQueue();
            State.DebugRenderer = new Rendering.DebugRenderer(this, Graphics, Settings, State.AltarService, State.AreaService, weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue);
            // Use no-op logger for AltarDisplayRenderer to prevent recursive logging during render loop
            State.AltarDisplayRenderer = new Rendering.AltarDisplayRenderer(Graphics, Settings, GameController ?? throw new InvalidOperationException("GameController is null @ altarDisplayRenderer initialize"), weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue, State.AltarService, (msg, frame) => { });
            LockManager.Instance = new Utils.LockManager(Settings);
            State.ClickService = new Services.ClickService(
                Settings,
                GameController,
                msg => LogMessage(msg),
                LogError,
                State.AltarService,
                weightCalculator,
                State.AltarDisplayRenderer,
                PointIsInClickableArea,
                State.InputHandler,
                labelFilterService,
                GroundItemsVisible,
                State.CachedLabels,
                State.PerformanceMonitor);
            State.PerformanceMonitor.Start();

            var coroutineManager = new Utils.CoroutineManager(
                State,
                Settings,
                GameController,
                (msg, frame) => LogMessage(msg, frame),
                (reason) => State.InputSafetyManager?.ForceUnblockInput(reason),
                point => PointIsInClickableArea(point));
            coroutineManager.StartCoroutines(this);

            Settings.EnsureAllModsHaveWeights();

            // Start monitoring timers
            State.LastRenderTimer.Start();
            State.LastTickTimer.Start();
            State.Timer.Start();
            State.SecondTimer.Start();
            State.ShrineTimer.Start();

            return true;
        }
        private bool PointIsInClickableArea(Vector2 point, string? path = null)
        {
            State.AreaService?.UpdateScreenAreas(GameController);
            return State.AreaService?.PointIsInClickableArea(point) ?? false;
        }
        public override void Render()
        {
            if (State.PerformanceMonitor == null) return; // Not initialized yet

            // Set flag to prevent logging during render loop
            State.IsRendering = true;
            try
            {
                RenderInternal();
            }
            finally
            {
                State.IsRendering = false;
            }
        }

        private void RenderInternal()
        {
            bool debugMode = Settings.DebugMode;
            bool renderDebug = Settings.RenderDebug;
            bool hasDebugRendering = debugMode && renderDebug;

            int altarCount = State.AltarService?.GetAltarComponents()?.Count ?? 0;
            bool hasAltars = altarCount > 0;

            bool hasLazyModeIndicator = Settings.LazyMode.Value;

            if (!hasDebugRendering && !hasAltars && !hasLazyModeIndicator)
            {
                return; // Skip all timer operations for no-op renders
            }

            // Start timing only when actually rendering
            State.PerformanceMonitor.StartRenderTiming();
            State.PerformanceMonitor.UpdateFPS();

            // Render lazy mode indicator if enabled
            if (Settings.LazyMode.Value)
            {
                RenderLazyModeIndicator();
            }

            if (hasDebugRendering)
            {
                State.DebugRenderer?.RenderDebugFrames(Settings);
                State.DebugRenderer?.RenderDetailedDebugInfo(Settings, State.PerformanceMonitor);
            }

            if (hasAltars)
            {
                RenderAltarComponents();
            }

            State.PerformanceMonitor.StopRenderTiming();

            // Flush deferred text rendering to prevent freezes
            // Use no-op logger to prevent recursive logging during render loop
            State.DeferredTextQueue?.Flush(Graphics, (msg, frame) => { });
            State.DeferredFrameQueue?.Flush(Graphics, (msg, frame) => { });
        }
        private void RenderAltarComponents()
        {
            State.AltarDisplayRenderer?.RenderAltarComponents();
        }
        private void RenderLazyModeIndicator()
        {
            if (State.DeferredTextQueue == null) return;

            // Get screen center position
            var windowRect = GameController.Window.GetWindowRectangleTimeCache;
            float centerX = windowRect.Width / 2f;
            float topY = 60f; // Small margin from top

            // Check if lazy mode is restricted
            var allLabels = State.CachedLabels?.Value ?? new List<LabelOnGround>();
            bool hasRestrictedItems = State.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(allLabels) ?? false;

            if (hasRestrictedItems)
            {
                string lazyModeText = "Lazy Mode";
                State.DeferredTextQueue.Enqueue(lazyModeText, new Vector2(centerX, topY), SharpDX.Color.Red, 36, FontAlign.Center);

                float lineHeight = 36 * 1.2f;
                float secondLineY = topY + lineHeight;
                string firstExplanation = "Strongbox, Chest or Tree detected.";
                State.DeferredTextQueue.Enqueue(firstExplanation, new Vector2(centerX, secondLineY), SharpDX.Color.Red, 24, FontAlign.Center);


                float thirdLineY = secondLineY + lineHeight;
                string hotkeyName = Settings.ClickLabelKey.Value.ToString();
                string secondExplanation = $"Hold {hotkeyName} to click them.";
                State.DeferredTextQueue.Enqueue(secondExplanation, new Vector2(centerX, thirdLineY), SharpDX.Color.Red, 24, FontAlign.Center);
            }
            else
            {
                // Green text for "Lazy Mode" centered at size 36
                string text = "Lazy Mode";
                State.DeferredTextQueue.Enqueue(text, new Vector2(centerX, topY), SharpDX.Color.LawnGreen, 36, FontAlign.Center);
            }
        }
        public void LogMessage(string message, int frame = 5)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogMessage(message, frame);
        }

        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            LogMessage(localDebug, message, frame);
        }
        public void LogError(string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogError(message, frame);
        }




        private bool GroundItemsVisible()
        {
            // Avoid triggering cache refresh if not necessary
            var cachedValue = State.CachedLabels?.Value;
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
            bool actual = Input.GetKeyState(Settings.ClickLabelKey.Value);
            if (Settings?.LazyMode != null && Settings.LazyMode.Value)
            {
                // In lazy mode, invert hotkey behaviour: released -> active, held -> inactive
                return !actual;
            }
            return actual;
        }

        private void HandleHotkeyPressed()
        {
            if (State.ClickLabelCoroutine?.IsDone == true)
            {
                State.ClickLabelCoroutine = FindExistingClickLogicCoroutine();
            }

            State.ClickLabelCoroutine?.Resume();
            State.WorkFinished = false;
        }

        private void HandleHotkeyReleased()
        {
            if (State.WorkFinished)
            {
                State.ClickLabelCoroutine?.Pause();
            }
            State.PerformanceMonitor?.ResetClickCount();
        }

        private Coroutine? FindExistingClickLogicCoroutine()
        {
            return Core.ParallelRunner.Coroutines.FirstOrDefault(x => x.Name == "ClickIt.ClickLogic");
        }

        /// <summary>
        /// Resumes altar scanning coroutine if sufficient time has elapsed.
        /// Implements a throttling mechanism to prevent excessive scanning.
        /// </summary>
        private void ResumeAltarScanningIfDue()
        {
            if (State.SecondTimer.ElapsedMilliseconds > 200)
            {
                State.AltarCoroutine?.Resume();
                State.SecondTimer.Restart();
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
            IList<LabelOnGround>? groundLabels = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible;

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

        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
