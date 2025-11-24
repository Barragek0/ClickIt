using ClickIt.Utils;
using ExileCore;
using System.Diagnostics;
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
            LockManager.Instance = null;

            // Clear ThreadLocal storage
            LabelUtils.ClearThreadLocalStorage();

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
            State.StrongboxRenderer = null;
            State.LazyModeRenderer = null;
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
            State.DebugRenderer = new Rendering.DebugRenderer(this, State.AltarService, State.AreaService, weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue);
            State.StrongboxRenderer = new Rendering.StrongboxRenderer(Settings, State.DeferredFrameQueue);
            State.LazyModeRenderer = new Rendering.LazyModeRenderer(Settings, State.DeferredTextQueue, State.InputHandler, labelFilterService);
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
            new Func<bool>(State.LabelService.GroundItemsVisible),
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

        private bool PointIsInClickableArea(SharpDX.Vector2 point, string? path = null)
        {
            State.AreaService?.UpdateScreenAreas(GameController);
            return State.AreaService?.PointIsInClickableArea(point) ?? false;
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


        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
