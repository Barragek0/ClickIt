namespace ClickIt.Core.Bootstrap
{
    internal static class ClickDomainAssembler
    {
        public static ClickAutomationPort Assemble(ClickIt owner, ClickItSettings settings, GameController gameController, CoreDomainServices core, AltarChoiceEvaluator altarChoiceEvaluator)
        {
            LockManager.Instance = new LockManager(settings);

            return new ClickAutomationPort(
                settings,
                gameController,
                core.ErrorHandler,
                core.AltarService,
                core.WeightCalculator,
                altarChoiceEvaluator,
                (point, _) => core.AreaService.PointIsInClickableArea(gameController, point),
                (point, _) => core.AreaService.PointIsInClickableArea(gameController, point, forceBlockedUiRefresh: false),
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