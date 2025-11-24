using ExileCore;
using ExileCore.Shared.Cache;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using Color = SharpDX.Color;
using System.Diagnostics;
using ClickIt.Components;
using System.Reflection;
using ClickIt.Services;
using ClickIt.Utils;

#nullable enable

namespace ClickIt.Rendering
{
    public class DebugRenderer
    {
        private const double FPS_HIGH_THRESHOLD = 144;
        private const double FPS_MEDIUM_THRESHOLD = 60;
        private const double RENDER_TIME_LOW_THRESHOLD = 6.94;
        private const double RENDER_TIME_MEDIUM_THRESHOLD = 16.67;
        private const double COROUTINE_HIGH_THRESHOLD = 50;
        private const double COROUTINE_MEDIUM_THRESHOLD = 25;
        private const double TARGET_DEVIATION_LOW = 0.05;
        private const double TARGET_DEVIATION_MEDIUM = 0.10;

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
                !settings.DebugShowLabels && !settings.DebugShowRecentErrors)
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
        public int RenderAltarDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue("--- Altar Detection ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            var altarComps = _altarService?.GetAltarComponents() ?? [];
            Color altarCountColor = altarComps.Count > 0 ? Color.LightGreen : Color.Gray;
            _deferredTextQueue.Enqueue($"Altar Components: {altarComps.Count}", new Vector2(xPos, yPos), altarCountColor, 16);
            yPos += lineHeight;
            if (altarComps.Count > 0)
            {
                _deferredTextQueue.Enqueue("Active Altars:", new Vector2(xPos, yPos), Color.Cyan, 16);
                yPos += lineHeight;
                for (int i = 0; i < Math.Min(altarComps.Count, 2); i++)
                {
                    var altar = altarComps[i];
                    yPos = RenderSingleAltarDebug(xPos, yPos, lineHeight, altar, i + 1);
                }
            }
            return yPos + lineHeight;
        }
        public int RenderSingleAltarDebug(int xPos, int yPos, int lineHeight, PrimaryAltarComponent altar, int altarNumber)
        {
            _deferredTextQueue.Enqueue($"Altar {altarNumber}:", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            // Calculate weights if WeightCalculator is available
            AltarWeights? weights = null;
            if (_weightCalculator != null)
            {
                weights = _weightCalculator.CalculateAltarWeights(altar);
            }

            // Top Mods: Show both upsides and downsides
            if (altar?.TopMods != null)
            {
                yPos = RenderTopModsSection(xPos, yPos, lineHeight, altar.TopMods, weights);
            }

            // Bottom Mods: Show both upsides and downsides
            if (altar?.BottomMods != null)
            {
                yPos = RenderBottomModsSection(xPos, yPos, lineHeight, altar.BottomMods, weights);
            }

            return yPos;
        }

        private int RenderTopModsSection(int xPos, int yPos, int lineHeight, SecondaryAltarComponent topMods, AltarWeights? weights)
        {
            int topUpsidesCount = topMods.Upsides?.Count ?? 0;
            int topDownsidesCount = topMods.Downsides?.Count ?? 0;
            _deferredTextQueue.Enqueue($"  Top Mods (Upsides: {topUpsidesCount}, Downsides: {topDownsidesCount}):", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            // Show top mod upsides
            for (int i = 0; i < topUpsidesCount && i < 8; i++)
            {
                string mod = topMods.Upsides?[i] ?? "";
                if (!string.IsNullOrEmpty(mod))
                {
                    Color color = Color.LightBlue;
                    string prefix = $"{i + 1}";
                    string weightText = weights.HasValue ? $" ({GetTopUpsideWeight(weights.Value, i)})" : "";
                    yPos = RenderWrappedText($"    {prefix}: {mod}{weightText}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
                }
            }

            // Show top mod downsides
            for (int i = 0; i < topDownsidesCount && i < 8; i++)
            {
                string mod = topMods.Downsides?[i] ?? "";
                if (!string.IsNullOrEmpty(mod))
                {
                    Color color = Color.LightCoral;
                    string prefix = $"{i + 1}";
                    string weightText = weights.HasValue ? $" ({GetTopDownsideWeight(weights.Value, i)})" : "";
                    yPos = RenderWrappedText($"    {prefix}: {mod}{weightText}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
                }
            }

            return yPos;
        }

        private int RenderBottomModsSection(int xPos, int yPos, int lineHeight, SecondaryAltarComponent bottomMods, AltarWeights? weights)
        {
            int bottomUpsidesCount = bottomMods.Upsides?.Count ?? 0;
            int bottomDownsidesCount = bottomMods.Downsides?.Count ?? 0;
            _deferredTextQueue.Enqueue($"  Bottom Mods (Upsides: {bottomUpsidesCount}, Downsides: {bottomDownsidesCount}):", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            // Show bottom mod upsides
            for (int i = 0; i < bottomUpsidesCount && i < 8; i++)
            {
                string mod = bottomMods.Upsides?[i] ?? "";
                if (!string.IsNullOrEmpty(mod))
                {
                    Color color = Color.LightBlue;
                    string prefix = $"{i + 1}";
                    string weightText = weights.HasValue ? $" ({GetBottomUpsideWeight(weights.Value, i)})" : "";
                    yPos = RenderWrappedText($"    {prefix}: {mod}{weightText}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
                }
            }

            // Show bottom mod downsides
            for (int i = 0; i < bottomDownsidesCount && i < 8; i++)
            {
                string mod = bottomMods.Downsides?[i] ?? "";
                if (!string.IsNullOrEmpty(mod))
                {
                    Color color = Color.LightCoral;
                    string prefix = $"{i + 1}";
                    string weightText = weights.HasValue ? $" ({GetBottomDownsideWeight(weights.Value, i)})" : "";
                    yPos = RenderWrappedText($"    {prefix}: {mod}{weightText}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
                }
            }

            return yPos;
        }

        private static decimal GetTopUpsideWeight(AltarWeights weights, int index)
        {
            var arr = weights.GetTopUpsideWeights();
            return (index >= 0 && index < arr.Length) ? arr[index] : 0;
        }

        private static decimal GetTopDownsideWeight(AltarWeights weights, int index)
        {
            var arr = weights.GetTopDownsideWeights();
            return (index >= 0 && index < arr.Length) ? arr[index] : 0;
        }

        private static decimal GetBottomUpsideWeight(AltarWeights weights, int index)
        {
            var arr = weights.GetBottomUpsideWeights();
            return (index >= 0 && index < arr.Length) ? arr[index] : 0;
        }

        private static decimal GetBottomDownsideWeight(AltarWeights weights, int index)
        {
            var arr = weights.GetBottomDownsideWeights();
            return (index >= 0 && index < arr.Length) ? arr[index] : 0;
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
        public int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue($"--- Altar Service ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            var debugInfo = _altarService?.DebugInfo;
            if (debugInfo != null)
            {
                _deferredTextQueue.Enqueue($"Last Scan Exarch: {debugInfo.LastScanExarchLabels}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _deferredTextQueue.Enqueue($"Last Scan Eater: {debugInfo.LastScanEaterLabels}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _deferredTextQueue.Enqueue($"Elements Found: {debugInfo.ElementsFound}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _deferredTextQueue.Enqueue($"Components Processed: {debugInfo.ComponentsProcessed}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _deferredTextQueue.Enqueue($"Components Added: {debugInfo.ComponentsAdded}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _deferredTextQueue.Enqueue($"Components Duplicated: {debugInfo.ComponentsDuplicated}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _deferredTextQueue.Enqueue($"Mods Matched: {debugInfo.ModsMatched}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _deferredTextQueue.Enqueue($"Mods Unmatched: {debugInfo.ModsUnmatched}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _deferredTextQueue.Enqueue($"Last Altar Type: {debugInfo.LastProcessedAltarType}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                if (!string.IsNullOrEmpty(debugInfo.LastError))
                {
                    _deferredTextQueue.Enqueue($"Last Error: {debugInfo.LastError}", new Vector2(xPos, yPos), Color.Red, 16);
                    yPos += lineHeight;
                }
                if (debugInfo.LastScanTime != DateTime.MinValue)
                {
                    _deferredTextQueue.Enqueue($"Last Scan: {debugInfo.LastScanTime:HH:mm:ss}", new Vector2(xPos, yPos), Color.White, 16);
                    yPos += lineHeight;
                }
            }
            else
            {
                _deferredTextQueue.Enqueue($"  Altar Service: NULL", new Vector2(xPos, yPos), Color.Red, 16);
                yPos += lineHeight;
            }

            return yPos;
        }
        public int RenderLabelsDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue($"Labels Debug:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var gameController = _plugin.GameController;
            var labelsCollection = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labelsCollection != null)
            {
                int totalLabels = labelsCollection.Count;
                _deferredTextQueue.Enqueue($"  Total Labels: {totalLabels}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                int validLabels = 0;
                foreach (var label in labelsCollection)
                {
                    if (label?.ItemOnGround?.Path != null)
                        validLabels++;
                }
                _deferredTextQueue.Enqueue($"  Valid Labels: {validLabels}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
            }
            else
            {
                _deferredTextQueue.Enqueue($"  Labels Collection: NULL", new Vector2(xPos, yPos), Color.Red, 16);
                yPos += lineHeight;
            }
            return yPos;
        }
        public int RenderPerformanceDebug(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            _deferredTextQueue.Enqueue($"--- Performance ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            yPos = RenderFps(xPos, yPos, lineHeight, performanceMonitor.CurrentFPS);
            yPos = RenderMemory(xPos, yPos, lineHeight);
            yPos = RenderRenderTime(xPos, yPos, lineHeight, performanceMonitor);
            yPos = RenderCoroutineTimings(xPos, yPos, lineHeight, performanceMonitor);

            return yPos;
        }

        private int RenderFps(int xPos, int yPos, int lineHeight, double fps)
        {
            Color fpsColor = fps >= FPS_HIGH_THRESHOLD ? Color.LawnGreen : (fps >= FPS_MEDIUM_THRESHOLD ? Color.Yellow : Color.Red);
            _deferredTextQueue.Enqueue($"FPS: {fps:F1}", new Vector2(xPos, yPos), fpsColor, 16);
            return yPos + lineHeight;
        }

        private int RenderMemory(int xPos, int yPos, int lineHeight)
        {
            var process = Process.GetCurrentProcess();
            long memoryUsage = process.WorkingSet64 / 1024 / 1024;
            _deferredTextQueue.Enqueue($"Memory Usage: {memoryUsage} MB", new Vector2(xPos, yPos), Color.Yellow, 16);
            return yPos + lineHeight;
        }

        private int RenderRenderTime(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            var renderTimings = performanceMonitor.RenderTimings;
            if (renderTimings == null || renderTimings.Count == 0)
                return yPos;

            // Compute last / avg / max in a single pass without creating intermediate arrays
            long lastRenderTime = 0;
            long sum = 0;
            long maxRenderTime = long.MinValue;
            int count = 0;
            foreach (var t in renderTimings)
            {
                lastRenderTime = t;
                sum += t;
                if (t > maxRenderTime) maxRenderTime = t;
                count++;
            }
            double avgRenderTime = count > 0 ? (double)sum / count : 0.0;

            Color renderColor = avgRenderTime <= RENDER_TIME_LOW_THRESHOLD ? Color.LawnGreen :
                               (avgRenderTime <= RENDER_TIME_MEDIUM_THRESHOLD ? Color.Yellow : Color.Red);

            _deferredTextQueue.Enqueue($"Render: {lastRenderTime} ms (avg: {avgRenderTime:F2}, max: {maxRenderTime})", new Vector2(xPos, yPos), renderColor, 16);
            return yPos + lineHeight;
        }

        private int RenderCoroutineTimings(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, "altar", "Altar Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, "click", "Click Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, "flare", "Flare Coroutine");
            yPos = RenderCoroutineTiming(xPos, yPos, lineHeight, performanceMonitor, "shrine", "Shrine Coroutine");
            return yPos;
        }

        private int RenderCoroutineTiming(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor, string timingType, string label)
        {
            double current = performanceMonitor.GetLastTiming(timingType);
            double avg = performanceMonitor.GetAverageTiming(timingType);
            double max = performanceMonitor.GetMaxTiming(timingType);
            Color color = current >= COROUTINE_HIGH_THRESHOLD ? Color.Red :
                         (current >= COROUTINE_MEDIUM_THRESHOLD ? Color.Yellow : Color.LawnGreen);
            _deferredTextQueue.Enqueue($"{label}: {current:F0} ms (avg: {avg:F1}, max: {max:F0})", new Vector2(xPos, yPos), color, 16);
            return yPos + lineHeight;
        }

        private void RenderClickFrequencyTargetDebug(int xPos, int yPos, int lineHeight, PerformanceMonitor performanceMonitor)
        {
            _deferredTextQueue.Enqueue($"--- Click Frequency Target ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            var pluginSettings = _plugin.Settings;
            bool lazyModeEnabled = pluginSettings.LazyMode.Value;
            int lazyModeTarget = pluginSettings.LazyModeClickLimiting.Value;
            bool lazyModeDisableKeyHeld = Input.GetKeyState(pluginSettings.LazyModeDisableKey.Value);

            // Check if there are restricted items on screen
            bool hasRestrictedItems = false;
            if (_plugin is ClickIt clickItPlugin)
            {
                var gameController = _plugin.GameController;
                // Only materialize the label list if the LabelFilterService is present (costly operation)
                var labelFilterService = clickItPlugin.LabelFilterService;
                if (labelFilterService != null)
                {

                    var allLabels = (System.Collections.Generic.IReadOnlyList<LabelOnGround>?)gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
                    hasRestrictedItems = labelFilterService.HasLazyModeRestrictedItemsOnScreen(allLabels);
                }
                else
                {
                    hasRestrictedItems = false;
                }
            }

            bool poeActive = _plugin.GameController?.Window?.IsForeground() == true;
            bool lazyModeActive = lazyModeEnabled && !lazyModeDisableKeyHeld && !hasRestrictedItems && poeActive;

            double clickTarget = lazyModeActive ? lazyModeTarget : performanceMonitor.GetClickTargetInterval();
            double avgClickTime = performanceMonitor.GetAverageSuccessfulClickTiming();
            double effectiveDelay = clickTarget - avgClickTime;
            double expectedTotal = effectiveDelay + avgClickTime;
            double targetDeviation = (expectedTotal - clickTarget) / clickTarget;
            string targetStatus = targetDeviation <= TARGET_DEVIATION_MEDIUM ? "meeting target" : "not meeting target";
            Color targetLineColor = targetDeviation <= TARGET_DEVIATION_LOW ? Color.LawnGreen :
                                  (targetDeviation <= TARGET_DEVIATION_MEDIUM ? Color.Yellow : Color.Red);

            string delayStr = $"{effectiveDelay:F0}";
            string procStr = $"{avgClickTime:F0}";
            string targetStr = $"{expectedTotal:F0}";
            string settingStr = $"{clickTarget:F0}";
            int maxLen = Math.Max(Math.Max(delayStr.Length, procStr.Length), Math.Max(targetStr.Length, settingStr.Length));

            Color procColor = avgClickTime > clickTarget ? Color.Red :
                              avgClickTime >= clickTarget * 0.75 ? Color.Yellow : Color.LawnGreen;
            _deferredTextQueue.Enqueue($"Target:      {settingStr.PadLeft(maxLen)} ms {(lazyModeActive ? "(Lazy)" : "")}", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Click Delay: {delayStr.PadLeft(maxLen)} ms +", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Processing:  {procStr.PadLeft(maxLen)} ms =", new Vector2(xPos, yPos), procColor, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Total:       {targetStr.PadLeft(maxLen)} ms ({targetStatus})", new Vector2(xPos, yPos), targetLineColor, 16);
        }

        public int RenderErrorsDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue($"--- Recent Errors ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            // Access recent errors from the main plugin class
            if (_plugin is ClickIt clickItPlugin)
            {
                var recentErrors = clickItPlugin.RecentErrors;
                if (recentErrors.Count > 0)
                {
                    _deferredTextQueue.Enqueue($"Error Count: {recentErrors.Count}", new Vector2(xPos, yPos), Color.White, 16);
                    yPos += lineHeight;

                    for (int i = Math.Max(0, recentErrors.Count - 3); i < recentErrors.Count; i++)
                    {
                        string error = recentErrors[i];
                        yPos = RenderWrappedText($"  {error}", new Vector2(xPos, yPos), Color.Red, 14, lineHeight, 50);
                    }
                }
                else
                {
                    _deferredTextQueue.Enqueue($"No Recent Errors", new Vector2(xPos, yPos), Color.LightGreen, 16);
                    yPos += lineHeight;
                }
            }

            return yPos;
        }
    }
}
