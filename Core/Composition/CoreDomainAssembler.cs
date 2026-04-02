using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;

namespace ClickIt.Composition
{
    internal readonly record struct CoreDomainServices(
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
        WeightCalculator WeightCalculator,
        DeferredTextQueue DeferredTextQueue,
        DeferredFrameQueue DeferredFrameQueue);

    internal static class CoreDomainAssembler
    {
        public static CoreDomainServices Assemble(ClickIt owner, ClickItSettings settings, GameController gameController)
        {
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

            return new CoreDomainServices(
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
                weightCalculator,
                deferredTextQueue,
                deferredFrameQueue);
        }
    }
}
