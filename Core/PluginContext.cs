using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using System.Diagnostics;
using ClickIt.Services;
using ClickIt.Utils;

namespace ClickIt
{
    public class PluginContext
    {
        private readonly DisposableServiceRegistry _serviceRegistry = new();

        public Utils.PerformanceMonitor? PerformanceMonitor { get; set; }
        public Utils.ErrorHandler? ErrorHandler { get; set; }
        public Random Random { get; } = new Random();
        public TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }
        public Coroutine? AltarCoroutine { get; set; }
        public Coroutine? ClickLabelCoroutine { get; set; }
        public Coroutine? ManualUiHoverCoroutine { get; set; }
        public Coroutine? DelveFlareCoroutine { get; set; }
        public Coroutine? DeepMemoryDumpCoroutine { get; set; }
        public Stopwatch LastRenderTimer { get; } = new Stopwatch();
        public Stopwatch LastTickTimer { get; } = new Stopwatch();
        public Stopwatch Timer { get; } = new Stopwatch();
        public Stopwatch SecondTimer { get; } = new Stopwatch();
        public bool LastHotkeyState { get; set; } = false;
        public Services.AreaService? AreaService { get; set; }
        public Services.AltarService? AltarService { get; set; }
        public Services.ShrineService? ShrineService { get; set; }
        public Utils.InputHandler? InputHandler { get; set; }
        public Rendering.DebugRenderer? DebugRenderer { get; set; }
        public Rendering.StrongboxRenderer? StrongboxRenderer { get; set; }
        public Rendering.UltimatumRenderer? UltimatumRenderer { get; set; }
        public Rendering.LazyModeRenderer? LazyModeRenderer { get; set; }
        public Rendering.ClickHotkeyToggleRenderer? ClickHotkeyToggleRenderer { get; set; }
        public Rendering.InventoryFullWarningRenderer? InventoryFullWarningRenderer { get; set; }
        public Rendering.PathfindingRenderer? PathfindingRenderer { get; set; }
        public Rendering.AltarDisplayRenderer? AltarDisplayRenderer { get; set; }
        public Utils.DeferredTextQueue? DeferredTextQueue { get; set; }
        public Utils.DeferredFrameQueue? DeferredFrameQueue { get; set; }
        public Services.LabelFilterService? LabelFilterService { get; set; }
        public Services.LabelService? LabelService { get; set; }
        public Services.ClickService? ClickService { get; set; }
        public Services.PathfindingService? PathfindingService { get; set; }
        public Services.AlertService? AlertService { get; set; }
        public Camera? Camera { get; set; }
        public bool WorkFinished { get; set; } = false;
        public bool IsShuttingDown { get; set; } = false;

        /// <summary>
        /// Indicates whether we're currently in the Render() method.
        /// Used to prevent logging that could cause recursive rendering issues.
        /// </summary>
        public bool IsRendering { get; set; } = false;

        public double CurrentFPS => PerformanceMonitor?.CurrentFPS ?? 0;

        public Queue<long> RenderTimings => PerformanceMonitor?.GetRenderTimingsSnapshot() ?? new Queue<long>();

        public IReadOnlyList<string> RecentErrors => ErrorHandler?.RecentErrors ?? [];

        public void InitializeCompositionRoot(ClickIt owner, ClickItSettings settings)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            _serviceRegistry.Reset();
            IsShuttingDown = false;

            var gameController = owner.GameController;
            if (gameController == null)
                throw new InvalidOperationException("GameController is null during plugin initialization.");

            var performanceMonitor = new PerformanceMonitor(settings);
            var errorHandler = new ErrorHandler(settings, owner.LogError, owner.LogMessage);
            var areaService = new Services.AreaService();
            areaService.UpdateScreenAreas(gameController);

            var labelService = new Services.LabelService(
                gameController,
                point => AreaService?.PointIsInClickableArea(gameController, point) ?? false);
            var cachedLabels = labelService.CachedLabels;
            var camera = gameController.Game?.IngameState?.Camera;
            var altarService = new Services.AltarService(owner, settings, cachedLabels);
            var labelFilterService = new Services.LabelFilterService(settings, new Services.EssenceService(settings), errorHandler, gameController);
            var shrineService = new Services.ShrineService(gameController, camera ?? throw new InvalidOperationException("Camera is null during plugin initialization."));
            var inputHandler = new InputHandler(settings, performanceMonitor, errorHandler);
            var pathfindingService = new Services.PathfindingService(settings, errorHandler);
            var weightCalculator = new WeightCalculator(settings);
            var deferredTextQueue = new DeferredTextQueue();
            var deferredFrameQueue = new DeferredFrameQueue();
            var debugRenderer = new Rendering.DebugRenderer(owner, altarService, areaService, weightCalculator, deferredTextQueue, deferredFrameQueue);
            var strongboxRenderer = new Rendering.StrongboxRenderer(settings, deferredFrameQueue);
            var lazyModeRenderer = new Rendering.LazyModeRenderer(settings, deferredTextQueue, inputHandler, labelFilterService);
            var clickHotkeyToggleRenderer = new Rendering.ClickHotkeyToggleRenderer(settings, deferredTextQueue, inputHandler);
            var inventoryFullWarningRenderer = new Rendering.InventoryFullWarningRenderer(
                deferredTextQueue,
                areaService,
                owner.TryAutoCopyInventoryWarningDebugSnapshotForLifecycle);
            var pathfindingRenderer = new Rendering.PathfindingRenderer(pathfindingService);
            var altarDisplayRenderer = new Rendering.AltarDisplayRenderer(
                owner.Graphics,
                settings,
                gameController,
                weightCalculator,
                deferredTextQueue,
                deferredFrameQueue,
                altarService,
                owner.LogMessage);

            LockManager.Instance = new LockManager(settings);

            var clickService = new Services.ClickService(
                settings,
                gameController,
                errorHandler,
                altarService,
                weightCalculator,
                altarDisplayRenderer,
                (point, path) => AreaService?.PointIsInClickableArea(gameController, point) ?? false,
                inputHandler,
                labelFilterService,
                shrineService,
                pathfindingService,
                new Func<bool>(labelService.GroundItemsVisible),
                cachedLabels,
                performanceMonitor);

            var ultimatumRenderer = new Rendering.UltimatumRenderer(settings, clickService, deferredFrameQueue);
            var alertService = owner.GetAlertService();
            var effectiveSettings = owner.GetEffectiveSettingsForLifecycle();

            PerformanceMonitor = performanceMonitor;
            ErrorHandler = errorHandler;
            AreaService = areaService;
            LabelService = labelService;
            CachedLabels = cachedLabels;
            Camera = camera;
            AltarService = altarService;
            LabelFilterService = labelFilterService;
            ShrineService = shrineService;
            InputHandler = inputHandler;
            PathfindingService = pathfindingService;
            DeferredTextQueue = deferredTextQueue;
            DeferredFrameQueue = deferredFrameQueue;
            DebugRenderer = debugRenderer;
            StrongboxRenderer = strongboxRenderer;
            LazyModeRenderer = lazyModeRenderer;
            ClickHotkeyToggleRenderer = clickHotkeyToggleRenderer;
            InventoryFullWarningRenderer = inventoryFullWarningRenderer;
            PathfindingRenderer = pathfindingRenderer;
            AltarDisplayRenderer = altarDisplayRenderer;
            ClickService = clickService;
            UltimatumRenderer = ultimatumRenderer;
            AlertService = alertService;

            settings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            settings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;
            if (!ReferenceEquals(settings, effectiveSettings))
            {
                effectiveSettings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
                effectiveSettings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;
            }

            _serviceRegistry.Register(() => settings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            _serviceRegistry.Register(() => settings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);
            if (!ReferenceEquals(settings, effectiveSettings))
            {
                _serviceRegistry.Register(() => effectiveSettings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
                _serviceRegistry.Register(() => effectiveSettings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);
            }
            _serviceRegistry.Register(() => ErrorHandler?.UnregisterGlobalExceptionHandlers());
            _serviceRegistry.Register(() => PerformanceMonitor?.ShutdownForHotReload());
            _serviceRegistry.Register(() => LastRenderTimer.Stop());
            _serviceRegistry.Register(() => LastTickTimer.Stop());
            _serviceRegistry.Register(() => Timer.Stop());
            _serviceRegistry.Register(() => SecondTimer.Stop());
        }

        public void FinalizeCompositionRootForStartup(ClickIt owner, ClickItSettings settings)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            settings.EnsureAllModsHaveWeights();

            AlertService?.ReloadAlertSound();
            PerformanceMonitor?.Start();

            LastRenderTimer.Start();
            LastTickTimer.Start();
            Timer.Start();
            SecondTimer.Start();
        }

        public void DisposeCompositionRoot()
        {
            _serviceRegistry.DisposeAll();

            AreaService = null;
            AltarService = null;
            ShrineService = null;
            InputHandler = null;
            DebugRenderer = null;
            StrongboxRenderer = null;
            UltimatumRenderer = null;
            LazyModeRenderer = null;
            ClickHotkeyToggleRenderer = null;
            InventoryFullWarningRenderer = null;
            PathfindingRenderer = null;
            DeferredTextQueue = null;
            DeferredFrameQueue = null;
            AltarDisplayRenderer = null;
            PathfindingService = null;
            AlertService = null;
            LabelService = null;
            LabelFilterService = null;
            ClickService = null;
            Camera = null;
            PerformanceMonitor = null;
            ErrorHandler = null;
            CachedLabels = null;
        }

        private sealed class DisposableServiceRegistry
        {
            private readonly List<Action> _teardownActions = new();

            public void Register(Action action)
            {
                if (action == null)
                    return;
                _teardownActions.Add(action);
            }

            public void DisposeAll()
            {
                for (int i = _teardownActions.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        _teardownActions[i]();
                    }
                    catch
                    {
                        // Best effort shutdown.
                    }
                }

                _teardownActions.Clear();
            }

            public void Reset()
            {
                _teardownActions.Clear();
            }
        }
    }
}