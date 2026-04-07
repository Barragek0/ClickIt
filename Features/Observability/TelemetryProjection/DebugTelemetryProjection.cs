namespace ClickIt.Features.Observability.TelemetryProjection
{
    internal static partial class DebugTelemetryProjection
    {
        public static DebugTelemetrySnapshot Build(
            ClickAutomationPort? clickAutomationPort,
            ClickAutomationSupport? clickAutomationSupport,
            LabelDebugService? labelDebugService,
            LazyModeBlockerService? lazyModeBlockerService,
            InventoryProbeService? inventoryProbeService,
            PathfindingService? pathfindingService,
            AltarService? altarService,
            WeightCalculator? weightCalculator,
            PluginRenderingState? renderingState,
            GameController? gameController,
            InputHandler? inputHandler,
            ClickItSettings? settings,
            TimeCache<List<LabelOnGround>>? cachedLabels,
            ErrorHandler? errorHandler)
        {
            ClickTelemetrySnapshot clickTelemetry = BuildClickTelemetry(
                clickAutomationPort,
                clickAutomationSupport,
                lazyModeBlockerService,
                gameController,
                inputHandler,
                settings,
                cachedLabels);
            LabelTelemetrySnapshot labelTelemetry = BuildLabelTelemetry(labelDebugService);
            PathfindingTelemetrySnapshot pathfindingTelemetry = BuildPathfindingTelemetry(pathfindingService);
            RenderingTelemetrySnapshot renderingTelemetry = BuildRenderingTelemetry(renderingState);
            StatusTelemetrySnapshot statusTelemetry = BuildStatusTelemetry(gameController, cachedLabels);
            ErrorTelemetrySnapshot errorTelemetry = BuildErrorTelemetry(errorHandler);
            InventoryTelemetrySnapshot inventoryTelemetry = BuildInventoryTelemetry(inventoryProbeService);
            AltarTelemetrySnapshot altarTelemetry = BuildAltarTelemetry(altarService, weightCalculator);
            HoveredItemMetadataTelemetrySnapshot hoveredItemTelemetry = BuildHoveredItemMetadataTelemetry(gameController);

            return new DebugTelemetrySnapshot(
                Click: clickTelemetry,
                Label: labelTelemetry,
                Pathfinding: pathfindingTelemetry,
                Rendering: renderingTelemetry,
                Status: statusTelemetry,
                Errors: errorTelemetry,
                Inventory: inventoryTelemetry,
                Altar: altarTelemetry,
                HoveredItem: hoveredItemTelemetry);
        }

        private static LabelTelemetrySnapshot BuildLabelTelemetry(LabelDebugService? labelDebugService)
        {
            if (labelDebugService == null)
                return LabelTelemetrySnapshot.Empty;

            (bool labelsAvailable, int totalVisibleLabels, int validVisibleLabels) = labelDebugService.GetVisibleLabelCounts();

            return new LabelTelemetrySnapshot(
                ServiceAvailable: true,
                Label: labelDebugService.GetLatestDebug(),
                LabelTrail: labelDebugService.GetLatestDebugTrail(),
                LabelsAvailable: labelsAvailable,
                TotalVisibleLabels: totalVisibleLabels,
                ValidVisibleLabels: validVisibleLabels);
        }

        private static PathfindingTelemetrySnapshot BuildPathfindingTelemetry(PathfindingService? pathfindingService)
        {
            if (pathfindingService == null)
                return PathfindingTelemetrySnapshot.Empty;

            return new PathfindingTelemetrySnapshot(
                ServiceAvailable: true,
                Pathfinding: pathfindingService.GetDebugSnapshot(),
                OffscreenMovement: pathfindingService.GetLatestOffscreenMovementDebug(),
                OffscreenMovementTrail: pathfindingService.GetLatestOffscreenMovementDebugTrail());
        }

        private static RenderingTelemetrySnapshot BuildRenderingTelemetry(PluginRenderingState? renderingState)
        {
            if (renderingState == null)
                return RenderingTelemetrySnapshot.Empty;

            return new RenderingTelemetrySnapshot(
                ServiceAvailable: true,
                PendingTextCount: renderingState.DeferredTextQueue?.GetPendingCount() ?? 0,
                PendingFrameCount: renderingState.DeferredFrameQueue?.GetPendingCount() ?? 0);
        }
    }
}

