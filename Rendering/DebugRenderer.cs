using ExileCore;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using ClickIt.Utils;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using ClickIt.Services;

#nullable enable

namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        private const int DetailedDebugStartY = 120;
        private const int DetailedDebugLineHeight = 18;
        private const int DetailedDebugBaseX = 10;
        private const int DetailedDebugLinesPerColumn = 45;
        private const int DetailedDebugColumnShiftPx = 405;
        private const int DetailedDebugMaxColumns = 4;

        private readonly BaseSettingsPlugin<ClickItSettings> _plugin;
        private readonly AltarService? _altarService;
        private readonly AreaService? _areaService;
        private readonly WeightCalculator? _weightCalculator;
        private readonly DeferredTextQueue _deferredTextQueue;
        private readonly DeferredFrameQueue _deferredFrameQueue;

        public DebugRenderer(BaseSettingsPlugin<ClickItSettings> plugin,
                             AltarService? altarService = null,
                             AreaService? areaService = null,
                             WeightCalculator? weightCalculator = null,
                             DeferredTextQueue? deferredTextQueue = null,
                             DeferredFrameQueue? deferredFrameQueue = null)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _altarService = altarService;
            _areaService = areaService;
            _weightCalculator = weightCalculator;
            _deferredTextQueue = deferredTextQueue ?? new DeferredTextQueue();
            _deferredFrameQueue = deferredFrameQueue ?? new DeferredFrameQueue();
        }

        public void RenderDebugFrames(ClickItSettings settings)
        {
            if (_areaService == null) return;

            if (settings.DebugShowFrames)
            {
                var healthSquare = _areaService.HealthSquareRectangle;
                var flaskRect = _areaService.FlaskRectangle;
                var manaSquare = _areaService.ManaSquareRectangle;
                var skillsRect = _areaService.SkillsRectangle;

                // Fallback: derive split rectangles from combined regions if cached split fields are empty.
                if (IsEmptyRect(healthSquare) || IsEmptyRect(flaskRect))
                {
                    (healthSquare, flaskRect) = AreaService.SplitBottomAnchoredRectangleFromLeft(_areaService.HealthAndFlaskRectangle, 0.6f);
                }

                if (IsEmptyRect(manaSquare) || IsEmptyRect(skillsRect))
                {
                    (manaSquare, skillsRect) = AreaService.SplitBottomAnchoredRectangleFromRight(_areaService.ManaAndSkillsRectangle, 0.6f);
                }

                RectangleF healthSquareDraw = ToDrawRectangleFromLtrb(healthSquare);
                RectangleF flaskRectDraw = ToDrawRectangleFromLtrb(flaskRect);
                RectangleF skillsRectDraw = ToDrawRectangleFromLtrb(skillsRect);
                RectangleF manaSquareDraw = ToDrawRectangleFromLtrb(manaSquare);

                _deferredFrameQueue.Enqueue(_areaService.FullScreenRectangle, Color.LightSkyBlue, 1);
                _deferredFrameQueue.Enqueue(healthSquareDraw, Color.Red, 1);
                _deferredFrameQueue.Enqueue(flaskRectDraw, Color.OrangeRed, 1);
                _deferredFrameQueue.Enqueue(skillsRectDraw, Color.DeepSkyBlue, 1);
                _deferredFrameQueue.Enqueue(manaSquareDraw, Color.DeepSkyBlue, 1);
                var buffsAndDebuffsRects = _areaService.BuffsAndDebuffsRectangles;
                if (buffsAndDebuffsRects.Count > 0)
                {
                    for (int i = 0; i < buffsAndDebuffsRects.Count; i++)
                    {
                        _deferredFrameQueue.Enqueue(buffsAndDebuffsRects[i], Color.Plum, 1);
                    }
                }
                else
                {
                    _deferredFrameQueue.Enqueue(_areaService.BuffsAndDebuffsRectangle, Color.Plum, 1);
                }
                _deferredFrameQueue.Enqueue(_areaService.ChatPanelBlockedRectangle, Color.Green, 1);
                _deferredFrameQueue.Enqueue(_areaService.MapPanelBlockedRectangle, Color.Pink, 1);
                _deferredFrameQueue.Enqueue(_areaService.GameUiPanelBlockedRectangle, Color.Orange, 1);
                var questTrackerRects = _areaService.QuestTrackerBlockedRectangles;
                for (int i = 0; i < questTrackerRects.Count; i++)
                {
                    _deferredFrameQueue.Enqueue(questTrackerRects[i], Color.Lavender, 1);
                }
            }
        }

        private static bool IsEmptyRect(RectangleF rect)
        {
            return rect.X == 0f && rect.Y == 0f && rect.Width == 0f && rect.Height == 0f;
        }

        private static RectangleF ToDrawRectangleFromLtrb(RectangleF rect)
        {
            float width = Math.Max(0f, rect.Width - rect.X);
            float height = Math.Max(0f, rect.Height - rect.Y);
            return new RectangleF(rect.X, rect.Y, width, height);
        }

        public void RenderDetailedDebugInfo(ClickItSettings settings, PerformanceMonitor performanceMonitor)
        {
            if (settings == null || performanceMonitor == null) return;

            // avoid doing any work if nothing to render
            if (!settings.IsAnyDetailedDebugSectionEnabled())
            {
                return;
            }

            const int startY = DetailedDebugStartY;
            const int lineHeight = DetailedDebugLineHeight;
            const int baseX = DetailedDebugBaseX;
            const int linesPerColumn = DetailedDebugLinesPerColumn;
            const int columnShiftPx = DetailedDebugColumnShiftPx;
            const int maxColumns = DetailedDebugMaxColumns;

            int currentColumn = 0;
            int xPos = baseX;
            int yPos = startY;

            void RenderSectionIfEnabled(bool enabled, Func<int, int, int, int> renderSection)
            {
                if (!enabled)
                {
                    return;
                }

                (currentColumn, xPos, yPos) = ResolveDebugColumnForNextSection(
                    currentColumn,
                    xPos,
                    yPos,
                    startY,
                    lineHeight,
                    linesPerColumn,
                    maxColumns,
                    baseX,
                    columnShiftPx);

                yPos = renderSection(xPos, yPos, lineHeight);
            }

            RenderSectionIfEnabled(settings.DebugShowStatus, (x, y, h) => RenderPluginStatusDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowGameState, (x, y, h) => RenderGameStateDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowPerformance, (x, y, h) => RenderPerformanceDebug(x, y, h, performanceMonitor));
            RenderSectionIfEnabled(settings.DebugShowClickFrequencyTarget, (x, y, h) => RenderClickFrequencyTargetDebug(x, y, h, performanceMonitor));
            RenderSectionIfEnabled(settings.DebugShowAltarDetection, (x, y, h) => RenderAltarDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowAltarService, (x, y, h) => RenderAltarServiceDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowLabels, (x, y, h) => RenderLabelsDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowHoveredItemMetadata, (x, y, h) => RenderHoveredItemMetadataDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowPathfinding, (x, y, h) => RenderPathfindingDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowClicking, (x, y, h) => RenderClickingDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowRecentErrors, (x, y, h) => RenderErrorsDebug(x, y, h));
        }

        private static (int NextColumn, int NextX, int NextY) ResolveDebugColumnForNextSection(
            int currentColumn,
            int currentX,
            int currentY,
            int startY,
            int lineHeight,
            int linesPerColumn,
            int maxColumns,
            int baseX,
            int columnShiftPx)
        {
            if (lineHeight <= 0 || linesPerColumn <= 0 || maxColumns <= 0)
            {
                return (currentColumn, currentX, currentY);
            }

            int usedLines = Math.Max(0, (currentY - startY) / lineHeight);
            if (usedLines < linesPerColumn || currentColumn >= maxColumns - 1)
            {
                return (currentColumn, currentX, currentY);
            }

            int nextColumn = currentColumn + 1;
            int nextX = baseX + (nextColumn * columnShiftPx);
            return (nextColumn, nextX, startY);
        }

        private int RenderDebugTrailBlock(int xPos, int yPos, int lineHeight, IReadOnlyList<string> trail, int maxRows, int trimWidth)
        {
            if (trail == null || trail.Count == 0 || lineHeight <= 0)
                return yPos;

            int remainingLines = GetRemainingLinesInCurrentDebugColumn(yPos);
            if (remainingLines <= 0)
            {
                return yPos;
            }

            _deferredTextQueue.Enqueue("Recent Stages:", new Vector2(xPos, yPos), Color.LightBlue, 13);
            yPos += lineHeight;

            remainingLines--;
            if (remainingLines <= 0)
            {
                return yPos;
            }

            int rowsToRender = Math.Min(Math.Max(1, maxRows), remainingLines);
            int start = Math.Max(0, trail.Count - rowsToRender);
            for (int i = start; i < trail.Count; i++)
            {
                _deferredTextQueue.Enqueue($"  {TrimForDebug(trail[i], trimWidth)}", new Vector2(xPos, yPos), Color.LightGray, 12);
                yPos += lineHeight;
            }

            return yPos;
        }

        private static int GetRemainingLinesInCurrentDebugColumn(int yPos)
        {
            int usedLines = Math.Max(0, (yPos - DetailedDebugStartY) / DetailedDebugLineHeight);
            return Math.Max(0, DetailedDebugLinesPerColumn - usedLines);
        }

        public int RenderPluginStatusDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue($"--- ClickIt Status ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var gameController = _plugin.GameController;
            bool inGame = gameController?.InGame == true;
            Color gameColor = inGame ? Color.LightGreen : Color.Red;
            _deferredTextQueue.Enqueue($"In Game: {inGame}", new Vector2(xPos, yPos), gameColor, 16);
            yPos += lineHeight;
            bool entityListValid = gameController?.EntityListWrapper?.ValidEntitiesByType != null;
            Color entityColor = entityListValid ? Color.LightGreen : Color.Red;
            _deferredTextQueue.Enqueue($"Entity List Valid: {entityListValid}", new Vector2(xPos, yPos), entityColor, 16);
            yPos += lineHeight;
            bool playerValid = gameController?.Player != null;
            Color playerColor = playerValid ? Color.LightGreen : Color.Red;
            _deferredTextQueue.Enqueue($"Player Valid: {playerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;

            return yPos;
        }
        public int RenderGameStateDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue($"--- Game State ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var gameController = _plugin.GameController;
            var currentArea = gameController?.Area?.CurrentArea;
            string areaName = currentArea?.DisplayName ?? "Unknown";
            _deferredTextQueue.Enqueue($"Current Area: {areaName}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;

            bool hasItems = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count > 0;
            Color itemColor = hasItems ? Color.LightGreen : Color.Gray;
            int itemCount = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count ?? 0;
            _deferredTextQueue.Enqueue($"Items on Ground: {itemCount}", new Vector2(xPos, yPos), itemColor, 16);
            yPos += lineHeight;

            if (_plugin is ClickIt clickItPlugin)
            {
                var cachedLabels = clickItPlugin.State.CachedLabels;
                if (cachedLabels != null)
                {
                    var labels = cachedLabels.Value;
                    int cachedCount = labels?.Count ?? 0;
                    _deferredTextQueue.Enqueue($"Cached Labels: {cachedCount}", new Vector2(xPos, yPos), Color.White, 16);
                    yPos += lineHeight;
                }
                else
                {
                    _deferredTextQueue.Enqueue($"Cache Info Unavailable", new Vector2(xPos, yPos), Color.Gray, 16);
                }
                yPos += lineHeight;
            }

            bool playerValid = gameController?.Player != null;
            Color playerColor = playerValid ? Color.LightGreen : Color.Red;
            _deferredTextQueue.Enqueue($"Player Valid: {playerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;

            if (playerValid && gameController?.Player != null)
            {
                var playerPos = gameController.Player.Pos;
                _deferredTextQueue.Enqueue($"Player Pos: ({playerPos.X:F1}, {playerPos.Y:F1})", new Vector2(xPos, yPos), Color.White, 14);
                yPos += lineHeight;
            }

            return yPos;
        }
        public int RenderWrappedText(string text, Vector2 position, Color color, int fontSize, int lineHeight, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
                return (int)(position.Y + lineHeight);
            int currentY = (int)position.Y;
            int startIndex = 0;

            int leadingSpaces = 0;
            while (leadingSpaces < text.Length && text[leadingSpaces] == ' ')
            {
                leadingSpaces++;
            }
            string indentation = new(' ', leadingSpaces);
            ReadOnlySpan<char> content = text.AsSpan(leadingSpaces);
            int contentLength = content.Length;

            while (startIndex < contentLength)
            {
                int endIndex = Math.Min(startIndex + maxCharsPerLine, contentLength);
                if (endIndex < contentLength)
                {
                    ReadOnlySpan<char> segment = content.Slice(startIndex, endIndex - startIndex);
                    int lastSpaceOffset = segment.LastIndexOf(' ');
                    if (lastSpaceOffset > 0)
                    {
                        endIndex = startIndex + lastSpaceOffset;
                    }
                }

                ReadOnlySpan<char> lineSpan = content.Slice(startIndex, endIndex - startIndex).TrimEnd();
                string line = lineSpan.ToString();
                _deferredTextQueue.Enqueue(indentation + line, new Vector2(position.X, currentY), color, fontSize);
                currentY += lineHeight;
                startIndex = endIndex;
                if (startIndex < contentLength && content[startIndex] == ' ')
                {
                    startIndex++;
                }
            }
            return currentY;
        }
        private static bool IsPointInRect(int x, int y, RectangleF rect)
        {
            return x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;
        }

        private static bool IsCursorInsideWindow(RectangleF windowRect, int cursorX, int cursorY)
        {
            return windowRect != RectangleF.Empty && IsPointInRect(cursorX, cursorY, windowRect);
        }

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

        private int RenderPathfindingDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue("--- Pathfinding ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_plugin is not ClickIt clickIt || clickIt.State.PathfindingService == null)
            {
                _deferredTextQueue.Enqueue("Pathfinding service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            var snap = clickIt.State.PathfindingService.GetDebugSnapshot();
            Color terrainColor = snap.TerrainLoaded ? Color.LightGreen : Color.Red;
            _deferredTextQueue.Enqueue($"Terrain Loaded: {snap.TerrainLoaded}", new Vector2(xPos, yPos), terrainColor, 14);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Grid: {snap.AreaWidth} x {snap.AreaHeight}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Expanded Nodes: {snap.LastExpandedNodes}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Path Length: {snap.LastPathLength}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Compute: {snap.LastComputeMs} ms", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            string targetPath = string.IsNullOrWhiteSpace(snap.LastTargetPath) ? "<none>" : snap.LastTargetPath;
            yPos = RenderWrappedText($"Target Path: {targetPath}", new Vector2(xPos, yPos), Color.LightBlue, 14, lineHeight, 46);

            if (!string.IsNullOrWhiteSpace(snap.LastFailureReason))
            {
                yPos = RenderWrappedText($"Failure: {snap.LastFailureReason}", new Vector2(xPos, yPos), Color.OrangeRed, 14, lineHeight, 46);
            }

            var movement = clickIt.State.PathfindingService.GetLatestOffscreenMovementDebug();
            if (!movement.HasData)
            {
                _deferredTextQueue.Enqueue("Offscreen Movement: <no data>", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            Vector2 gridDelta = movement.TargetGrid - movement.PlayerGrid;
            Vector2 targetDelta = movement.TargetScreen - movement.WindowCenter;
            Vector2 clickDelta = movement.ClickScreen - movement.WindowCenter;

            _deferredTextQueue.Enqueue(
                $"Offscreen Stage: {movement.Stage} | built={movement.BuiltPath} | fromPath={movement.ResolvedFromPath} | clickPoint={movement.ResolvedClickPoint}",
                new Vector2(xPos, yPos),
                Color.White,
                14);
            yPos += lineHeight;

            if (!string.IsNullOrWhiteSpace(movement.MovementSkillDebug))
            {
                yPos = RenderWrappedText(
                    $"Movement Skill Debug: {movement.MovementSkillDebug}",
                    new Vector2(xPos, yPos),
                    Color.Yellow,
                    14,
                    lineHeight,
                    46);
            }

            yPos = RenderWrappedText(
                $"Offscreen Target: {TrimPath(movement.TargetPath)}",
                new Vector2(xPos, yPos),
                Color.LightBlue,
                14,
                lineHeight,
                46);

            _deferredTextQueue.Enqueue(
                $"Grid P=({movement.PlayerGrid.X:0},{movement.PlayerGrid.Y:0}) T=({movement.TargetGrid.X:0},{movement.TargetGrid.Y:0}) d=({gridDelta.X:0},{gridDelta.Y:0})",
                new Vector2(xPos, yPos),
                Color.Orange,
                14);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue(
                $"Target Delta=({targetDelta.X:0.0},{targetDelta.Y:0.0}) dir={PathfindingRenderer.ToCompass(targetDelta)}",
                new Vector2(xPos, yPos),
                Color.Cyan,
                14);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue(
                $"Click Delta=({clickDelta.X:0.0},{clickDelta.Y:0.0}) dir={PathfindingRenderer.ToCompass(clickDelta)}",
                new Vector2(xPos, yPos),
                Color.Lime,
                14);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue(
                $"Center=({movement.WindowCenter.X:0.0},{movement.WindowCenter.Y:0.0}) Target=({movement.TargetScreen.X:0.0},{movement.TargetScreen.Y:0.0}) Click=({movement.ClickScreen.X:0.0},{movement.ClickScreen.Y:0.0})",
                new Vector2(xPos, yPos),
                Color.Gray,
                14);
            yPos += lineHeight;

            return yPos;
        }

        private static string TrimPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "<none>";

            return path.Length <= 80 ? path : path[^80..];
        }

    }
}
