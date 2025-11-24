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
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        private PluginContext State { get; } = new PluginContext();
        private Action? _reportBugHandler;

        public double CurrentFPS => State.PerformanceMonitor?.CurrentFPS ?? 0;
        public Services.ShrineService? ShrineService => State.ShrineService;
        public Services.LabelFilterService? LabelFilterService => State.LabelFilterService;
        public PerformanceMonitor? PerformanceMonitor => State.PerformanceMonitor;
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



            base.OnClose();
        }
        public override bool Initialise()
        {
            _reportBugHandler = () => { _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues"); };
            Settings.ReportBugButton.OnPressed += _reportBugHandler;
            State.PerformanceMonitor = new PerformanceMonitor(Settings);
            State.ErrorHandler = new ErrorHandler(Settings, LogError, LogMessage);
            // Use LabelService to own label discovery + caching
            State.LabelService = new Services.LabelService(GameController!, point => PointIsInClickableArea(point));
            State.CachedLabels = State.LabelService.CachedLabels;
            State.AreaService = new Services.AreaService();
            State.AreaService.UpdateScreenAreas(GameController);
            State.Camera = GameController?.Game?.IngameState?.Camera;
            State.AltarService = new Services.AltarService(this, Settings, State.CachedLabels);
            var labelFilterService = new Services.LabelFilterService(Settings, new Services.EssenceService(Settings), State.ErrorHandler);
            State.LabelFilterService = labelFilterService;
            State.ShrineService = new Services.ShrineService(GameController!, State.Camera!);
            State.InputHandler = new InputHandler(Settings, State.PerformanceMonitor, State.ErrorHandler);
            var weightCalculator = new WeightCalculator(Settings);
            State.DeferredTextQueue = new DeferredTextQueue();
            State.DeferredFrameQueue = new DeferredFrameQueue();
            State.DebugRenderer = new Rendering.DebugRenderer(this, Graphics, Settings, State.AltarService, State.AreaService, weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue);
            // Use no-op logger for AltarDisplayRenderer to prevent recursive logging during render loop
            State.AltarDisplayRenderer = new Rendering.AltarDisplayRenderer(Graphics, Settings, GameController ?? throw new InvalidOperationException("GameController is null @ altarDisplayRenderer initialize"), weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue, State.AltarService, (msg, frame) => { });
            LockManager.Instance = new LockManager(Settings);
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
            // LabelService is created during Initialise and owns label discovery; pass its delegate directly
            new System.Func<bool>(State.LabelService.GroundItemsVisible),
            State.CachedLabels,
            State.PerformanceMonitor);
            State.PerformanceMonitor.Start();

            var coroutineManager = new CoroutineManager(
                State,
                Settings,
                GameController,
                State.ErrorHandler,
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
        // Rendering and clickable area helpers moved to Core/ClickIt.Render.cs
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

        // Render logic extracted to Core/ClickIt.Render.cs
        // Altar rendering moved to Core/ClickIt.Render.cs
        /// <summary>
        /// Check if a ritual is currently active by looking for RitualBlocker entities
        /// </summary>
        // Ritual detection moved to Core/ClickIt.Render.cs

        // Lazy-mode UI rendering moved to Core/ClickIt.Render.cs

        // Lazy mode text rendering moved to Core/ClickIt.Render.cs

        // CanClick diagnostics moved to Core/ClickIt.Render.cs
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

        // Ground item helpers moved to Core/ClickIt.Labels.cs

        // Element helpers moved to Core/ClickIt.Labels.cs
        // Tick/hotkey handling moved to Core/ClickIt.Input.cs

        // Lazy-mode hotkey helper moved to Core/ClickIt.Input.cs

        // HandleHotkeyPressed moved to Core/ClickIt.Input.cs

        // HandleHotkeyReleased moved to Core/ClickIt.Input.cs

        // Coroutine helper moved to Core/ClickIt.Input.cs

        /// <summary>
        /// Resumes altar scanning coroutine if sufficient time has elapsed.
        /// Implements a throttling mechanism to prevent excessive scanning.
        /// </summary>
        // ResumeAltarScanningIfDue moved to Core/ClickIt.Input.cs
        /// <summary>
        /// Updates and filters the list of ground labels to find valid clickable items.
        /// Filters by visibility, position, entity type, and path patterns to identify
        /// items that should be processed for clicking.
        /// </summary>
        /// <returns>Filtered and sorted list of valid ground labels</returns>
        // Label discovery and sorting moved to Core/ClickIt.Labels.cs

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
