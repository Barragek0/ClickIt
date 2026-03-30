using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;

namespace ClickIt.Composition
{
    internal sealed record ComposedServices(
        PerformanceMonitor PerformanceMonitor,
        ErrorHandler ErrorHandler,
        AreaService AreaService,
        LabelService LabelService,
        TimeCache<List<LabelOnGround>> CachedLabels,
        Camera Camera,
        AltarService AltarService,
        LabelFilterService LabelFilterService,
        ShrineService ShrineService,
        InputHandler InputHandler,
        PathfindingService PathfindingService,
        DeferredTextQueue DeferredTextQueue,
        DeferredFrameQueue DeferredFrameQueue,
        Rendering.DebugRenderer DebugRenderer,
        Rendering.StrongboxRenderer StrongboxRenderer,
        Rendering.LazyModeRenderer LazyModeRenderer,
        Rendering.ClickHotkeyToggleRenderer ClickHotkeyToggleRenderer,
        Rendering.InventoryFullWarningRenderer InventoryFullWarningRenderer,
        Rendering.PathfindingRenderer PathfindingRenderer,
        Rendering.AltarDisplayRenderer AltarDisplayRenderer,
        ClickService ClickService,
        Rendering.UltimatumRenderer UltimatumRenderer,
        AlertService AlertService,
        ClickItSettings EffectiveSettings);

    internal static class ServiceCompositionRoot
    {
        public static ComposedServices Compose(ClickIt owner, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(settings);

            var gameController = owner.GameController
                ?? throw new InvalidOperationException("GameController is null during plugin initialization.");

            var performanceMonitor = new PerformanceMonitor(settings);
            var errorHandler = new ErrorHandler(settings, owner.LogError, owner.LogMessage);
            var areaService = new AreaService();
            areaService.UpdateScreenAreas(gameController);

            var labelService = new LabelService(
                gameController,
                point => areaService.PointIsInClickableArea(gameController, point));
            var cachedLabels = labelService.CachedLabels;
            var camera = gameController.Game?.IngameState?.Camera
                ?? throw new InvalidOperationException("Camera is null during plugin initialization.");
            var altarService = new AltarService(owner, settings, cachedLabels);
            var labelFilterService = new LabelFilterService(settings, new EssenceService(settings), errorHandler, gameController);
            var shrineService = new ShrineService(gameController, camera);
            var inputHandler = new InputHandler(settings, performanceMonitor, errorHandler);
            var pathfindingService = new PathfindingService(settings, errorHandler);
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

            var clickService = new ClickService(
                settings,
                gameController,
                errorHandler,
                altarService,
                weightCalculator,
                altarDisplayRenderer,
                (point, _) => areaService.PointIsInClickableArea(gameController, point),
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

            return new ComposedServices(
                performanceMonitor,
                errorHandler,
                areaService,
                labelService,
                cachedLabels,
                camera,
                altarService,
                labelFilterService,
                shrineService,
                inputHandler,
                pathfindingService,
                deferredTextQueue,
                deferredFrameQueue,
                debugRenderer,
                strongboxRenderer,
                lazyModeRenderer,
                clickHotkeyToggleRenderer,
                inventoryFullWarningRenderer,
                pathfindingRenderer,
                altarDisplayRenderer,
                clickService,
                ultimatumRenderer,
                alertService,
                effectiveSettings);
        }

        public static void WireSettingsActions(
            ClickItSettings settings,
            ClickItSettings effectiveSettings,
            AlertService alertService,
            ServiceDisposalRegistry registry)
        {
            settings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            settings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;

            registry.Register(() => settings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            registry.Register(() => settings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);

            if (ReferenceEquals(settings, effectiveSettings))
                return;

            effectiveSettings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            effectiveSettings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;

            registry.Register(() => effectiveSettings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            registry.Register(() => effectiveSettings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);
        }
    }
}
