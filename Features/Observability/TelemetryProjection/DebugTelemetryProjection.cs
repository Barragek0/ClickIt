namespace ClickIt.Features.Observability.TelemetryProjection
{
    internal static class DebugTelemetryProjection
    {
        public static DebugTelemetrySnapshot Build(
            ClickService? clickAutomationPort,
            LabelFilterService? labelFilterPort,
            PathfindingService? pathfindingService)
        {
            ClickTelemetrySnapshot clickTelemetry = BuildClickTelemetry(clickAutomationPort);
            LabelTelemetrySnapshot labelTelemetry = BuildLabelTelemetry(labelFilterPort);
            PathfindingTelemetrySnapshot pathfindingTelemetry = BuildPathfindingTelemetry(pathfindingService);
            InventoryTelemetrySnapshot inventoryTelemetry = BuildInventoryTelemetry(labelFilterPort);

            return new DebugTelemetrySnapshot(
                Click: clickTelemetry,
                Label: labelTelemetry,
                Pathfinding: pathfindingTelemetry,
                Inventory: inventoryTelemetry);
        }

        private static ClickTelemetrySnapshot BuildClickTelemetry(ClickService? clickAutomationPort)
        {
            if (clickAutomationPort == null)
                return ClickTelemetrySnapshot.Empty;

            List<UltimatumOptionPreviewSnapshot> ultimatumPreview = [];
            if (clickAutomationPort.TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
                && previews.Count > 0)
            {
                for (int i = 0; i < previews.Count; i++)
                {
                    UltimatumPanelOptionPreview preview = previews[i];
                    ultimatumPreview.Add(new UltimatumOptionPreviewSnapshot(
                        Rect: preview.Rect,
                        ModifierName: preview.ModifierName,
                        PriorityIndex: preview.PriorityIndex,
                        IsSelected: preview.IsSelected));
                }
            }

            return new ClickTelemetrySnapshot(
                ServiceAvailable: true,
                Click: clickAutomationPort.GetLatestClickDebug(),
                ClickTrail: clickAutomationPort.GetLatestClickDebugTrail(),
                RuntimeLog: clickAutomationPort.GetLatestRuntimeDebugLog(),
                RuntimeLogTrail: clickAutomationPort.GetLatestRuntimeDebugLogTrail(),
                Ultimatum: clickAutomationPort.GetLatestUltimatumDebug(),
                UltimatumTrail: clickAutomationPort.GetLatestUltimatumDebugTrail(),
                UltimatumOptionPreview: ultimatumPreview);
        }

        private static LabelTelemetrySnapshot BuildLabelTelemetry(LabelFilterService? labelFilterPort)
        {
            if (labelFilterPort == null)
                return LabelTelemetrySnapshot.Empty;

            return new LabelTelemetrySnapshot(
                ServiceAvailable: true,
                Label: labelFilterPort.GetLatestLabelDebug(),
                LabelTrail: labelFilterPort.GetLatestLabelDebugTrail());
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

        private static InventoryTelemetrySnapshot BuildInventoryTelemetry(LabelFilterService? labelFilterPort)
        {
            if (labelFilterPort == null)
                return new InventoryTelemetrySnapshot(
                    Inventory: InventoryDebugSnapshot.Empty,
                    InventoryTrail: []);

            return new InventoryTelemetrySnapshot(
                Inventory: labelFilterPort.GetLatestInventoryDebug(),
                InventoryTrail: labelFilterPort.GetLatestInventoryDebugTrail());
        }
    }
}

