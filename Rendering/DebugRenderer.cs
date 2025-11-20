using ExileCore;
using ExileCore.Shared.Cache;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Graphics = ExileCore.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ClickIt.Components;
using ClickIt.Services;
using ClickIt.Utils;
using System.Linq;
using ClickIt.Properties;

#nullable enable

namespace ClickIt.Rendering
{
    public class DebugRenderer
    {
        private readonly BaseSettingsPlugin<ClickItSettings> _plugin;
        private readonly Graphics _graphics;
        private readonly AltarService? _altarService;
        private readonly AreaService? _areaService;
        private readonly WeightCalculator? _weightCalculator;
        private readonly Utils.DeferredTextQueue _deferredTextQueue;

        public DebugRenderer(BaseSettingsPlugin<ClickItSettings> plugin, Graphics graphics, ClickItSettings settings, AltarService? altarService = null, AreaService? areaService = null, WeightCalculator? weightCalculator = null, Utils.DeferredTextQueue? deferredTextQueue = null)
        {
            _plugin = plugin;
            _graphics = graphics;
            _altarService = altarService;
            _areaService = areaService;
            _weightCalculator = weightCalculator;
            _deferredTextQueue = deferredTextQueue ?? new Utils.DeferredTextQueue();
        }

        public void RenderDebugFrames(ClickItSettings settings)
        {
            if (_areaService == null) return;

            if (settings.DebugShowFrames)
            {
                _graphics.DrawFrame(_areaService.FullScreenRectangle, Color.Green, 1);
                _graphics.DrawFrame(_areaService.HealthAndFlaskRectangle, Color.Orange, 1);
                _graphics.DrawFrame(_areaService.ManaAndSkillsRectangle, Color.Cyan, 1);
                _graphics.DrawFrame(_areaService.BuffsAndDebuffsRectangle, Color.Yellow, 1);
            }
        }

        public void RenderDetailedDebugInfo(ClickItSettings settings, Utils.PerformanceMonitor performanceMonitor)
        {
            if (settings == null || performanceMonitor == null) return;

            int startY = 120;
            int lineHeight = 18;
            int columnWidth = 300;
            int col1X = 10;
            int yPos = startY;

            // Column 1: Plugin status, performance, and game state
            if (settings.DebugShowStatus)
            {
                yPos = RenderPluginStatusDebug(col1X, yPos, lineHeight);
            }
            if (settings.DebugShowPerformance)
            {
                yPos = RenderPerformanceDebug(col1X, yPos, lineHeight, performanceMonitor);
            }
            if (settings.DebugShowGameState)
            {
                yPos = RenderGameStateDebug(col1X, yPos, lineHeight);
            }
            if (settings.DebugShowAltarService)
            {
                RenderAltarServiceDebug(col1X, yPos, lineHeight);
            }

            // Column 2: Altar detection, labels, and errors
            int col2X = col1X + columnWidth;
            yPos = startY;
            if (settings.DebugShowAltarDetection)
            {
                yPos = RenderAltarDebug(col2X, yPos, lineHeight);
            }
            if (settings.DebugShowLabels)
            {
                yPos = RenderLabelsDebug(col2X, yPos, lineHeight);
            }
            if (settings.DebugShowRecentErrors)
            {
                RenderErrorsDebug(col2X, yPos, lineHeight);
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

        [Obsolete]
        public int RenderInputDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue($"--- Input State ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            bool hotkeyPressed = Input.GetKeyState(_plugin.Settings.ClickLabelKey.Value);
            Color hotkeyColor = hotkeyPressed ? Color.LightGreen : Color.Gray;
            _deferredTextQueue.Enqueue($"Hotkey ({_plugin.Settings.ClickLabelKey.Value}): {hotkeyPressed}", new Vector2(xPos, yPos), hotkeyColor, 16);
            yPos += lineHeight;

            // Show input blocking state
            if (_plugin is ClickIt clickItPlugin)
            {
                // Reflection used for debug display only; safe because it is read-only and only in debug UI.
                var inputBlockedField = typeof(ClickIt).GetField("isInputCurrentlyBlocked",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (inputBlockedField?.GetValue(clickItPlugin) is bool isBlocked)
                {
                    Color blockedColor = isBlocked ? Color.Red : Color.LightGreen;
                    _deferredTextQueue.Enqueue($"Input Blocked: {isBlocked}", new Vector2(xPos, yPos), blockedColor, 16);
                    yPos += lineHeight;
                }

                var lastHotkeyTimerField = typeof(ClickIt).GetField("lastHotkeyReleaseTimer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (lastHotkeyTimerField?.GetValue(clickItPlugin) is Stopwatch hotkeyTimer)
                {
                    _deferredTextQueue.Enqueue($"Hotkey Release Timer: {hotkeyTimer.ElapsedMilliseconds} ms", new Vector2(xPos, yPos), Color.White, 16);
                    yPos += lineHeight;
                }

                _deferredTextQueue.Enqueue($"Input State Unavailable", new Vector2(xPos, yPos), Color.Gray, 16);
                yPos += lineHeight;
            }

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
                var cachedLabelsField = typeof(ClickIt).GetProperty("CachedLabels",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (cachedLabelsField?.GetValue(clickItPlugin) is TimeCache<List<LabelOnGround>> cachedLabels)
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
            var altarComps = _altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
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

        private decimal GetTopUpsideWeight(AltarWeights weights, int index)
        {
            var arr = weights.GetTopUpsideWeights();
            return (index >= 0 && index < arr.Length) ? arr[index] : 0;
        }

        private decimal GetTopDownsideWeight(AltarWeights weights, int index)
        {
            var arr = weights.GetTopDownsideWeights();
            return (index >= 0 && index < arr.Length) ? arr[index] : 0;
        }

        private decimal GetBottomUpsideWeight(AltarWeights weights, int index)
        {
            var arr = weights.GetBottomUpsideWeights();
            return (index >= 0 && index < arr.Length) ? arr[index] : 0;
        }

        private decimal GetBottomDownsideWeight(AltarWeights weights, int index)
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
            string indentation = new string(' ', leadingSpaces);
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
        public int RenderPerformanceDebug(int xPos, int yPos, int lineHeight, Utils.PerformanceMonitor performanceMonitor)
        {
            _deferredTextQueue.Enqueue($"--- Performance ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            // FPS Display
            double fps = performanceMonitor.CurrentFPS;
            Color fpsColor = fps >= 144 ? Color.LawnGreen : (fps >= 60 ? Color.Yellow : Color.Red);
            _deferredTextQueue.Enqueue($"FPS: {fps:F1}", new Vector2(xPos, yPos), fpsColor, 16);
            yPos += lineHeight;

            // Render time
            var renderTimings = performanceMonitor.RenderTimings;
            if (renderTimings != null && renderTimings.Count > 0)
            {
                // Create snapshot to avoid enumeration modification errors
                var renderTimingsSnapshot = renderTimings.ToArray();
                long lastRenderTime = renderTimingsSnapshot[renderTimingsSnapshot.Length - 1];
                double avgRenderTime = renderTimingsSnapshot.Average();
                double maxRenderTime = renderTimingsSnapshot.Max();

                Color renderColor;
                if (avgRenderTime <= 6.94)
                    renderColor = Color.LawnGreen;
                else if (avgRenderTime <= 16.67)
                    renderColor = Color.Yellow;
                else
                    renderColor = Color.Red;

                _deferredTextQueue.Enqueue($"Render: {lastRenderTime} ms (avg: {avgRenderTime:F2}, max: {maxRenderTime})", new Vector2(xPos, yPos), renderColor, 16);
                yPos += lineHeight;
            }

            var process = Process.GetCurrentProcess();
            long memoryUsage = process.WorkingSet64 / 1024 / 1024;
            Color memoryColor = Color.Yellow;
            _deferredTextQueue.Enqueue($"Memory Usage: {memoryUsage} MB", new Vector2(xPos, yPos), memoryColor, 16);
            yPos += lineHeight;

            // Display coroutine timing averages
            double altarCurrent = performanceMonitor.GetLastTiming("altar");
            double altarAvg = performanceMonitor.GetAverageTiming("altar");
            double altarMax = performanceMonitor.GetMaxTiming("altar");
            Color altarColor = altarCurrent >= 10 ? Color.Red : (altarCurrent >= 5 ? Color.Yellow : Color.LawnGreen);
            _deferredTextQueue.Enqueue($"Altar Coroutine: {altarCurrent:F0} ms (avg: {altarAvg:F1}, max: {altarMax:F0})", new Vector2(xPos, yPos), altarColor, 16);
            yPos += lineHeight;

            double clickCurrent = performanceMonitor.GetLastTiming("click");
            double clickAvg = performanceMonitor.GetAverageTiming("click");
            double clickMax = performanceMonitor.GetMaxTiming("click");
            double clickTarget = performanceMonitor.GetClickTargetInterval();
            double clickYellowThreshold = clickTarget * 0.75; // Yellow when within 25% of target (75% of target)
            double clickRedThreshold = clickTarget; // Above target
            Color clickColor = clickCurrent >= clickRedThreshold ? Color.Red : (clickCurrent >= clickYellowThreshold ? Color.Yellow : Color.LawnGreen);
            _deferredTextQueue.Enqueue($"Click Coroutine: {clickCurrent:F0} ms (avg: {clickAvg:F1}, max: {clickMax:F0})", new Vector2(xPos, yPos), clickColor, 16);
            yPos += lineHeight;

            double delveFlareCurrent = performanceMonitor.GetLastTiming("delveFlare");
            double delveFlareAvg = performanceMonitor.GetAverageTiming("delveFlare");
            double delveFlareMax = performanceMonitor.GetMaxTiming("delveFlare");
            Color delveFlareColor = delveFlareCurrent >= 10 ? Color.Red : (delveFlareCurrent >= 5 ? Color.Yellow : Color.LawnGreen);
            _deferredTextQueue.Enqueue($"Delve Flare Coroutine: {delveFlareCurrent:F0} ms (avg: {delveFlareAvg:F1}, max: {delveFlareMax:F0})", new Vector2(xPos, yPos), delveFlareColor, 16);
            yPos += lineHeight;

            double shrineCurrent = performanceMonitor.GetLastTiming("shrine");
            double shrineAvg = performanceMonitor.GetAverageTiming("shrine");
            double shrineMax = performanceMonitor.GetMaxTiming("shrine");
            Color shrineColor = shrineCurrent >= 10 ? Color.Red : (shrineCurrent >= 5 ? Color.Yellow : Color.LawnGreen);
            _deferredTextQueue.Enqueue($"Shrine Coroutine: {shrineCurrent:F0} ms (avg: {shrineAvg:F1}, max: {shrineMax:F0})", new Vector2(xPos, yPos), shrineColor, 16);
            yPos += lineHeight;

            double clickIntervalAvg = performanceMonitor.GetAverageClickInterval();
            double avgClickTime = performanceMonitor.GetAverageTiming("click");
            double actualTarget = clickTarget - avgClickTime;
            Color intervalColor;
            double deviationPercent = Math.Abs(clickIntervalAvg - actualTarget) / Math.Max(actualTarget, 1);
            if (deviationPercent <= 0.15)
                intervalColor = Color.LawnGreen;  // Within 15% of target
            else if (deviationPercent <= 0.30)
                intervalColor = Color.Yellow;     // 15-30% deviation
            else
                intervalColor = Color.Red;        // More than 30% deviation
            _deferredTextQueue.Enqueue($"Click Interval: {clickIntervalAvg:F0} ms (target: {actualTarget:F0})", new Vector2(xPos, yPos), intervalColor, 16);
            yPos += lineHeight;

            return yPos;
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
