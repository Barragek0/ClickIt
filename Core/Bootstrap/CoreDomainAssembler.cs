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
        DeferredFrameQueue DeferredFrameQueue,
        HarvestService HarvestService,
        HarvestOverlayRenderer HarvestOverlayRenderer,
        BlightService BlightService,
        BlightRenderer BlightRenderer);

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
            PerformanceMonitor performanceMonitor = new(settings);
            ErrorHandler errorHandler = new(settings, owner.LogError, owner.LogMessage);
            Camera camera = resolveCamera(gameController)
                ?? throw new InvalidOperationException("Camera is null during plugin initialization.");

            AreaService areaService = new(settings);
            refreshScreenAreas(areaService, gameController);

            LabelReadModelService labelReadModelService = new(
                gameController,
                point => areaService.PointIsInClickableArea(gameController, point));
            TimeCache<List<LabelOnGround>> cachedLabels = labelReadModelService.CachedLabels;

            AltarService altarService = new(owner, settings, cachedLabels);
            LabelFilterPort labelFilterPort = new(settings, new EssenceService(settings), errorHandler, gameController);
            ShrineService shrineService = new(gameController, camera);
            InputHandler inputHandler = new(settings);
            PathfindingService pathfindingService = new(errorHandler);
            WeightCalculator weightCalculator = new(settings);
            HarvestService harvestService = new(settings);

            DeferredTextQueue deferredTextQueue = new();
            DeferredFrameQueue deferredFrameQueue = new();
            HarvestOverlayRenderer harvestOverlayRenderer = new(harvestService, settings, deferredTextQueue, deferredFrameQueue);

            BlightService blightService = new(settings, point => areaService.PointIsInClickableArea(gameController, point));
            BlightRenderer blightRenderer = new(blightService, settings);

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
                deferredFrameQueue,
                harvestService,
                harvestOverlayRenderer,
                blightService,
                blightRenderer);
        }
    }
}