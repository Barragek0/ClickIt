namespace ClickIt.Core.Bootstrap
{
    internal readonly record struct CoreDomainServices(
        PerformanceMonitor PerformanceMonitor,
        ErrorHandler ErrorHandler,
        AreaService AreaService,
        LabelReadModelService LabelReadModelService,
        TimeCache<List<LabelOnGround>> CachedLabels,
        Camera Camera,
        AltarService AltarService,
        LabelFilterPort LabelFilterPort,
        LabelDebugService LabelDebugService,
        LazyModeBlockerService LazyModeBlockerService,
        InventoryProbeService InventoryProbeService,
        InventoryInteractionPolicy InventoryInteractionPolicy,
        ShrineService ShrineService,
        InputHandler InputHandler,
        PathfindingService PathfindingService,
        WeightCalculator WeightCalculator,
        DeferredTextQueue DeferredTextQueue,
        DeferredFrameQueue DeferredFrameQueue);

    internal static class CoreDomainAssembler
    {
        /**
        Keep this thin runtime entry wrapper so plugin startup continues to use the
        normal owner-facing API. The injected internal overload remains available
        for direct bootstrap tests and composition-only validation, and this wrapper
        should stay unless an equally testable boundary replaces it.
         */
        public static CoreDomainServices Assemble(ClickIt owner, ClickItSettings settings, GameController gameController)
            => Assemble(
                owner,
                settings,
                gameController,
                static (areaService, controller) => areaService.UpdateScreenAreas(controller),
                static controller => controller.Game?.IngameState?.Camera);

        internal static CoreDomainServices Assemble(
            ClickIt owner,
            ClickItSettings settings,
            GameController gameController,
            Action<AreaService, GameController> refreshScreenAreas,
            Func<GameController, Camera?> resolveCamera)
        {
            var performanceMonitor = new PerformanceMonitor(settings);
            var errorHandler = new ErrorHandler(settings, owner.LogError, owner.LogMessage);
            var camera = resolveCamera(gameController)
                ?? throw new InvalidOperationException("Camera is null during plugin initialization.");

            var areaService = new AreaService(settings);
            refreshScreenAreas(areaService, gameController);

            var labelReadModelService = new LabelReadModelService(
                gameController,
                point => areaService.PointIsInClickableArea(gameController, point));
            var cachedLabels = labelReadModelService.CachedLabels;

            var altarService = new AltarService(owner, settings, cachedLabels);
            var labelFilterPort = new LabelFilterPort(settings, new EssenceService(settings), errorHandler, gameController);
            var shrineService = new ShrineService(gameController, camera);
            var inputHandler = new InputHandler(settings);
            var pathfindingService = new PathfindingService(settings, errorHandler);
            var weightCalculator = new WeightCalculator(settings);

            var deferredTextQueue = new DeferredTextQueue();
            var deferredFrameQueue = new DeferredFrameQueue();

            return new CoreDomainServices(
                performanceMonitor,
                errorHandler,
                areaService,
                labelReadModelService,
                cachedLabels,
                camera,
                altarService,
                labelFilterPort,
                labelFilterPort.LabelDebugService,
                labelFilterPort.LazyModeBlockerService,
                labelFilterPort.InventoryProbeService,
                labelFilterPort.InventoryInteractionPolicy,
                shrineService,
                inputHandler,
                pathfindingService,
                weightCalculator,
                deferredTextQueue,
                deferredFrameQueue);
        }
    }
}