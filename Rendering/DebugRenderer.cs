using ExileCore;
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
            _graphics.DrawText($"ClickIt Plugin Status:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var gameController = _plugin.GameController;
            bool inGame = gameController?.InGame == true;
            Color gameColor = inGame ? Color.LightGreen : Color.Red;
            _graphics.DrawText($"  In Game: {inGame}", new Vector2(xPos, yPos), gameColor, 16);
            yPos += lineHeight;
            bool entityListValid = gameController?.EntityListWrapper?.ValidEntitiesByType != null;
            Color entityColor = entityListValid ? Color.LightGreen : Color.Red;
            _graphics.DrawText($"  Entity List Valid: {entityListValid}", new Vector2(xPos, yPos), entityColor, 16);
            yPos += lineHeight;
            bool playerValid = gameController?.Player != null;
            Color playerColor = playerValid ? Color.LightGreen : Color.Red;
            _graphics.DrawText($"  Player Valid: {playerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;
            return yPos;
        }
        public int RenderInputDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"Input Debug:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            bool hotkeyPressed = Input.GetKeyState(_plugin.Settings.ClickLabelKey);
            Color hotkeyColor = hotkeyPressed ? Color.LightGreen : Color.Gray;
            _graphics.DrawText($"  Hotkey Pressed: {hotkeyPressed}", new Vector2(xPos, yPos), hotkeyColor, 16);
            yPos += lineHeight;
            return yPos;
        }
        public int RenderGameStateDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"Game State:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var gameController = _plugin.GameController;
            var currentArea = gameController?.Area?.CurrentArea;
            string areaName = currentArea?.DisplayName ?? "Unknown";
            _graphics.DrawText($"  Area: {areaName}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            bool hasItems = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count > 0;
            Color itemColor = hasItems ? Color.LightGreen : Color.Gray;
            _graphics.DrawText($"  Items on Ground: {hasItems}", new Vector2(xPos, yPos), itemColor, 16);
            yPos += lineHeight;
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
            _graphics.DrawText($"Altar Service Debug:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
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
            _graphics.DrawText($"Performance Debug:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            var process = Process.GetCurrentProcess();
            long memoryUsage = process.WorkingSet64 / 1024 / 1024;
            _graphics.DrawText($"  Memory: {memoryUsage} MB", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            double cpuUsage = 0;
            try
            {
                cpuUsage = process.TotalProcessorTime.TotalMilliseconds;
            }
            catch
            {
                cpuUsage = 0;
            }
            _graphics.DrawText($"  CPU Time: {cpuUsage:F2} ms", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            return yPos;
        }
        public int RenderErrorsDebug(int xPos, int yPos, int lineHeight)
        {
            _graphics.DrawText($"Errors Debug:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;
            _graphics.DrawText($"  No Error Tracking Available", new Vector2(xPos, yPos), Color.Gray, 16);
            yPos += lineHeight;
            return yPos;
        }
    }
}
