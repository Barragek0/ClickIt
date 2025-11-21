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
            State.InputSafetyManager = new Utils.InputSafetyManager(Settings, State, State.ErrorHandler);
            State.CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, 50);
            State.AreaService = new Services.AreaService();
            State.AreaService.UpdateScreenAreas(GameController);
            State.Camera = GameController?.Game?.IngameState?.Camera;
            State.AltarService = new Services.AltarService(this, Settings, State.CachedLabels);
            var labelFilterService = new Services.LabelFilterService(Settings, new Services.EssenceService(Settings));
            State.LabelFilterService = labelFilterService;
            State.ShrineService = new Services.ShrineService(GameController!, State.Camera!);
            State.InputHandler = new Utils.InputHandler(Settings, (block) => State.InputSafetyManager?.SafeBlockInput(block), State.ErrorHandler);
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
                State.ErrorHandler,
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
                State.ErrorHandler,
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
        /// <summary>
        /// Check if a ritual is currently active by looking for RitualBlocker entities
        /// </summary>
        private bool IsRitualActive()
        {
            if (GameController?.EntityListWrapper?.OnlyValidEntities == null)
                return false;

            foreach (var entity in GameController.EntityListWrapper.OnlyValidEntities)
            {
                if (entity?.Path?.Contains("RitualBlocker") == true)
                {
                    return true;
                }
            }
            return false;
        }

        private void RenderLazyModeIndicator()
        {
            if (State.DeferredTextQueue == null) return;
            var windowRect = GameController.Window.GetWindowRectangleTimeCache;
            float centerX = windowRect.Width / 2f;
            float topY = 60f; // Small margin from top

            // Check if lazy mode is restricted
            var allLabels = State.CachedLabels?.Value ?? new List<LabelOnGround>();
            bool hasRestrictedItems = State.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(allLabels) ?? false;

            // Check if a ritual is active
            bool isRitualActive = IsRitualActive();

            // Check if primary mouse button is held (prevents lazy clicking)
            bool leftButtonHeld = Input.GetKeyState(Keys.LButton);
            bool rightButtonHeld = Input.GetKeyState(Keys.RButton);
            bool skillButtonHeld = Settings.LeftHanded.Value ? leftButtonHeld : rightButtonHeld;

            // Check if hotkey is currently held
            bool hotkeyHeld = Input.GetKeyState(Settings.ClickLabelKey.Value);

            // Check if lazy mode disable key is currently held
            bool lazyModeDisableHeld = Input.GetKeyState(Settings.LazyModeDisableKey.Value);

            // Determine display state and messages
            SharpDX.Color textColor;
            string line1 = "", line2 = "", line3 = "";

            if (isRitualActive)
            {
                textColor = SharpDX.Color.Red;
                line1 = "Ritual in progress.";
                line2 = "Clicking disabled.";
            }
            else if (hasRestrictedItems)
            {
                if (hotkeyHeld)
                {
                    textColor = SharpDX.Color.LawnGreen;
                    line1 = "Blocking overridden by hotkey.";
                    line2 = "Clicking restricted items.";
                }
                else
                {
                    textColor = SharpDX.Color.Red;
                    line1 = "Strongbox, Chest or Tree detected.";
                    string hotkeyName = Settings.ClickLabelKey.Value.ToString();
                    line2 = $"Hold {hotkeyName} to click them.";
                }
            }
            else if (!hasRestrictedItems && lazyModeDisableHeld)
            {
                textColor = SharpDX.Color.Red;
                line1 = "Lazy mode disabled by hotkey.";
                line2 = "Release to resume lazy clicking.";
            }
            else if (skillButtonHeld)
            {
                textColor = SharpDX.Color.Red;
                line1 = Settings.LeftHanded.Value ? "Left mouse button held." : "Right mouse button held.";
                line2 = "Release to resume lazy clicking.";
            }
            else
            {
                // Check if CanClick would actually allow clicking
                bool canActuallyClick = State.InputHandler?.CanClick(GameController, false, isRitualActive) ?? false;

                if (!canActuallyClick)
                {
                    textColor = SharpDX.Color.Red;
                    line1 = GetCanClickFailureReason();
                }
                else
                {
                    textColor = SharpDX.Color.LawnGreen;
                    // No additional lines for green state
                }
            }

            // Render the lazy mode indicator
            RenderLazyModeText(centerX, topY, textColor, line1, line2, line3);
        }

        private void RenderLazyModeText(float centerX, float topY, SharpDX.Color color, string line1, string line2, string line3)
        {
            const string LAZY_MODE_TEXT = "Lazy Mode";
            State.DeferredTextQueue?.Enqueue(LAZY_MODE_TEXT, new Vector2(centerX, topY), color, 36, FontAlign.Center);

            if (string.IsNullOrEmpty(line1)) return;

            float lineHeight = 36 * 1.2f;
            float secondLineY = topY + lineHeight;
            State.DeferredTextQueue?.Enqueue(line1, new Vector2(centerX, secondLineY), color, 24, FontAlign.Center);

            if (string.IsNullOrEmpty(line2)) return;

            float thirdLineY = secondLineY + lineHeight;
            State.DeferredTextQueue?.Enqueue(line2, new Vector2(centerX, thirdLineY), color, 24, FontAlign.Center);

            if (string.IsNullOrEmpty(line3)) return;

            float fourthLineY = thirdLineY + lineHeight;
            State.DeferredTextQueue?.Enqueue(line3, new Vector2(centerX, fourthLineY), color, 24, FontAlign.Center);
        }

        private string GetCanClickFailureReason()
        {
            if (GameController?.Window?.IsForeground() == false)
                return "PoE not in focus.";

            if (Settings.BlockOnOpenLeftRightPanel.Value &&
                (GameController?.IngameState?.IngameUi?.OpenLeftPanel?.Address != 0 ||
                 GameController?.IngameState?.IngameUi?.OpenRightPanel?.Address != 0))
                return "Panel is open.";

            if (GameController?.Area?.CurrentArea?.IsTown == true ||
                GameController?.Area?.CurrentArea?.IsHideout == true)
                return "In town/hideout.";

            if (GameController?.IngameState?.IngameUi?.ChatTitlePanel?.IsVisible == true)
                return "Chat is open.";

            if (GameController?.IngameState?.IngameUi?.AtlasPanel?.IsVisible == true)
                return "Atlas panel is open.";

            if (GameController?.IngameState?.IngameUi?.AtlasTreePanel?.IsVisible == true)
                return "Atlas tree panel is open.";

            if (GameController?.IngameState?.IngameUi?.TreePanel?.IsVisible == true)
                return "Passive tree panel is open.";

            if (GameController?.IngameState?.IngameUi?.UltimatumPanel?.IsVisible == true)
                return "Ultimatum panel is open.";

            if (GameController?.IngameState?.IngameUi?.BetrayalWindow?.IsVisible == true)
                return "Betrayal window is open.";

            if (GameController?.IngameState?.IngameUi?.SyndicatePanel?.IsVisible == true)
                return "Syndicate panel is open.";

            if (GameController?.IngameState?.IngameUi?.SyndicateTree?.IsVisible == true)
                return "Syndicate tree panel is open.";

            if (GameController?.IngameState?.IngameUi?.IncursionWindow?.IsVisible == true)
                return "Incursion window is open.";

            if (GameController?.IngameState?.IngameUi?.RitualWindow?.IsVisible == true)
                return "Ritual window is open.";

            if (GameController?.IngameState?.IngameUi?.SanctumFloorWindow?.IsVisible == true)
                return "Sanctum floor window is open.";

            if (GameController?.IngameState?.IngameUi?.SanctumRewardWindow?.IsVisible == true)
                return "Sanctum reward window is open.";

            if (GameController?.IngameState?.IngameUi?.MicrotransactionShopWindow?.IsVisible == true)
                return "Microtransaction shop window is open.";

            if (GameController?.IngameState?.IngameUi?.ResurrectPanel?.IsVisible == true)
                return "Resurrect panel is open.";

            if (GameController?.IngameState?.IngameUi?.NpcDialog?.IsVisible == true)
                return "NPC dialog is open.";

            if (GameController?.IngameState?.IngameUi?.KalandraTabletWindow?.IsVisible == true)
                return "Kalandra tablet window is open.";

            if (GameController?.Game?.IsEscapeState == true)
                return "In escape state.";

            return "Clicking disabled.";
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
                // Check if restricted items are present
                bool hasRestrictedItems = State.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(State.CachedLabels?.Value ?? new List<LabelOnGround>()) ?? false;

                if (hasRestrictedItems)
                {
                    // When restricted items are present in lazy mode, only activate when hotkey is held (normal behavior)
                    return actual;
                }
                else
                {
                    // No restricted items, check if lazy mode disable key is held
                    bool lazyModeDisableHeld = Input.GetKeyState(Settings.LazyModeDisableKey.Value);
                    if (lazyModeDisableHeld)
                    {
                        // Lazy mode disable key is held, don't allow clicking
                        return false;
                    }

                    // No restricted items and disable key not held, invert hotkey: released -> active
                    bool inverted = !actual;
                    if (inverted)
                    {
                        // Prevent lazy mode clicking if the primary mouse button is held
                        // Left-handed users use right click as primary, right-handed users use left click
                        bool primaryButtonHeld = Settings.LeftHanded.Value ?
                            Input.GetKeyState(Keys.LButton) : Input.GetKeyState(Keys.RButton);
                        if (primaryButtonHeld)
                        {
                            return false; // Don't click while primary mouse button is held
                        }
                    }
                    return inverted;
                }
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
