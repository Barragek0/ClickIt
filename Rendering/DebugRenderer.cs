using ExileCore;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using ClickIt.Utils;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using ClickIt.Services;
using ClickIt.Services.Observability;

#nullable enable

namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        private const int DetailedDebugStartY = 120;
        private const int DetailedDebugLineHeight = 18;
        private const int DetailedDebugBaseX = 10;
        private const int DetailedDebugLinesPerColumn = 34;
        private const int DetailedDebugColumnShiftPx = 600;
        private const int DetailedDebugMaxColumns = 4;

        private readonly BaseSettingsPlugin<ClickItSettings> _plugin;
        private readonly AltarService? _altarService;
        private readonly AreaService? _areaService;
        private readonly WeightCalculator? _weightCalculator;
        private readonly DeferredTextQueue _deferredTextQueue;
        private readonly DeferredFrameQueue _deferredFrameQueue;
        private readonly IDebugTelemetrySource _debugTelemetrySource;
        private readonly Debug.DebugOverlayRenderContext _overlayContext;
        private readonly Debug.Sections.ClickingDebugOverlaySection _clickingDebugOverlaySection;
        private readonly Debug.Sections.LabelDebugOverlaySection _labelDebugOverlaySection;
        private readonly Debug.Sections.UltimatumDebugOverlaySection _ultimatumDebugOverlaySection;
        private readonly Debug.Sections.PerformanceDebugOverlaySection _performanceDebugOverlaySection;

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
            _debugTelemetrySource = new PluginDebugTelemetrySource(_plugin);
            _overlayContext = new Debug.DebugOverlayRenderContext(
                _plugin,
                _altarService,
                _areaService,
                _weightCalculator,
                _deferredTextQueue,
                _deferredFrameQueue,
                _debugTelemetrySource);
            _clickingDebugOverlaySection = new Debug.Sections.ClickingDebugOverlaySection(_overlayContext);
            _labelDebugOverlaySection = new Debug.Sections.LabelDebugOverlaySection(_overlayContext);
            _ultimatumDebugOverlaySection = new Debug.Sections.UltimatumDebugOverlaySection(_overlayContext);
            _performanceDebugOverlaySection = new Debug.Sections.PerformanceDebugOverlaySection(_overlayContext);
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

            PerformanceMetricsSnapshot performanceSnapshot = performanceMonitor.GetDebugSnapshot();

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

            void RenderSectionIfEnabledWithPosition(bool enabled, Func<int, int, int, (int NextX, int NextY)> renderSection)
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

                (xPos, yPos) = renderSection(xPos, yPos, lineHeight);
                currentColumn = ResolveDebugColumnFromX(xPos, baseX, columnShiftPx, maxColumns);
            }

            RenderSectionIfEnabled(settings.DebugShowStatus, (x, y, h) => RenderPluginStatusDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowGameState, (x, y, h) => RenderGameStateDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowPerformance, (x, y, h) => _performanceDebugOverlaySection.RenderPerformanceDebug(x, y, h, performanceSnapshot));
            RenderSectionIfEnabled(settings.DebugShowClickFrequencyTarget, (x, y, h) => _performanceDebugOverlaySection.RenderClickFrequencyTargetDebug(x, y, h, performanceSnapshot));
            RenderSectionIfEnabled(settings.DebugShowAltarDetection, (x, y, h) => RenderAltarDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowAltarService, (x, y, h) => RenderAltarServiceDebug(x, y, h));
            RenderSectionIfEnabledWithPosition(settings.DebugShowLabels, (x, y, h) =>
            {
                int localX = x;
                int nextY = _labelDebugOverlaySection.RenderLabelsDebug(ref localX, y, h);
                return (localX, nextY);
            });
            RenderSectionIfEnabledWithPosition(settings.DebugShowInventoryPickup, (x, y, h) =>
            {
                int localX = x;
                int nextY = _labelDebugOverlaySection.RenderInventoryPickupDebug(ref localX, y, h);
                return (localX, nextY);
            });
            RenderSectionIfEnabled(settings.DebugShowHoveredItemMetadata, (x, y, h) => RenderHoveredItemMetadataDebug(x, y, h));
            RenderSectionIfEnabled(settings.DebugShowPathfinding, (x, y, h) => RenderPathfindingDebug(x, y, h));
            RenderSectionIfEnabledWithPosition(settings.DebugShowUltimatum, (x, y, h) =>
            {
                int localX = x;
                int nextY = _ultimatumDebugOverlaySection.RenderUltimatumDebug(ref localX, y, h);
                return (localX, nextY);
            });
            RenderSectionIfEnabledWithPosition(settings.DebugShowClicking, (x, y, h) =>
            {
                int localX = x;
                int nextY = _clickingDebugOverlaySection.RenderClickingDebug(ref localX, y, h);
                return (localX, nextY);
            });
            RenderSectionIfEnabledWithPosition(settings.DebugShowRuntimeDebugLogOverlay, (x, y, h) =>
            {
                int localX = x;
                int nextY = _clickingDebugOverlaySection.RenderRuntimeDebugLogOverlay(ref localX, y, h);
                return (localX, nextY);
            });
            RenderSectionIfEnabled(settings.DebugShowRecentErrors, (x, y, h) => RenderErrorsDebug(x, y, h));
        }

        private static int ResolveDebugColumnFromX(int xPos, int baseX, int columnShiftPx, int maxColumns)
        {
            if (columnShiftPx <= 0 || maxColumns <= 0)
                return 0;

            int raw = (xPos - baseX) / columnShiftPx;
            return Math.Clamp(raw, 0, maxColumns - 1);
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

        private int RenderDebugTrailBlock(ref int xPos, int yPos, int lineHeight, IReadOnlyList<string> trail, int maxRows, int wrapWidth)
        {
            if (trail == null || trail.Count == 0 || lineHeight <= 0)
                return yPos;

            if (!EnsureDebugLineCapacity(ref xPos, ref yPos, lineHeight))
                return yPos;

            _deferredTextQueue.Enqueue("Recent Stages:", new Vector2(xPos, yPos), Color.LightBlue, 13);
            yPos += lineHeight;

            int rowsToRender = Math.Min(Math.Max(1, maxRows), trail.Count);
            int start = Math.Max(0, trail.Count - rowsToRender);
            for (int i = start; i < trail.Count; i++)
            {
                yPos = EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"  {trail[i]}", Color.LightGray, 12, wrapWidth);
            }

            return yPos;
        }

        protected int EnqueueWrappedDebugLine(
            ref int xPos,
            int yPos,
            int lineHeight,
            string text,
            Color color,
            int fontSize,
            int maxCharsPerLine = 72)
        {
            if (lineHeight <= 0)
                return yPos;

            if (string.IsNullOrEmpty(text))
            {
                if (!EnsureDebugLineCapacity(ref xPos, ref yPos, lineHeight))
                    return yPos;

                _deferredTextQueue.Enqueue(string.Empty, new Vector2(xPos, yPos), color, fontSize);
                return yPos + lineHeight;
            }

            int safeWrap = Math.Max(20, maxCharsPerLine);
            foreach (string wrappedLine in WrapTextForDebug(text, safeWrap))
            {
                if (!EnsureDebugLineCapacity(ref xPos, ref yPos, lineHeight))
                    break;

                _deferredTextQueue.Enqueue(wrappedLine, new Vector2(xPos, yPos), color, fontSize);
                yPos += lineHeight;
            }

            return yPos;
        }

        private static bool EnsureDebugLineCapacity(ref int xPos, ref int yPos, int lineHeight)
        {
            if (lineHeight <= 0)
                return false;

            int usedLines = Math.Max(0, (yPos - DetailedDebugStartY) / lineHeight);
            if (usedLines < DetailedDebugLinesPerColumn)
                return true;

            int currentColumn = ResolveDebugColumnFromX(xPos, DetailedDebugBaseX, DetailedDebugColumnShiftPx, DetailedDebugMaxColumns);
            if (currentColumn >= DetailedDebugMaxColumns - 1)
                return false;

            int nextColumn = currentColumn + 1;
            xPos = DetailedDebugBaseX + (nextColumn * DetailedDebugColumnShiftPx);
            yPos = DetailedDebugStartY;
            return true;
        }

        private static List<string> WrapTextForDebug(string text, int maxCharsPerLine)
        {
            var lines = new List<string>(8);
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            int safeWrap = Math.Max(20, maxCharsPerLine);
            int leadingSpaces = 0;
            while (leadingSpaces < text.Length && text[leadingSpaces] == ' ')
            {
                leadingSpaces++;
            }

            string indentation = new(' ', leadingSpaces);
            string content = text.Substring(leadingSpaces);
            int contentLength = content.Length;
            int startIndex = 0;

            while (startIndex < contentLength)
            {
                int endIndex = Math.Min(startIndex + safeWrap, contentLength);
                if (endIndex < contentLength)
                {
                    string segment = content.Substring(startIndex, endIndex - startIndex);
                    int lastSpaceOffset = segment.LastIndexOf(' ');
                    if (lastSpaceOffset > 0)
                        endIndex = startIndex + lastSpaceOffset;
                }

                string line = content.Substring(startIndex, endIndex - startIndex).TrimEnd();
                lines.Add(indentation + line);

                startIndex = endIndex;
                if (startIndex < contentLength && content[startIndex] == ' ')
                    startIndex++;
            }

            return lines;
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

            if (_plugin is not ClickIt clickIt)
            {
                _deferredTextQueue.Enqueue("Pathfinding service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            DebugTelemetrySnapshot telemetry = clickIt.State.GetDebugTelemetrySnapshot();
            if (!telemetry.Pathfinding.ServiceAvailable)
            {
                _deferredTextQueue.Enqueue("Pathfinding service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            if (clickIt.State.TryGetDebugTelemetryFreezeState(out long remainingMs, out string freezeReason))
            {
                string freezeSummary = string.IsNullOrWhiteSpace(freezeReason)
                    ? $"Telemetry Hold Active: {remainingMs}ms remaining"
                    : $"Telemetry Hold Active: {remainingMs}ms remaining | {freezeReason}";
                yPos = RenderWrappedText(freezeSummary, new Vector2(xPos, yPos), Color.Orange, 14, lineHeight, 46);
            }

            var snap = telemetry.Pathfinding.Pathfinding;
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

            _deferredTextQueue.Enqueue(
                $"Goal Mode: {(snap.LastGoalResolutionUsedFallback ? "Fallback" : "Direct")}",
                new Vector2(xPos, yPos),
                snap.LastGoalResolutionUsedFallback ? Color.Yellow : Color.LightGreen,
                14);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue(
                $"Grid Start=({snap.LastStart.X},{snap.LastStart.Y}) Req=({snap.LastRequestedGoal.X},{snap.LastRequestedGoal.Y}) Res=({snap.LastResolvedGoal.X},{snap.LastResolvedGoal.Y})",
                new Vector2(xPos, yPos),
                Color.White,
                14);
            yPos += lineHeight;

            if (!string.IsNullOrWhiteSpace(snap.LastGoalResolutionNote))
            {
                yPos = RenderWrappedText(
                    $"Goal Note: {snap.LastGoalResolutionNote}",
                    new Vector2(xPos, yPos),
                    Color.Yellow,
                    14,
                    lineHeight,
                    46);
            }

            string targetPath = string.IsNullOrWhiteSpace(snap.LastTargetPath) ? "<none>" : snap.LastTargetPath;
            yPos = RenderWrappedText($"Target Path: {targetPath}", new Vector2(xPos, yPos), Color.LightBlue, 14, lineHeight, 46);

            if (!string.IsNullOrWhiteSpace(snap.LastFailureReason))
            {
                yPos = RenderWrappedText($"Failure: {snap.LastFailureReason}", new Vector2(xPos, yPos), Color.OrangeRed, 14, lineHeight, 46);
            }

            var movement = telemetry.Pathfinding.OffscreenMovement;
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

            IReadOnlyList<string> trail = telemetry.Pathfinding.OffscreenMovementTrail;
            yPos = RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 6, wrapWidth: 52);

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
