namespace ClickIt.Core.Bootstrap
{
    internal static class ClickDomainAssembler
    {
        // Thin runtime entry wrapper so the injected internal overload stays testable without runtime traversal.
        public static ClickAutomationPort Assemble(ClickIt owner, ClickItSettings settings, GameController gameController, CoreDomainServices core, AltarChoiceEvaluator altarChoiceEvaluator)
            => Assemble(
                settings,
                gameController,
                core,
                altarChoiceEvaluator,
                (point, _) => core.AreaService.PointIsInClickableArea(gameController, point),
                (point, _) => core.AreaService.PointIsInClickableArea(gameController, point, forceBlockedUiRefresh: false),
                core.LabelFilterPort.LabelSelectionService,
                owner.State.FreezeDebugTelemetrySnapshot, core.InventoryInteractionPolicy);

        internal static ClickAutomationPort Assemble(
            ClickItSettings settings,
            GameController gameController,
            CoreDomainServices core,
            AltarChoiceEvaluator altarChoiceEvaluator,
            Func<Vector2, string, bool> pointIsInClickableArea,
            Func<Vector2, string, bool> forceRefreshPointIsInClickableArea,
            ILabelSelectionService labelSelectionService,
            Action<string, int>? freezeDebugTelemetrySnapshot,
            InventoryInteractionPolicy? inventory = null)
        {
            LockManager.Instance = new LockManager(settings);

            ClickAutomationPort port = new(new ClickAutomationPortDependencies(
                settings,
                gameController,
                core.ErrorHandler,
                core.AltarService,
                core.WeightCalculator,
                altarChoiceEvaluator,
                pointIsInClickableArea,
                forceRefreshPointIsInClickableArea,
                core.InputHandler,
                core.LabelFilterPort,
                labelSelectionService,
                core.ShrineService,
                core.PathfindingService,
                new Func<bool>(core.LabelReadModelService.GroundItemsVisible),
                core.CachedLabels,
                core.PerformanceMonitor,
                freezeDebugTelemetrySnapshot,
                inventory ?? core.InventoryInteractionPolicy))
            {
                BlightChestDebug = core.BlightService.BlightChestDebug,
                HarvestUpgrade = core.HarvestUpgrade
            };
            return port;
        }
    }
}
