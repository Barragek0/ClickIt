namespace ClickIt.Core.Bootstrap
{
    internal static class ClickDomainAssembler
    {
        /**
        Keep this thin runtime entry wrapper so production composition still reads
        from the real owner and GameController path. The injected internal overload
        remains available for direct bootstrap tests without hidden runtime
        traversal, so do not remove this wrapper unless the same seam is preserved
        another way.
         */
        public static ClickAutomationPort Assemble(ClickIt owner, ClickItSettings settings, GameController gameController, CoreDomainServices core, AltarChoiceEvaluator altarChoiceEvaluator)
            => Assemble(
                settings,
                gameController,
                core,
                altarChoiceEvaluator,
                (point, _) => core.AreaService.PointIsInClickableArea(gameController, point),
                (point, _) => core.AreaService.PointIsInClickableArea(gameController, point, forceBlockedUiRefresh: false),
                core.LabelFilterPort.LabelSelectionService,
                owner.State.FreezeDebugTelemetrySnapshot);

        internal static ClickAutomationPort Assemble(
            ClickItSettings settings,
            GameController gameController,
            CoreDomainServices core,
            AltarChoiceEvaluator altarChoiceEvaluator,
            Func<Vector2, string, bool> pointIsInClickableArea,
            Func<Vector2, string, bool> forceRefreshPointIsInClickableArea,
            ILabelSelectionService labelSelectionService,
            Action<string, int>? freezeDebugTelemetrySnapshot)
        {
            LockManager.Instance = new LockManager(settings);

            ClickAutomationPort port = new(
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
                freezeDebugTelemetrySnapshot)
            {
                BlightChestDebug = core.BlightService.BlightChestDebug
            };
            return port;
        }
    }
}