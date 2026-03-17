using ClickIt.Utils;
using ExileCore;
using System.Diagnostics;
using System.Threading;

namespace ClickIt
{
    public partial class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        public PluginContext State { get; } = new PluginContext();

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
        }

        public override void OnClose()
        {
            ClickItSettings runtimeSettings = Settings ?? EffectiveSettings;
            State.IsShuttingDown = true;

            // Remove event handlers to prevent issues during DLL reload
            runtimeSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
            runtimeSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            if (!ReferenceEquals(runtimeSettings, EffectiveSettings))
            {
                EffectiveSettings.ReportBugButton.OnPressed -= ReportBugButtonPressed;
                EffectiveSettings.CopyAdditionalDebugInfoButton.OnPressed -= CopyAdditionalDebugInfoButtonPressed;
            }

            if (State.AlertService != null)
            {
                runtimeSettings.OpenConfigDirectory.OnPressed -= State.AlertService.OpenConfigDirectory;
                runtimeSettings.ReloadAlertSound.OnPressed -= State.AlertService.ReloadAlertSound;

                if (!ReferenceEquals(runtimeSettings, EffectiveSettings))
                {
                    EffectiveSettings.OpenConfigDirectory.OnPressed -= State.AlertService.OpenConfigDirectory;
                    EffectiveSettings.ReloadAlertSound.OnPressed -= State.AlertService.ReloadAlertSound;
                }
            }

            State.ErrorHandler?.UnregisterGlobalExceptionHandlers();

            State.AltarCoroutine?.Done();
            State.ClickLabelCoroutine?.Done();
            State.DelveFlareCoroutine?.Done();
            StopAllClickItCoroutines();
            WaitForCoroutineShutdown(State.AltarCoroutine);
            WaitForCoroutineShutdown(State.ClickLabelCoroutine);
            WaitForCoroutineShutdown(State.DelveFlareCoroutine);
            WaitForAllClickItCoroutinesShutdown();

            State.AltarCoroutine = null;
            State.ClickLabelCoroutine = null;
            State.DelveFlareCoroutine = null;

            LockManager.Instance = null;

            LabelUtils.ClearThreadLocalStorage();
            Services.ShrineService.ClearThreadLocalStorageForCurrentThread();
            Services.ClickService.ClearThreadLocalStorageForCurrentThread();
            Services.LabelFilterService.ClearInventoryProbeCacheForShutdown();
            State.AltarService?.ClearRuntimeCaches();

            State.CachedLabels = null;

            State.PerformanceMonitor?.ShutdownForHotReload();

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
            State.ClickHotkeyToggleRenderer = null;
            State.InventoryFullWarningRenderer = null;
            State.PathfindingRenderer = null;
            State.DeferredTextQueue = null;
            State.DeferredFrameQueue = null;
            State.AltarDisplayRenderer = null;
            State.PathfindingService = null;
            State.AlertService = null;
            State.LabelService = null;
            State.LabelFilterService = null;
            State.ClickService = null;
            State.Camera = null;

            State.LastRenderTimer.Stop();
            State.LastTickTimer.Stop();
            State.Timer.Stop();
            State.SecondTimer.Stop();

            // Best-effort finalizer drain to reduce transient assembly/file lock windows during host hot-reload.
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(50);
            }
            catch
            {
                // Best effort only.
            }

            // In some test scenarios the Settings property isn't populated on the base class even though tests inject settings via the test seam.
            // Avoid invoking base.OnClose when the real Settings property is null to prevent ExileCore.BaseSettingsPlugin from attempting to save a null settings instance.
            if (Settings != null)
            {
                base.OnClose();
            }
        }

        public override bool Initialise()
        {
            State.IsShuttingDown = false;
            Settings.ReportBugButton.OnPressed += ReportBugButtonPressed;
            Settings.CopyAdditionalDebugInfoButton.OnPressed += CopyAdditionalDebugInfoButtonPressed;
            State.PerformanceMonitor = new PerformanceMonitor(Settings);
            State.ErrorHandler = new ErrorHandler(Settings, LogError, LogMessage);
            State.ErrorHandler.RegisterGlobalExceptionHandlers();
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
            State.PathfindingService = new Services.PathfindingService(Settings, State.ErrorHandler);
            var weightCalculator = new WeightCalculator(Settings);
            State.DeferredTextQueue = new DeferredTextQueue();
            State.DeferredFrameQueue = new DeferredFrameQueue();
            State.DebugRenderer = new Rendering.DebugRenderer(this, State.AltarService, State.AreaService, weightCalculator, State.DeferredTextQueue, State.DeferredFrameQueue);
            State.StrongboxRenderer = new Rendering.StrongboxRenderer(Settings, State.DeferredFrameQueue);
            State.LazyModeRenderer = new Rendering.LazyModeRenderer(Settings, State.DeferredTextQueue, State.InputHandler, labelFilterService);
            State.ClickHotkeyToggleRenderer = new Rendering.ClickHotkeyToggleRenderer(Settings, State.DeferredTextQueue, State.InputHandler);
            State.InventoryFullWarningRenderer = new Rendering.InventoryFullWarningRenderer(Settings, State.DeferredTextQueue, State.AreaService);
            State.PathfindingRenderer = new Rendering.PathfindingRenderer(State.PathfindingService);
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
                State.PathfindingService,
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

        private static void WaitForCoroutineShutdown(ExileCore.Shared.Coroutine? coroutine, int timeoutMs = 750)
        {
            if (coroutine == null)
                return;

            var sw = Stopwatch.StartNew();
            while (!coroutine.IsDone && sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(10);
            }
        }

        private static void StopAllClickItCoroutines()
        {
            try
            {
                var coroutines = Core.ParallelRunner.Coroutines
                    .Where(c => c != null && c.Name != null && c.Name.StartsWith("ClickIt.", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var coroutine in coroutines)
                {
                    coroutine.Done();
                }
            }
            catch
            {
                // Best effort cleanup during shutdown.
            }
        }

        private static void WaitForAllClickItCoroutinesShutdown(int timeoutMs = 2000)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    bool anyActive = Core.ParallelRunner.Coroutines
                        .Any(c => c != null
                            && c.Name != null
                            && c.Name.StartsWith("ClickIt.", StringComparison.OrdinalIgnoreCase)
                            && !c.IsDone);

                    if (!anyActive)
                        break;

                    Thread.Sleep(10);
                }
            }
            catch
            {
                // Best effort cleanup during shutdown.
            }
        }

        private void ReportBugButtonPressed()
        {
            _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues");
        }

        private void CopyAdditionalDebugInfoButtonPressed()
        {
            _copyAdditionalDebugInfoRequested = true;
        }

        public override void Render()
        {
            if (State.IsShuttingDown || State.PerformanceMonitor == null) return;

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
