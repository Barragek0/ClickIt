namespace ClickIt.Features.Observability.TelemetryProjection
{
    internal static class DebugTelemetryProjection
    {
        public static DebugTelemetrySnapshot Build(
            ClickService? clickService,
            LabelFilterService? labelFilterService,
            PathfindingService? pathfindingService)
        {
            ClickTelemetrySnapshot clickTelemetry = BuildClickTelemetry(clickService);
            LabelTelemetrySnapshot labelTelemetry = BuildLabelTelemetry(labelFilterService);
            PathfindingTelemetrySnapshot pathfindingTelemetry = BuildPathfindingTelemetry(pathfindingService);
            InventoryTelemetrySnapshot inventoryTelemetry = BuildInventoryTelemetry(labelFilterService);

            return new DebugTelemetrySnapshot(
                Click: clickTelemetry,
                Label: labelTelemetry,
                Pathfinding: pathfindingTelemetry,
                Inventory: inventoryTelemetry);
        }

        private static ClickTelemetrySnapshot BuildClickTelemetry(ClickService? clickService)
        {
            if (clickService == null)
                return ClickTelemetrySnapshot.Empty;

            List<UltimatumOptionPreviewSnapshot> ultimatumPreview = [];
            if (clickService.TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
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
                Click: clickService.GetLatestClickDebug(),
                ClickTrail: clickService.GetLatestClickDebugTrail(),
                RuntimeLog: clickService.GetLatestRuntimeDebugLog(),
                RuntimeLogTrail: clickService.GetLatestRuntimeDebugLogTrail(),
                Ultimatum: clickService.GetLatestUltimatumDebug(),
                UltimatumTrail: clickService.GetLatestUltimatumDebugTrail(),
                UltimatumOptionPreview: ultimatumPreview);
        }

        private static LabelTelemetrySnapshot BuildLabelTelemetry(LabelFilterService? labelFilterService)
        {
            if (labelFilterService == null)
                return LabelTelemetrySnapshot.Empty;

            return new LabelTelemetrySnapshot(
                ServiceAvailable: true,
                Label: labelFilterService.GetLatestLabelDebug(),
                LabelTrail: labelFilterService.GetLatestLabelDebugTrail());
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

        private static InventoryTelemetrySnapshot BuildInventoryTelemetry(LabelFilterService? labelFilterService)
        {
            if (labelFilterService == null)
                return new InventoryTelemetrySnapshot(
                    Inventory: InventoryDebugSnapshot.Empty,
                    InventoryTrail: []);

            return new InventoryTelemetrySnapshot(
                Inventory: labelFilterService.GetLatestInventoryDebug(),
                InventoryTrail: labelFilterService.GetLatestInventoryDebugTrail());
        }
    }
}

