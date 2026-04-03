namespace ClickIt.Core.Bootstrap
{
    internal static class ClickDomainAssembler
    {
        public static ClickService Assemble(ClickIt owner, ClickItSettings settings, GameController gameController, CoreDomainServices core, AltarDisplayRenderer altarDisplayRenderer)
        {
            LockManager.Instance = new LockManager(settings);

            return new ClickService(
                settings,
                gameController,
                core.ErrorHandler,
                core.AltarService,
                core.WeightCalculator,
                altarDisplayRenderer,
                (point, _) => core.AreaService.PointIsInClickableArea(gameController, point),
                core.InputHandler,
                core.LabelFilterService,
                core.ShrineService,
                core.PathfindingService,
                new Func<bool>(core.LabelService.GroundItemsVisible),
                core.CachedLabels,
                core.PerformanceMonitor,
                owner.State.FreezeDebugTelemetrySnapshot);
        }
    }
}