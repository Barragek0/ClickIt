using ClickIt.Utils;
using ExileCore;
using System.Diagnostics;

namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();

        public override void OnLoad()
        {
            // Register global error handlers
            State.ErrorHandler?.RegisterGlobalExceptionHandlers();

            CanUseMultiThreading = true;
        }

        public override void OnClose()
        {
            // Remove event handlers to prevent issues during DLL reload
            // Unsubscribe the report-bug event handler (use EffectiveSettings so tests that inject settings succeed)
            EffectiveSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
            // Unsubscribe alert sound handlers
            if (State.AlertService != null)
            {
                EffectiveSettings.OpenConfigDirectory.OnPressed -= State.AlertService.OpenConfigDirectory;
                EffectiveSettings.ReloadAlertSound.OnPressed -= State.AlertService.ReloadAlertSound;
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
            State.UltimatumRenderer = null;
            State.LazyModeRenderer = null;
            State.DeferredTextQueue = null;
            State.DeferredFrameQueue = null;
            State.AltarDisplayRenderer = null;
            State.AlertService = null;

            // Stop coroutines to prevent issues during DLL reload
            State.AltarCoroutine?.Done();
            State.ClickLabelCoroutine?.Done();
            State.DelveFlareCoroutine?.Done();

            // Base OnClose will attempt to save plugin settings which relies on the actual base-class storage for Settings.
            // In some test scenarios the Settings property isn't populated on the base class even though tests inject settings via the test seam.
            // Avoid invoking base.OnClose when the real Settings property is null to prevent ExileCore.BaseSettingsPlugin from attempting to save a null settings instance.
            if (Settings != null)
            {
                base.OnClose();
            }
        }

        public override bool Initialise()
        {
            Settings.ReportBugButton.OnPressed += ReportBugButtonPressed;
            State.PerformanceMonitor = new PerformanceMonitor(Settings);
            State.ErrorHandler = new ErrorHandler(Settings, LogError, LogMessage);
            State.AreaService = new Services.AreaService();
            State.AreaService.UpdateScreenAreas(GameController);
            State.LabelService = new Services.LabelService(
                GameController!,
                point => State.AreaService?.PointIsInClickableArea(GameController, point) ?? false);
            State.CachedLabels = State.LabelService.CachedLabels;
            State.Camera = GameController?.Game?.IngameState?.Camera;
            State.AltarService = new Services.AltarService(this, Settings, State.CachedLabels);
            var labelFilterService = new Services.LabelFilterService(Settings, new Services.EssenceService(Settings), State.ErrorHandler, GameController);
            State.LabelFilterService = labelFilterService;
            State.ShrineService = new Services.ShrineService(GameController!, State.Camera!);
            State.InputHandler = new InputHandler(Settings, State.PerformanceMonitor, State.ErrorHandler);
            var weightCalculator = new WeightCalculator(Settings);
            State.DeferredTextQueue = new DeferredTextQueue();
            State.DeferredFrameQueue = new DeferredFrameQueue();
            State.DebugRenderer = new Rendering.DebugRenderer(this, State.AltarService, State.AreaService, weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue);
            State.StrongboxRenderer = new Rendering.StrongboxRenderer(Settings, State.DeferredFrameQueue);
            State.LazyModeRenderer = new Rendering.LazyModeRenderer(Settings, State.DeferredTextQueue, State.InputHandler, labelFilterService);
            State.AltarDisplayRenderer = new Rendering.AltarDisplayRenderer(Graphics, Settings, GameController ?? throw new InvalidOperationException("GameController is null @ altarDisplayRenderer initialize"), weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue, State.AltarService, LogMessage);
            LockManager.Instance = new LockManager(Settings);
            State.ClickService = new Services.ClickService(
                Settings,
                GameController,
                State.ErrorHandler,
                State.AltarService,
                weightCalculator,
                State.AltarDisplayRenderer,
                (point, path) => State.AreaService?.PointIsInClickableArea(GameController, point) ?? false,
                State.InputHandler,
                labelFilterService,
                State.ShrineService,
                new Func<bool>(State.LabelService.GroundItemsVisible),
                State.CachedLabels,
                State.PerformanceMonitor);
            State.UltimatumRenderer = new Rendering.UltimatumRenderer(Settings, State.ClickService, State.DeferredFrameQueue);
            var alertService = GetOrCreateAlertService();
            State.PerformanceMonitor.Start();

            var coroutineManager = new CoroutineManager(
                State,
                Settings,
                GameController,
                State.ErrorHandler);
            coroutineManager.StartCoroutines(this);

            Settings.EnsureAllModsHaveWeights();

            Settings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            Settings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;
            alertService.ReloadAlertSound();

            State.LastRenderTimer.Start();
            State.LastTickTimer.Start();
            State.Timer.Start();
            State.SecondTimer.Start();

            return true;
        }

        private void ReportBugButtonPressed()
        {
            _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues");
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
            // Log when not in local debug mode, or when in local debug mode and DebugMode is enabled.
            if (!localDebug || Settings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }
        public void LogError(string message, int frame = 0)
        {
            // Skip logging during render loop to prevent crashes
            if (State.IsRendering) return;
            base.LogError(message, frame);
        }

    }
}
