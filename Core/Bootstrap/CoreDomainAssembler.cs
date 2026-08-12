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
        BlightService BlightService,
        DeferredDrawQueue DeferredDrawQueue);

    internal static class CoreDomainAssembler
    {
        // Thin runtime entry wrapper so the injected internal overload stays testable without runtime traversal.
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
                ms => performanceMonitor.RecordProcessingTiming(ProcessingSection.Label, ms),
                bytes => performanceMonitor.RecordAllocation(ProcessingSection.Label, bytes),
                breakdown => performanceMonitor.RecordLabelScanAllocation(breakdown));
            TimeCache<List<LabelOnGround>> cachedLabels = labelReadModelService.CachedLabels;

            AltarService altarService = new(owner, settings, cachedLabels,
                (ReadOnlySpan<long> bytes, ReadOnlySpan<double> ms) => performanceMonitor.RecordBreakdown(ProcessingSection.Altar, bytes, ms));
            LabelFilterPort labelFilterPort = new(settings, new EssenceService(settings), errorHandler, gameController);
            ShrineService shrineService = new(gameController, camera);
            InputHandler inputHandler = new(settings);
            PathfindingService pathfindingService = new(
                errorHandler,
                ms => performanceMonitor.RecordProcessingTiming(ProcessingSection.Pathfinding, ms),
                bytes => performanceMonitor.RecordAllocation(ProcessingSection.Pathfinding, bytes),
                (ReadOnlySpan<long> bytes, ReadOnlySpan<double> ms) => performanceMonitor.RecordBreakdown(ProcessingSection.Pathfinding, bytes, ms));
            WeightCalculator weightCalculator = new(settings);
            HarvestService harvestService = new(settings);

            DeferredTextQueue deferredTextQueue = new();
            DeferredFrameQueue deferredFrameQueue = new();
            DeferredDrawQueue deferredDrawQueue = new();

            BlightService blightService = new(
                settings,
                point => areaService.PointIsInClickableArea(gameController, point),
                (ReadOnlySpan<long> bytes, ReadOnlySpan<double> ms) => performanceMonitor.RecordBreakdown(ProcessingSection.Blight, bytes, ms),
                (bytes, ms) => performanceMonitor.RecordBreakdownStage(ProcessingSection.Blight, PerformanceMonitor.BlightExecutorStageIndex, bytes, ms),
                (bytes, ms) => performanceMonitor.RecordBreakdownStage(ProcessingSection.Blight, PerformanceMonitor.BlightEventsStageIndex, bytes, ms));

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
                blightService,
                deferredDrawQueue);
        }
    }
}