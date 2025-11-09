using ExileCore;
using ExileCore.Shared.Cache;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ClickIt.Components;
using ClickIt.Services;
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

        public DebugRenderer(BaseSettingsPlugin<ClickItSettings> plugin, Graphics graphics, ClickItSettings settings, AltarService? altarService = null)
        {
            _plugin = plugin;
            _graphics = graphics;
            _altarService = altarService;
        }
        public int RenderPluginStatusDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"--- ClickIt Status ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var gameController = _plugin.GameController;
            bool inGame = gameController?.InGame == true;
            Color gameColor = inGame ? Color.LightGreen : Color.Red;
            _graphics.DrawText($"In Game: {inGame}", new Vector2(xPos, yPos), gameColor, 16);
            yPos += lineHeight;
            bool entityListValid = gameController?.EntityListWrapper?.ValidEntitiesByType != null;
            Color entityColor = entityListValid ? Color.LightGreen : Color.Red;
            _graphics.DrawText($"Entity List Valid: {entityListValid}", new Vector2(xPos, yPos), entityColor, 16);
            yPos += lineHeight;
            bool playerValid = gameController?.Player != null;
            Color playerColor = playerValid ? Color.LightGreen : Color.Red;
            _graphics.DrawText($"Player Valid: {playerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;

            // Show plugin settings state
            _graphics.DrawText($"Plugin Enabled: {_plugin.Settings.Enable.Value}", new Vector2(xPos, yPos),
                _plugin.Settings.Enable.Value ? Color.LightGreen : Color.Red, 16);
            yPos += lineHeight;
            _graphics.DrawText($"Debug Mode: {_plugin.Settings.DebugMode.Value}", new Vector2(xPos, yPos),
                _plugin.Settings.DebugMode.Value ? Color.Yellow : Color.Gray, 16);
            yPos += lineHeight;
            _graphics.DrawText($"Click Items: {_plugin.Settings.ClickItems.Value}", new Vector2(xPos, yPos),
                _plugin.Settings.ClickItems.Value ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;
            _graphics.DrawText($"Click Exarch: {_plugin.Settings.ClickExarchAltars.Value}", new Vector2(xPos, yPos),
                _plugin.Settings.ClickExarchAltars.Value ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;
            _graphics.DrawText($"Click Eater: {_plugin.Settings.ClickEaterAltars.Value}", new Vector2(xPos, yPos),
                _plugin.Settings.ClickEaterAltars.Value ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            return yPos;
        }

        [Obsolete]
        public int RenderInputDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"--- Input State ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            bool hotkeyPressed = Input.GetKeyState(_plugin.Settings.ClickLabelKey);
            Color hotkeyColor = hotkeyPressed ? Color.LightGreen : Color.Gray;
            _graphics.DrawText($"Hotkey ({_plugin.Settings.ClickLabelKey.Value}): {hotkeyPressed}", new Vector2(xPos, yPos), hotkeyColor, 16);
            yPos += lineHeight;

            // Show input blocking state
            if (_plugin is ClickIt clickItPlugin)
            {
                try
                {
                    var inputBlockedField = typeof(ClickIt).GetField("isInputCurrentlyBlocked",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (inputBlockedField?.GetValue(clickItPlugin) is bool isBlocked)
                    {
                        Color blockedColor = isBlocked ? Color.Red : Color.LightGreen;
                        _graphics.DrawText($"Input Blocked: {isBlocked}", new Vector2(xPos, yPos), blockedColor, 16);
                        yPos += lineHeight;
                    }

                    var lastHotkeyTimerField = typeof(ClickIt).GetField("lastHotkeyReleaseTimer",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (lastHotkeyTimerField?.GetValue(clickItPlugin) is Stopwatch hotkeyTimer)
                    {
                        _graphics.DrawText($"Hotkey Release Timer: {hotkeyTimer.ElapsedMilliseconds} ms", new Vector2(xPos, yPos), Color.White, 16);
                        yPos += lineHeight;
                    }
                }
                catch
                {
                    _graphics.DrawText($"Input State Unavailable", new Vector2(xPos, yPos), Color.Gray, 16);
                    yPos += lineHeight;
                }
            }

            return yPos;
        }
        public int RenderGameStateDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"--- Game State ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var gameController = _plugin.GameController;
            var currentArea = gameController?.Area?.CurrentArea;
            string areaName = currentArea?.DisplayName ?? "Unknown";
            _graphics.DrawText($"Current Area: {areaName}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;

            bool hasItems = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count > 0;
            Color itemColor = hasItems ? Color.LightGreen : Color.Gray;
            int itemCount = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count ?? 0;
            _graphics.DrawText($"Items on Ground: {itemCount}", new Vector2(xPos, yPos), itemColor, 16);
            yPos += lineHeight;

            // Show cached labels information
            if (_plugin is ClickIt clickItPlugin)
            {
                try
                {
                    var cachedLabelsField = typeof(ClickIt).GetProperty("CachedLabels",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (cachedLabelsField?.GetValue(clickItPlugin) is TimeCache<List<LabelOnGround>> cachedLabels)
                    {
                        var labels = cachedLabels.Value;
                        int cachedCount = labels?.Count ?? 0;
                        _graphics.DrawText($"Cached Labels: {cachedCount}", new Vector2(xPos, yPos), Color.White, 16);
                        yPos += lineHeight;
                    }
                }
                catch
                {
                    _graphics.DrawText($"Cache Info Unavailable", new Vector2(xPos, yPos), Color.Gray, 16);
                    yPos += lineHeight;
                }
            }

            // Show player state
            bool playerValid = gameController?.Player != null;
            Color playerColor = playerValid ? Color.LightGreen : Color.Red;
            _graphics.DrawText($"Player Valid: {playerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;

            if (playerValid && gameController?.Player != null)
            {
                var playerPos = gameController.Player.Pos;
                _graphics.DrawText($"Player Pos: ({playerPos.X:F1}, {playerPos.Y:F1})", new Vector2(xPos, yPos), Color.White, 14);
                yPos += lineHeight;
            }

            return yPos;
        }
        public int RenderAltarDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText("--- Altar Detection ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            var altarComps = _altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
            Color altarCountColor = altarComps.Count > 0 ? Color.LightGreen : Color.Gray;
            _graphics.DrawText($"Altar Components: {altarComps.Count}", new Vector2(xPos, yPos), altarCountColor, 16);
            yPos += lineHeight;
            if (altarComps.Count > 0)
            {
                _graphics.DrawText("Active Altars:", new Vector2(xPos, yPos), Color.Cyan, 16);
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
            _graphics.DrawText($"Altar {altarNumber}:", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            if (altar?.TopMods?.Upsides != null && altar.TopMods.Upsides.Count > 0)
            {
                _graphics.DrawText($"  Top Mods ({altar.TopMods.Upsides.Count}):", new Vector2(xPos, yPos), Color.White, 14);
                yPos += lineHeight;
                for (int i = 0; i < altar.TopMods.Upsides.Count && i < 3; i++)
                {
                    string mod = altar.TopMods.Upsides[i];
                    Color color = Color.LightBlue;
                    string prefix = $"Mod{i + 1}";
                    yPos = RenderWrappedText($"      {prefix}: {mod}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
                }
            }
            if (altar?.BottomMods?.Downsides != null && altar.BottomMods.Downsides.Count > 0)
            {
                _graphics.DrawText($"  Bottom Mods ({altar.BottomMods.Downsides.Count}):", new Vector2(xPos, yPos), Color.White, 14);
                yPos += lineHeight;
                for (int i = 0; i < altar.BottomMods.Downsides.Count && i < 3; i++)
                {
                    string mod = altar.BottomMods.Downsides[i];
                    Color color = Color.LightCoral;
                    string prefix = $"Mod{i + 1}";
                    yPos = RenderWrappedText($"      {prefix}: {mod}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
                }
            }
            return yPos;
        }
        public int RenderWrappedText(string text, Vector2 position, Color color, int fontSize, int lineHeight, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
                return (int)(position.Y + lineHeight);
            int currentY = (int)position.Y;
            int startIndex = 0;
            while (startIndex < text.Length)
            {
                int endIndex = Math.Min(startIndex + maxCharsPerLine, text.Length);
                if (endIndex < text.Length)
                {
                    int lastSpaceIndex = text.LastIndexOf(' ', endIndex - 1, Math.Min(maxCharsPerLine, endIndex - startIndex));
                    if (lastSpaceIndex > startIndex)
                    {
                        endIndex = lastSpaceIndex;
                    }
                }
                string line = text.Substring(startIndex, endIndex - startIndex).TrimStart();
                _graphics.DrawText(line, new Vector2(position.X, currentY), color, fontSize);
                currentY += lineHeight;
                startIndex = endIndex;
                if (startIndex < text.Length && text[startIndex] == ' ')
                {
                    startIndex++;
                }
            }
            return currentY;
        }
        public int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"--- Altar Service ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            var debugInfo = _altarService?.DebugInfo;
            if (debugInfo != null)
            {
                _graphics.DrawText($"Last Scan Exarch: {debugInfo.LastScanExarchLabels}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _graphics.DrawText($"Last Scan Eater: {debugInfo.LastScanEaterLabels}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _graphics.DrawText($"Elements Found: {debugInfo.ElementsFound}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _graphics.DrawText($"Components Processed: {debugInfo.ComponentsProcessed}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _graphics.DrawText($"Components Added: {debugInfo.ComponentsAdded}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _graphics.DrawText($"Components Duplicated: {debugInfo.ComponentsDuplicated}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _graphics.DrawText($"Mods Matched: {debugInfo.ModsMatched}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _graphics.DrawText($"Mods Unmatched: {debugInfo.ModsUnmatched}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                _graphics.DrawText($"Last Altar Type: {debugInfo.LastProcessedAltarType}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                if (!string.IsNullOrEmpty(debugInfo.LastError))
                {
                    _graphics.DrawText($"Last Error: {debugInfo.LastError}", new Vector2(xPos, yPos), Color.Red, 16);
                    yPos += lineHeight;
                }
                if (debugInfo.LastScanTime != DateTime.MinValue)
                {
                    _graphics.DrawText($"Last Scan: {debugInfo.LastScanTime:HH:mm:ss}", new Vector2(xPos, yPos), Color.White, 16);
                    yPos += lineHeight;
                }
            }
            else
            {
                _graphics.DrawText($"  Altar Service: NULL", new Vector2(xPos, yPos), Color.Red, 16);
                yPos += lineHeight;
            }

            return yPos;
        }
        public int RenderLabelsDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"Labels Debug:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var gameController = _plugin.GameController;
            var labelsCollection = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labelsCollection != null)
            {
                int totalLabels = labelsCollection.Count;
                _graphics.DrawText($"  Total Labels: {totalLabels}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
                int validLabels = 0;
                foreach (var label in labelsCollection)
                {
                    if (label?.ItemOnGround?.Path != null)
                        validLabels++;
                }
                _graphics.DrawText($"  Valid Labels: {validLabels}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
            }
            else
            {
                _graphics.DrawText($"  Labels Collection: NULL", new Vector2(xPos, yPos), Color.Red, 16);
                yPos += lineHeight;
            }
            return yPos;
        }
        public int RenderPerformanceDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"--- Performance ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            // FPS Display
            if (_plugin is ClickIt clickItPlugin)
            {
                double fps = clickItPlugin.CurrentFPS;
                Color fpsColor = fps >= 144 ? Color.LawnGreen : (fps >= 60 ? Color.Yellow : Color.Red);
                _graphics.DrawText($"FPS: {fps:F1}", new Vector2(xPos, yPos), fpsColor, 16);
                yPos += lineHeight;

                // Render time
                var renderTimings = clickItPlugin.RenderTimings;
                if (renderTimings != null && renderTimings.Count > 0)
                {
                    long lastRenderTime = renderTimings.Last();
                    double avgRenderTime = renderTimings.Average();
                    double maxRenderTime = renderTimings.Max();

                    Color renderColor = avgRenderTime <= 6.94 ? Color.LawnGreen : (avgRenderTime <= 16.67 ? Color.Yellow : Color.Red);
                    _graphics.DrawText($"Render: {lastRenderTime} ms (avg: {avgRenderTime:F2}, max: {maxRenderTime})", new Vector2(xPos, yPos), renderColor, 16);
                    yPos += lineHeight;

                    // Show target frame time for 144 FPS
                    _graphics.DrawText($"Target: 6.94 ms/frame (144 FPS)", new Vector2(xPos, yPos), Color.Cyan, 14);
                    yPos += lineHeight;
                }
            }

            var process = Process.GetCurrentProcess();
            long memoryUsage = process.WorkingSet64 / 1024 / 1024;
            _graphics.DrawText($"Memory Usage: {memoryUsage} MB", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;

            // Access timing information from the main plugin class via reflection
            if (_plugin is ClickIt clickItPlugin2)
            {
                try
                {
                    var altarCoroutineTimerField = typeof(ClickIt).GetField("altarCoroutineTimer",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var clickCoroutineTimerField = typeof(ClickIt).GetField("clickCoroutineTimer",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (altarCoroutineTimerField?.GetValue(clickItPlugin2) is Stopwatch altarTimer)
                    {
                        // Get the timings queue for averaging
                        var altarTimingsField = typeof(ClickIt).GetField("altarCoroutineTimings",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        string displayText = $"Altar Coroutine: {altarTimer.ElapsedMilliseconds} ms";

                        if (altarTimingsField?.GetValue(clickItPlugin2) is Queue<long> altarTimings && altarTimings.Count > 0)
                        {
                            double average = altarTimings.Average();
                            displayText += $" (avg: {average:F1} ms)";
                        }

                        _graphics.DrawText(displayText, new Vector2(xPos, yPos), Color.White, 16);
                        yPos += lineHeight;
                    }

                    if (clickCoroutineTimerField?.GetValue(clickItPlugin2) is Stopwatch clickTimer)
                    {
                        // Get the timings queue for averaging
                        var clickTimingsField = typeof(ClickIt).GetField("clickCoroutineTimings",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        string displayText = $"Click Coroutine: {clickTimer.ElapsedMilliseconds} ms";

                        if (clickTimingsField?.GetValue(clickItPlugin2) is Queue<long> clickTimings && clickTimings.Count > 0)
                        {
                            double average = clickTimings.Average();
                            displayText += $" (avg: {average:F1} ms)";
                        }

                        _graphics.DrawText(displayText, new Vector2(xPos, yPos), Color.White, 16);
                        yPos += lineHeight;
                    }
                }
                catch
                {
                    _graphics.DrawText($"Timing Info Unavailable", new Vector2(xPos, yPos), Color.Gray, 16);
                    yPos += lineHeight;
                }
            }

            return yPos;
        }
        public int RenderErrorsDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"--- Recent Errors ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            // Access recent errors from the main plugin class
            if (_plugin is ClickIt clickItPlugin)
            {
                var recentErrorsField = typeof(ClickIt).GetField("recentErrors",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (recentErrorsField?.GetValue(clickItPlugin) is List<string> recentErrors)
                {
                    if (recentErrors.Count > 0)
                    {
                        _graphics.DrawText($"Error Count: {recentErrors.Count}", new Vector2(xPos, yPos), Color.White, 16);
                        yPos += lineHeight;

                        for (int i = Math.Max(0, recentErrors.Count - 3); i < recentErrors.Count; i++)
                        {
                            string error = recentErrors[i];
                            yPos = RenderWrappedText($"  {error}", new Vector2(xPos, yPos), Color.Red, 14, lineHeight, 50);
                        }
                    }
                    else
                    {
                        _graphics.DrawText($"No Recent Errors", new Vector2(xPos, yPos), Color.LightGreen, 16);
                        yPos += lineHeight;
                    }
                }
                else
                {
                    _graphics.DrawText($"  Error Tracking Unavailable", new Vector2(xPos, yPos), Color.Gray, 16);
                    yPos += lineHeight;
                }
            }
            else
            {
                _graphics.DrawText($"  Plugin Instance Error", new Vector2(xPos, yPos), Color.Red, 16);
                yPos += lineHeight;
            }

            return yPos;
        }
    }
}
