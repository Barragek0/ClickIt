namespace ClickIt.Features.Observability.TelemetryProjection
{
    internal static partial class DebugTelemetryProjection
    {
        private static InventoryTelemetrySnapshot BuildInventoryTelemetry(LabelFilterPort? labelFilterPort)
        {
            if (labelFilterPort == null)
                return new InventoryTelemetrySnapshot(
                    Inventory: InventoryDebugSnapshot.Empty,
                    InventoryTrail: []);

            return new InventoryTelemetrySnapshot(
                Inventory: labelFilterPort.GetLatestInventoryDebug(),
                InventoryTrail: labelFilterPort.GetLatestInventoryDebugTrail());
        }

        private static HoveredItemMetadataTelemetrySnapshot BuildHoveredItemMetadataTelemetry(GameController? gameController)
        {
            IList<LabelOnGround>? visibleLabels = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (visibleLabels == null || visibleLabels.Count == 0)
                return HoveredItemMetadataTelemetrySnapshot.Empty;

            RectangleF windowRect = gameController?.Window.GetWindowRectangleTimeCache ?? RectangleF.Empty;
            var cursorPosition = Mouse.GetCursorPosition();
            bool cursorInsideWindow = IsCursorInsideWindow(windowRect, cursorPosition.X, cursorPosition.Y);
            if (!cursorInsideWindow)
            {
                return new HoveredItemMetadataTelemetrySnapshot(
                    LabelsAvailable: true,
                    CursorInsideWindow: false,
                    HasHoveredItem: false,
                    GroundItemName: string.Empty,
                    EntityPath: string.Empty,
                    MetadataPath: string.Empty);
            }

            LabelOnGround? hoveredLabel = TryResolveHoveredLabel(visibleLabels, windowRect, cursorPosition.X, cursorPosition.Y);
            if (hoveredLabel == null)
            {
                return new HoveredItemMetadataTelemetrySnapshot(
                    LabelsAvailable: true,
                    CursorInsideWindow: true,
                    HasHoveredItem: false,
                    GroundItemName: string.Empty,
                    EntityPath: string.Empty,
                    MetadataPath: string.Empty);
            }

            return new HoveredItemMetadataTelemetrySnapshot(
                LabelsAvailable: true,
                CursorInsideWindow: true,
                HasHoveredItem: true,
                GroundItemName: hoveredLabel.ItemOnGround?.RenderName ?? "<unknown>",
                EntityPath: hoveredLabel.ItemOnGround?.Path ?? string.Empty,
                MetadataPath: ResolveHoveredItemMetadataPath(hoveredLabel));
        }

        private static StatusTelemetrySnapshot BuildStatusTelemetry(
            GameController? gameController,
            TimeCache<List<LabelOnGround>>? cachedLabels)
        {
            if (gameController == null && cachedLabels == null)
                return StatusTelemetrySnapshot.Empty;

            IList<LabelOnGround>? visibleLabels = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            var player = gameController?.Player;

            return new StatusTelemetrySnapshot(
                GameControllerAvailable: gameController != null,
                InGame: gameController?.InGame == true,
                EntityListValid: gameController?.EntityListWrapper?.ValidEntitiesByType != null,
                PlayerValid: player != null,
                CurrentAreaName: gameController?.Area?.CurrentArea?.DisplayName ?? "Unknown",
                VisibleItemsAvailable: visibleLabels != null,
                VisibleItemCount: visibleLabels?.Count ?? 0,
                CachedLabelsAvailable: cachedLabels != null,
                CachedLabelCount: cachedLabels?.Value?.Count ?? 0,
                PlayerPositionAvailable: player != null,
                PlayerPositionX: player?.PosNum.X ?? 0,
                PlayerPositionY: player?.PosNum.Y ?? 0);
        }

        private static ErrorTelemetrySnapshot BuildErrorTelemetry(ErrorHandler? errorHandler)
        {
            if (errorHandler == null)
                return ErrorTelemetrySnapshot.Empty;

            return new ErrorTelemetrySnapshot(
                ServiceAvailable: true,
                RecentErrors: errorHandler.RecentErrors);
        }

        private static LabelOnGround? TryResolveHoveredLabel(
            IList<LabelOnGround> labels,
            RectangleF windowRect,
            int cursorX,
            int cursorY)
        {
            LabelOnGround? hovered = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label?.Label?.IsValid != true)
                    continue;

                object? rectObject = label.Label.GetClientRect();
                if (rectObject is not RectangleF labelRect)
                    continue;

                if (!IsCursorOverLabelRect(labelRect, windowRect, cursorX, cursorY))
                    continue;

                float distance = label.ItemOnGround?.DistancePlayer ?? float.MaxValue;
                if (distance < bestDistance)
                {
                    hovered = label;
                    bestDistance = distance;
                }
            }

            return hovered;
        }

        private static bool IsCursorInsideWindow(RectangleF windowRect, int cursorX, int cursorY)
            => windowRect != RectangleF.Empty && IsPointInRect(cursorX, cursorY, windowRect);

        private static bool IsCursorOverLabelRect(RectangleF labelRect, RectangleF windowRect, int cursorX, int cursorY)
        {
            if (labelRect.Width <= 0 || labelRect.Height <= 0)
                return false;

            float left = labelRect.Left + windowRect.X;
            float right = labelRect.Right + windowRect.X;
            float top = labelRect.Top + windowRect.Y;
            float bottom = labelRect.Bottom + windowRect.Y;

            return cursorX >= left && cursorX <= right && cursorY >= top && cursorY <= bottom;
        }

        private static bool IsPointInRect(int x, int y, RectangleF rect)
            => x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;

        private static string ResolveHoveredItemMetadataPath(LabelOnGround label)
        {
            try
            {
                return EntityHelpers.ResolveWorldItemMetadataPath(
                    label.ItemOnGround,
                    missingItemFallback: "<missing item>",
                    missingItemEntityFallback: "<missing WorldItem.ItemEntity>",
                    missingMetadataFallback: "<missing metadata/path>");
            }
            catch (Exception ex)
            {
                return $"<error: {ex.GetType().Name}>";
            }
        }
    }
}