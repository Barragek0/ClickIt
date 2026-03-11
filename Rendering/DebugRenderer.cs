using ExileCore;
using ExileCore.Shared.Cache;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using ClickIt.Utils;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using System.Reflection;
using ClickIt.Services;

#nullable enable

namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {

        private readonly BaseSettingsPlugin<ClickItSettings> _plugin;
        private readonly AltarService? _altarService;
        private readonly AreaService? _areaService;
        private readonly WeightCalculator? _weightCalculator;
        private readonly DeferredTextQueue _deferredTextQueue;
        private readonly DeferredFrameQueue _deferredFrameQueue;
        private static readonly PropertyInfo? CachedLabelsPropertyInfo = typeof(ClickIt).GetProperty("CachedLabels", BindingFlags.NonPublic | BindingFlags.Instance);

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
                _deferredFrameQueue.Enqueue(_areaService.FullScreenRectangle, Color.Green, 1);
                _deferredFrameQueue.Enqueue(_areaService.HealthAndFlaskRectangle, Color.Orange, 1);
                _deferredFrameQueue.Enqueue(_areaService.ManaAndSkillsRectangle, Color.Cyan, 1);
                _deferredFrameQueue.Enqueue(_areaService.BuffsAndDebuffsRectangle, Color.Yellow, 1);
            }
        }

        public void RenderDetailedDebugInfo(ClickItSettings settings, PerformanceMonitor performanceMonitor)
        {
            if (settings == null || performanceMonitor == null) return;

            // avoid doing any work if nothing to render
            if (!settings.DebugShowStatus && !settings.DebugShowGameState && !settings.DebugShowPerformance &&
                !settings.DebugShowClickFrequencyTarget && !settings.DebugShowAltarDetection && !settings.DebugShowAltarService &&
                !settings.DebugShowLabels && !settings.DebugShowHoveredItemMetadata && !settings.DebugShowRecentErrors)
            {
                return;
            }

            int startY = 120;
            int lineHeight = 18;
            int columnWidth = 380;
            int col1X = 10;
            int yPos = startY;

            // Column 1: Plugin status, game state, performance, and click frequency
            if (settings.DebugShowStatus)
            {
                yPos = RenderPluginStatusDebug(col1X, yPos, lineHeight);
                yPos += lineHeight;
            }
            if (settings.DebugShowGameState)
            {
                yPos = RenderGameStateDebug(col1X, yPos, lineHeight);
                yPos += lineHeight;
            }
            if (settings.DebugShowPerformance)
            {
                yPos = RenderPerformanceDebug(col1X, yPos, lineHeight, performanceMonitor);
                yPos += lineHeight;
            }
            if (settings.DebugShowClickFrequencyTarget)
            {
                RenderClickFrequencyTargetDebug(col1X, yPos, lineHeight, performanceMonitor);
            }

            // Column 2: Altar detection, labels, altar service, and errors
            int col2X = col1X + columnWidth;
            yPos = startY;
            if (settings.DebugShowAltarDetection)
            {
                yPos = RenderAltarDebug(col2X, yPos, lineHeight);
                yPos += lineHeight;
            }
            if (settings.DebugShowAltarService)
            {
                yPos = RenderAltarServiceDebug(col2X, yPos, lineHeight);
                yPos += lineHeight;
            }
            if (settings.DebugShowLabels)
            {
                yPos = RenderLabelsDebug(col2X, yPos, lineHeight);
                yPos += lineHeight;
            }
            if (settings.DebugShowHoveredItemMetadata)
            {
                yPos = RenderHoveredItemMetadataDebug(col2X, yPos, lineHeight);
                yPos += lineHeight;
            }
            if (settings.DebugShowRecentErrors)
            {
                _ = RenderErrorsDebug(col2X, yPos, lineHeight);
                // No line break needed after the last category
            }
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

            // Show cached labels information
            if (_plugin is ClickIt clickItPlugin)
            {
                if (CachedLabelsPropertyInfo?.GetValue(clickItPlugin) is TimeCache<List<LabelOnGround>> cachedLabels)
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

            // Show player state
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

            // Count leading spaces to preserve indentation
            int leadingSpaces = 0;
            while (leadingSpaces < text.Length && text[leadingSpaces] == ' ')
            {
                leadingSpaces++;
            }
            string indentation = new(' ', leadingSpaces);
            string content = text.Substring(leadingSpaces);

            while (startIndex < content.Length)
            {
                int endIndex = Math.Min(startIndex + maxCharsPerLine, content.Length);
                if (endIndex < content.Length)
                {
                    int lastSpaceIndex = content.LastIndexOf(' ', endIndex - 1, Math.Min(maxCharsPerLine, endIndex - startIndex));
                    if (lastSpaceIndex > startIndex)
                    {
                        endIndex = lastSpaceIndex;
                    }
                }
                string line = content.Substring(startIndex, endIndex - startIndex).TrimEnd();
                // Add back the indentation
                _deferredTextQueue.Enqueue(indentation + line, new Vector2(position.X, currentY), color, fontSize);
                currentY += lineHeight;
                startIndex = endIndex;
                if (startIndex < content.Length && content[startIndex] == ' ')
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
