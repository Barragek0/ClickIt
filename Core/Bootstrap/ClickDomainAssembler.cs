namespace ClickIt.Core.Bootstrap
{
    internal static class ClickDomainAssembler
    {
        public static ClickAutomationPort Assemble(ClickIt owner, ClickItSettings settings, GameController gameController, CoreDomainServices core, AltarDisplayRenderer altarDisplayRenderer)
        {
            LockManager.Instance = new LockManager(settings);

            return new ClickAutomationPort(
                settings,
                gameController,
                core.ErrorHandler,
                core.AltarService,
                core.WeightCalculator,
                altarDisplayRenderer,
                (point, _) => core.AreaService.PointIsInClickableArea(gameController, point),
                core.InputHandler,
                core.LabelFilterPort,
                core.ShrineService,
                core.PathfindingService,
                new Func<bool>(core.LabelReadModelService.GroundItemsVisible),
                core.CachedLabels,
                core.PerformanceMonitor,
                owner.State.FreezeDebugTelemetrySnapshot);
        }
    }
}