using ClickIt.Utils;
using ClickIt.Constants;
using ClickIt.Components;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using SharpDX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ClickIt
{
#nullable enable
    public class ClickIt : BaseSettingsPlugin<ClickItSettings>
    {
        private const string CleansingFireAltar = "CleansingFireAltar";
        private const string TangleAltar = "TangleAltar";
        private const string Brequel = "Brequel";
        private const string CrimsonIron = "CrimsonIron";
        private const string CopperAltar = "copper_altar";
        private const string PetrifiedWood = "PetrifiedWood";
        private const string Bismuth = "Bismuth";
        private const string Verisium = "Verisium";
        private const string ReportBugMessage = "\nPlease report this as a bug on github";

        private Stopwatch Timer { get; } = new Stopwatch();
        private Stopwatch SecondTimer { get; } = new Stopwatch();
        private Random Random { get; } = new Random();
        private TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }

        // Performance tracking
        private readonly Stopwatch renderTimer = new Stopwatch();
        private readonly Stopwatch altarCoroutineTimer = new Stopwatch();
        private readonly Stopwatch clickCoroutineTimer = new Stopwatch();
        private int renderFrameCount = 0;
        private readonly Stopwatch renderFpsTimer = new Stopwatch();
        private float currentRenderFps = 0;
        private double lastAltarCoroutineMs = 0;
        private double lastClickCoroutineMs = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder ss, int count);



        private Coroutine? altarCoroutine;
        private Coroutine? clickLabelCoroutine;
        private Coroutine? inputSafetyCoroutine;

        // Verisium hold click state
        private bool isHoldingVerisiumClick = false;
        private readonly Stopwatch verisiumHoldTimer = new Stopwatch();
        private const int VERISIUM_HOLD_FAILSAFE_MS = 10000; // 10 seconds

        private bool isInputCurrentlyBlocked = false;
        private readonly Stopwatch lastHotkeyReleaseTimer = new Stopwatch();
        private const int HOTKEY_RELEASE_FAILSAFE_MS = 5000; // 5 seconds after hotkey release
        private bool lastHotkeyState = false;

        // Services
        private Services.AreaService? areaService;
        private Services.AltarService? altarService;
        private Services.LabelFilterService? labelFilterService;
        private Utils.InputHandler? inputHandler;
        private Rendering.AltarRenderer? altarRenderer;
        private Services.AltarWeightCalculator? altarWeightCalculator;

        private RectangleF FullScreenRectangle { get; set; }
        private RectangleF HealthAndFlaskRectangle { get; set; }
        private RectangleF ManaAndSkillsRectangle { get; set; }
        private RectangleF BuffsAndDebuffsRectangle { get; set; }

        // Error tracking
        private readonly List<string> recentErrors = new List<string>();
        private const int MAX_ERRORS_TO_TRACK = 10;

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
        }

        public override void OnClose()
        {
            try
            {
                ForceUnblockInput("Plugin closing");

                // Clean up Verisium state
                if (isHoldingVerisiumClick)
                {
                    Mouse.LeftMouseUp();
                    isHoldingVerisiumClick = false;
                    verisiumHoldTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Mouse.blockInput(false);
                }
                catch
                {
                    // Nothing more we can do
                }
                LogError($"Error during plugin shutdown: {ex.Message}");
            }

            base.OnClose();
        }

        public override bool Initialise()
        {
            Settings.ReportBugButton.OnPressed += () => { _ = Process.Start("explorer", "http://github.com/Barragek0/ClickIt/issues"); };

            CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, 200);

            // Initialize services
            areaService = new Services.AreaService();
            areaService.UpdateScreenAreas(GameController);

            altarService = new Services.AltarService(Settings, CachedLabels);
            labelFilterService = new Services.LabelFilterService(Settings);
            inputHandler = new Utils.InputHandler(Settings);
            altarWeightCalculator = new Services.AltarWeightCalculator(Settings);
            altarRenderer = new Rendering.AltarRenderer(Graphics, Settings);

            // Initialize legacy rectangles for backward compatibility (to be removed)
            FullScreenRectangle = areaService.FullScreenRectangle;
            HealthAndFlaskRectangle = areaService.HealthAndFlaskRectangle;
            ManaAndSkillsRectangle = areaService.ManaAndSkillsRectangle;
            BuffsAndDebuffsRectangle = areaService.BuffsAndDebuffsRectangle;

            Timer.Start();
            SecondTimer.Start();

            altarCoroutine = new Coroutine(MainScanForAltarsLogic(), this, "ClickIt.ScanForAltarsLogic", false);
            _ = Core.ParallelRunner.Run(altarCoroutine);
            altarCoroutine.Priority = CoroutinePriority.High;

            clickLabelCoroutine = new Coroutine(MainClickLabelCoroutine(), this, "ClickIt.ClickLogic", false);
            _ = Core.ParallelRunner.Run(clickLabelCoroutine);
            clickLabelCoroutine.Priority = CoroutinePriority.High;

            inputSafetyCoroutine = new Coroutine(InputSafetyCoroutine(), this, "ClickIt.InputSafety");
            _ = Core.ParallelRunner.Run(inputSafetyCoroutine);
            inputSafetyCoroutine.Priority = CoroutinePriority.Critical;

            Settings.EnsureAllModsHaveWeights();

            // Start the render FPS timer
            renderFpsTimer.Start();

            return true;
        }



        private bool PointIsInClickableArea(Vector2 point, string? path = null)
        {
            areaService?.UpdateScreenAreas(GameController);
            return areaService?.PointIsInClickableArea(point) ?? false;
        }

        public override void Render()
        {
            renderTimer.Restart();

            // Always render debug frames if enabled
            RenderDebugFrames();

            // Debug information to check plugin status
            if (Settings.DebugMode && Settings.RenderDebug)
            {
                RenderDetailedDebugInfo();
            }

            RenderAltarComponents();

            // Track render performance
            renderTimer.Stop();
            UpdateRenderFps();
        }

        private void UpdateRenderFps()
        {
            renderFrameCount++;
            if (renderFpsTimer.Elapsed.TotalSeconds >= 1.0)
            {
                currentRenderFps = renderFrameCount / (float)renderFpsTimer.Elapsed.TotalSeconds;
                renderFrameCount = 0;
                renderFpsTimer.Restart();
            }
        }

        private void RenderDetailedDebugInfo()
        {
            int startY = 120;
            int lineHeight = 18;
            int columnWidth = 300; // Width of each column

            // Column 1 (left side)
            int col1X = 10;
            int yPos = startY;

            // Plugin Status Section
            yPos = RenderPluginStatusDebug(col1X, yPos, lineHeight);

            // Performance Section
            yPos = RenderPerformanceDebug(col1X, yPos, lineHeight);

            // Input & Hotkeys Section
            yPos = RenderInputDebug(col1X, yPos, lineHeight);

            // Game State Section
            yPos = RenderGameStateDebug(col1X, yPos, lineHeight);

            // Altar Debug Section (detailed processing info)
            yPos = RenderAltarServiceDebug(col1X, yPos, lineHeight);

            // Column 2 (right side) - start from top again
            int col2X = col1X + columnWidth;
            yPos = startY;

            // Altar Detection Section
            yPos = RenderAltarDebug(col2X, yPos, lineHeight);

            // Labels & Clicking Section
            yPos = RenderLabelsDebug(col2X, yPos, lineHeight);

            // Error Section
            yPos = RenderErrorsDebug(col2X, yPos, lineHeight);

            // Verisium State Section
            yPos = RenderVerisiumDebug(col2X, yPos, lineHeight);
        }

        private int RenderPluginStatusDebug(int xPos, int yPos, int lineHeight)
        {
            Graphics.DrawText("--- Plugin Status ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            var enabledStatus = GetStatusText(Settings.Enable.Value, "True", "False");
            Graphics.DrawText($"Enabled: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(enabledStatus.text, new Vector2(xPos + 70, yPos), enabledStatus.color, 16);
            yPos += lineHeight;

            var threadingStatus = GetStatusText(CanUseMultiThreading, "True", "False");
            Graphics.DrawText($"Can Use MultiThreading: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(threadingStatus.text, new Vector2(xPos + 190, yPos), threadingStatus.color, 16);
            yPos += lineHeight;

            return yPos + lineHeight;
        }

        private int RenderInputDebug(int xPos, int yPos, int lineHeight)
        {
            Graphics.DrawText("--- Input & Safety ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

#pragma warning disable CS0618
            bool hotkeyPressed = ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value);
#pragma warning restore CS0618
            Graphics.DrawText($"Hotkey ({Settings.ClickLabelKey.Value}): ", new Vector2(xPos, yPos), Color.Orange, 16);
            Color hotkeyColor = hotkeyPressed ? Color.LightGreen : Color.Gray;
            Graphics.DrawText(hotkeyPressed ? "Pressed" : "Released", new Vector2(xPos + 95, yPos), hotkeyColor, 16);
            yPos += lineHeight;

            var canClickStatus = GetStatusText(canClick(), "Yes", "No");
            Graphics.DrawText($"Can Click: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(canClickStatus.text, new Vector2(xPos + 85, yPos), canClickStatus.color, 16);
            yPos += lineHeight;

            var blockInputStatus = GetStatusText(Settings.BlockUserInput.Value, "Enabled", "Disabled");
            Graphics.DrawText($"Block User Input: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(blockInputStatus.text, new Vector2(xPos + 140, yPos), blockInputStatus.color, 16);
            yPos += lineHeight;

            var leftHandedStatus = GetStatusText(Settings.LeftHanded.Value, "Enabled", "Disabled");
            Graphics.DrawText($"Left Handed Mode: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(leftHandedStatus.text, new Vector2(xPos + 140, yPos), leftHandedStatus.color, 16);
            yPos += lineHeight;

            return yPos + lineHeight;
        }

        private int RenderGameStateDebug(int xPos, int yPos, int lineHeight)
        {
            Graphics.DrawText("--- Game State ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            bool inGame = GameController?.Game?.IngameState?.InGame == true;
            var inGameStatus = GetStatusText(inGame, "Active", "Inactive");
            Graphics.DrawText($"In Game: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(inGameStatus.text, new Vector2(xPos + 75, yPos), inGameStatus.color, 16);
            yPos += lineHeight;

            bool hasGameController = GameController != null;
            var controllerStatus = GetStatusText(hasGameController, "Connected", "Missing");
            Graphics.DrawText($"GameController: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(controllerStatus.text, new Vector2(xPos + 125, yPos), controllerStatus.color, 16);
            yPos += lineHeight;

            return yPos + lineHeight;
        }

        private int RenderAltarDebug(int xPos, int yPos, int lineHeight)
        {
            Graphics.DrawText("--- Altar Detection ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            var altarComps = altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
            Color altarCountColor = altarComps.Count > 0 ? Color.LightGreen : Color.Gray;
            Graphics.DrawText($"Altar Components: {altarComps.Count}", new Vector2(xPos, yPos), altarCountColor, 16);
            yPos += lineHeight;

            if (altarComps.Count > 0)
            {
                Graphics.DrawText("Active Altars:", new Vector2(xPos, yPos), Color.Cyan, 16);
                yPos += lineHeight;

                for (int i = 0; i < Math.Min(altarComps.Count, 2); i++)
                {
                    var altar = altarComps[i];
                    yPos = RenderSingleAltarDebug(xPos, yPos, lineHeight, altar, i + 1);
                }
            }

            return yPos + lineHeight;
        }

        private int RenderSingleAltarDebug(int xPos, int yPos, int lineHeight, PrimaryAltarComponent altar, int altarNumber)
        {
            Color altarTypeColor = altar.AltarType == AltarType.SearingExarch ? Color.Orange : Color.Purple;
            Graphics.DrawText($"  #{altarNumber}: {altar.AltarType}", new Vector2(xPos, yPos), altarTypeColor, 16);
            yPos += lineHeight;

            yPos = RenderAltarMods(xPos, yPos, lineHeight, altar.TopMods.Upsides, "Upsides", "T", Color.LightGreen);
            yPos = RenderAltarMods(xPos, yPos, lineHeight, altar.BottomMods.Upsides, "", "B", Color.LightGreen);
            yPos = RenderAltarMods(xPos, yPos, lineHeight, altar.TopMods.Downsides, "Downsides", "T", Color.LightCoral);
            yPos = RenderAltarMods(xPos, yPos, lineHeight, altar.BottomMods.Downsides, "", "B", Color.LightCoral);

            return yPos + lineHeight;
        }

        private int RenderAltarMods(int xPos, int yPos, int lineHeight, List<string> mods, string header, string prefix, Color color)
        {
            var validMods = mods.Where(mod => !string.IsNullOrEmpty(mod)).Take(2).ToList();
            if (validMods.Count == 0) return yPos;

            if (!string.IsNullOrEmpty(header))
            {
                Graphics.DrawText($"    {header}:", new Vector2(xPos, yPos), color, 14);
                yPos += lineHeight;
            }

            foreach (var mod in validMods)
            {
                yPos = RenderWrappedText($"      {prefix}: {mod}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
            }

            return yPos;
        }

        private int RenderWrappedText(string text, Vector2 position, Color color, int fontSize, int lineHeight, int maxCharsPerLine)
        {
            int yPos = (int)position.Y;
            int currentPos = 0;
            bool isFirstLine = true;

            // Find the position where actual content starts (after the colon and space)
            int colonPos = text.IndexOf(':');
            int contentStartPos = colonPos >= 0 ? colonPos + 2 : 0; // Position after ": "

            // Count leading spaces to preserve the original indentation structure
            int leadingSpaces = 0;
            for (int i = 0; i < text.Length && text[i] == ' '; i++)
            {
                leadingSpaces++;
            }

            while (currentPos < text.Length)
            {
                int remainingChars = text.Length - currentPos;
                int charsToTake = Math.Min(maxCharsPerLine, remainingChars);

                // Try to break at word boundary if we're not at the end
                if (charsToTake == maxCharsPerLine && currentPos + charsToTake < text.Length)
                {
                    int lastSpace = text.LastIndexOf(' ', currentPos + charsToTake, charsToTake);
                    if (lastSpace > currentPos)
                    {
                        charsToTake = lastSpace - currentPos;
                    }
                }

                string line;
                if (isFirstLine)
                {
                    // First line: preserve original text with its indentation
                    line = text.Substring(currentPos, charsToTake);
                }
                else
                {
                    // Continuation lines: add indentation to align with content after the colon
                    string content = text.Substring(currentPos, charsToTake).Trim();
                    line = new string(' ', contentStartPos) + content;
                }

                Graphics.DrawText(line, new Vector2(position.X, yPos), color, fontSize);

                currentPos += charsToTake;
                if (currentPos < text.Length && text[currentPos] == ' ')
                {
                    currentPos++; // Skip the space at the beginning of next line
                }

                yPos += lineHeight;
                isFirstLine = false;
            }

            return yPos;
        }

        private int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
        {
            Graphics.DrawText("--- Altar Debug ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            if (altarService?.DebugInfo == null)
            {
                Graphics.DrawText("AltarService not available", new Vector2(xPos, yPos), Color.Red, 16);
                return yPos + lineHeight;
            }

            var debugInfo = altarService.DebugInfo;

            // Scan statistics
            Graphics.DrawText($"Last Scan: ", new Vector2(xPos, yPos), Color.Orange, 16);
            var timeSinceLastScan = DateTime.Now - debugInfo.LastScanTime;
            string timeStr = timeSinceLastScan.TotalSeconds < 60 ?
                $"{timeSinceLastScan.TotalSeconds:F1}s ago" :
                $"{timeSinceLastScan.TotalMinutes:F1}m ago";
            Graphics.DrawText(timeStr, new Vector2(xPos + 85, yPos), Color.White, 16);
            yPos += lineHeight;

            // Label counts
            Graphics.DrawText($"Exarch Labels: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText($"{debugInfo.LastScanExarchLabels}", new Vector2(xPos + 120, yPos),
                debugInfo.LastScanExarchLabels > 0 ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            Graphics.DrawText($"Eater Labels: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText($"{debugInfo.LastScanEaterLabels}", new Vector2(xPos + 105, yPos),
                debugInfo.LastScanEaterLabels > 0 ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            // Processing statistics
            Graphics.DrawText($"Elements Found: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText($"{debugInfo.ElementsFound}", new Vector2(xPos + 125, yPos),
                debugInfo.ElementsFound > 0 ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            Graphics.DrawText($"Components: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText($"{debugInfo.ComponentsAdded}/{debugInfo.ComponentsProcessed}", new Vector2(xPos + 105, yPos),
                debugInfo.ComponentsAdded > 0 ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            if (debugInfo.ComponentsDuplicated > 0)
            {
                Graphics.DrawText($"Duplicates: ", new Vector2(xPos, yPos), Color.Orange, 16);
                Graphics.DrawText($"{debugInfo.ComponentsDuplicated}", new Vector2(xPos + 95, yPos), Color.Yellow, 16);
                yPos += lineHeight;
            }

            // Mod matching statistics
            Graphics.DrawText($"Mods Matched: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText($"{debugInfo.ModsMatched}", new Vector2(xPos + 125, yPos),
                debugInfo.ModsMatched > 0 ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            if (debugInfo.ModsUnmatched > 0)
            {
                Graphics.DrawText($"Mods Failed: ", new Vector2(xPos, yPos), Color.Orange, 16);
                Graphics.DrawText($"{debugInfo.ModsUnmatched}", new Vector2(xPos + 105, yPos), Color.Red, 16);
                yPos += lineHeight;

                // Show recent unmatched mods
                if (debugInfo.RecentUnmatchedMods.Count > 0)
                {
                    Graphics.DrawText("Recent Failed:", new Vector2(xPos, yPos), Color.Cyan, 14);
                    yPos += lineHeight;

                    foreach (var mod in debugInfo.RecentUnmatchedMods.Take(3))
                    {
                        Graphics.DrawText($"  {mod}", new Vector2(xPos, yPos), Color.Red, 12);
                        yPos += lineHeight;
                    }
                }
            }

            if (!string.IsNullOrEmpty(debugInfo.LastProcessedAltarType))
            {
                Graphics.DrawText($"Last Type: ", new Vector2(xPos, yPos), Color.Orange, 16);
                Graphics.DrawText(debugInfo.LastProcessedAltarType, new Vector2(xPos + 90, yPos), Color.Cyan, 16);
                yPos += lineHeight;
            }

            return yPos + lineHeight;
        }

        private int RenderLabelsDebug(int xPos, int yPos, int lineHeight)
        {
            Graphics.DrawText("--- Labels & Clicking ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            var labels = CachedLabels?.Value ?? new List<LabelOnGround>();
            Color labelCountColor = labels.Count > 0 ? Color.LightGreen : Color.Gray;
            Graphics.DrawText($"Ground Labels: {labels.Count}", new Vector2(xPos, yPos), labelCountColor, 16);
            yPos += lineHeight;

            if (labels.Count > 0)
            {
                Graphics.DrawText("Visible Labels:", new Vector2(xPos, yPos), Color.Cyan, 16);
                yPos += lineHeight;

                var nearestLabels = labels
                    .Where(label => label?.ItemOnGround?.Path != null)
                    .OrderBy(label => label.ItemOnGround.DistancePlayer)
                    .Take(10)
                    .ToList();

                for (int i = 0; i < nearestLabels.Count; i++)
                {
                    var label = nearestLabels[i];
                    string itemName = GetItemDisplayName(label);
                    float distance = label.ItemOnGround.DistancePlayer;

                    // Draw the number in orange, then the item name in its rarity color
                    Graphics.DrawText($"  #{i + 1}: ", new Vector2(xPos, yPos), Color.Orange, 14);
                    Color itemColor = GetItemRarityColor(label);
                    Graphics.DrawText($"{itemName} ({distance:F0})", new Vector2(xPos + 45, yPos), itemColor, 14);
                    yPos += lineHeight;
                }
            }

            return yPos + lineHeight;
        }

        private string GetItemDisplayName(LabelOnGround label)
        {
            try
            {
                var item = label.ItemOnGround;
                string path = item.Path ?? "Unknown";

                // Extract meaningful name from path
                if (path.Contains("/"))
                {
                    string[] pathParts = path.Split('/');
                    string fileName = pathParts[pathParts.Length - 1];

                    // Clean up common prefixes/suffixes
                    fileName = fileName.Replace("Currency", "").Replace("Pickup", "").Replace("Drop", "");

                    if (!string.IsNullOrEmpty(fileName))
                        return fileName;
                }

                // Fallback to component name if available
                var metadata = item.GetComponent<ExileCore.PoEMemory.Components.Mods>();
                if (metadata?.ItemRarity != null)
                {
                    return metadata.ItemRarity.ToString();
                }

                return path.Length > 20 ? path.Substring(0, 20) + "..." : path;
            }
            catch
            {
                return "Unknown Item";
            }
        }

        private Color GetItemRarityColor(LabelOnGround label)
        {
            try
            {
                var item = label.ItemOnGround;
                var mods = item.GetComponent<ExileCore.PoEMemory.Components.Mods>();

                if (mods?.ItemRarity != null)
                {
                    return mods.ItemRarity switch
                    {
                        ItemRarity.Normal => Color.White,
                        ItemRarity.Magic => Color.CornflowerBlue,
                        ItemRarity.Rare => Color.Yellow,
                        ItemRarity.Unique => Color.Orange,
                        _ => Color.LightGray
                    };
                }

                return Color.LightGray;
            }
            catch
            {
                return Color.LightGray;
            }
        }

        private int RenderPerformanceDebug(int xPos, int yPos, int lineHeight)
        {
            Graphics.DrawText("--- Performance ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            // Render FPS
            Graphics.DrawText($"Render FPS: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Color fpsColor;
            if (currentRenderFps > 50) fpsColor = Color.LightGreen;
            else if (currentRenderFps > 30) fpsColor = Color.Yellow;
            else fpsColor = Color.Red;
            Graphics.DrawText($"{currentRenderFps:F1}", new Vector2(xPos + 100, yPos), fpsColor, 16);
            yPos += lineHeight;

            // Altar Coroutine Performance
            Graphics.DrawText($"Altar Scan: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Color altarColor;
            if (lastAltarCoroutineMs < 5) altarColor = Color.LightGreen;
            else if (lastAltarCoroutineMs < 15) altarColor = Color.Yellow;
            else altarColor = Color.Red;
            Graphics.DrawText($"{lastAltarCoroutineMs:F1}ms", new Vector2(xPos + 100, yPos), altarColor, 16);
            yPos += lineHeight;

            // Click Coroutine Performance
            Graphics.DrawText($"Click Logic: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Color clickColor;
            if (lastClickCoroutineMs < 2) clickColor = Color.LightGreen;
            else if (lastClickCoroutineMs < 10) clickColor = Color.Yellow;
            else clickColor = Color.Red;
            Graphics.DrawText($"{lastClickCoroutineMs:F1}ms", new Vector2(xPos + 100, yPos), clickColor, 16);
            yPos += lineHeight;

            // Coroutines Section
            Graphics.DrawText($"Coroutines:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            bool altarRunning = altarCoroutine?.IsDone == false;
            Graphics.DrawText($"   AltarScan: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(altarRunning ? "Running" : "IDLE", new Vector2(xPos + 110, yPos),
                altarRunning ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            bool clickRunning = clickLabelCoroutine?.IsDone == false;
            Graphics.DrawText($"   ClickLabel: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(clickRunning ? "Running" : "IDLE", new Vector2(xPos + 115, yPos),
                clickRunning ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            bool safetyRunning = inputSafetyCoroutine?.IsDone == false;
            Graphics.DrawText($"   InputSafety: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(safetyRunning ? "Running" : "IDLE", new Vector2(xPos + 125, yPos),
                safetyRunning ? Color.LightGreen : Color.Gray, 16);
            yPos += lineHeight;

            // Services Section
            Graphics.DrawText($"Services:", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            Graphics.DrawText($"   AltarService: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(altarService != null ? "Alive" : "IDLE", new Vector2(xPos + 135, yPos),
                altarService != null ? Color.LightGreen : Color.Red, 16);
            yPos += lineHeight;

            Graphics.DrawText($"   AltarWeight: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(altarWeightCalculator != null ? "Alive" : "IDLE", new Vector2(xPos + 130, yPos),
                altarWeightCalculator != null ? Color.LightGreen : Color.Red, 16);
            yPos += lineHeight;

            Graphics.DrawText($"   AreaService: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(areaService != null ? "Alive" : "IDLE", new Vector2(xPos + 130, yPos),
                areaService != null ? Color.LightGreen : Color.Red, 16);
            yPos += lineHeight;

            Graphics.DrawText($"   LabelFilter: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(labelFilterService != null ? "Alive" : "IDLE", new Vector2(xPos + 130, yPos),
                labelFilterService != null ? Color.LightGreen : Color.Red, 16);
            yPos += lineHeight;

            Graphics.DrawText($"   InputHandler: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(inputHandler != null ? "Alive" : "IDLE", new Vector2(xPos + 135, yPos),
                inputHandler != null ? Color.LightGreen : Color.Red, 16);
            yPos += lineHeight;

            Graphics.DrawText($"   AltarRenderer: ", new Vector2(xPos, yPos), Color.Orange, 16);
            Graphics.DrawText(altarRenderer != null ? "Alive" : "IDLE", new Vector2(xPos + 140, yPos),
                altarRenderer != null ? Color.LightGreen : Color.Red, 16);
            yPos += lineHeight;

            return yPos + lineHeight;
        }



        private int RenderVerisiumDebug(int xPos, int yPos, int lineHeight)
        {
            if (isHoldingVerisiumClick || verisiumHoldTimer.ElapsedMilliseconds > 0)
            {
                Graphics.DrawText("--- Verisium State ---", new Vector2(xPos, yPos), Color.Yellow, 16);
                yPos += lineHeight;

                var verisiumStatus = GetStatusText(isHoldingVerisiumClick, "Active", "Inactive");
                Graphics.DrawText($"Holding Verisium Click: ", new Vector2(xPos, yPos), Color.Orange, 16);
                Graphics.DrawText(verisiumStatus.text, new Vector2(xPos + 185, yPos), verisiumStatus.color, 16);
                yPos += lineHeight;

                if (verisiumHoldTimer.IsRunning)
                {
                    long holdTime = verisiumHoldTimer.ElapsedMilliseconds;
                    Color holdColor;
                    if (holdTime > 8000)
                        holdColor = Color.Red;
                    else if (holdTime > 5000)
                        holdColor = Color.Orange;
                    else
                        holdColor = Color.White;

                    Graphics.DrawText($"Hold Duration: {holdTime}ms", new Vector2(xPos, yPos), holdColor, 16);
                    yPos += lineHeight;
                }

                yPos += lineHeight;
            }

            return yPos;
        }
        private int RenderErrorsDebug(int xPos, int yPos, int lineHeight)
        {
            if (recentErrors.Count > 0)
            {
                Graphics.DrawText("--- Recent Errors ---", new Vector2(xPos, yPos), Color.Yellow, 16);
                yPos += lineHeight;

                for (int i = Math.Max(0, recentErrors.Count - 5); i < recentErrors.Count; i++)
                {
                    Graphics.DrawText($"• {recentErrors[i]}", new Vector2(xPos, yPos), Color.LightCoral, 14);
                    yPos += lineHeight;
                }
            }

            return yPos + lineHeight;
        }

        private void RenderDebugFrames()
        {
            bool debugMode = Settings.DebugMode;
            bool renderDebug = Settings.RenderDebug;

            if (debugMode && renderDebug)
            {
                Graphics.DrawFrame(FullScreenRectangle, Color.Green, 1);
                Graphics.DrawFrame(HealthAndFlaskRectangle, Color.Orange, 1);
                Graphics.DrawFrame(ManaAndSkillsRectangle, Color.Cyan, 1);
                Graphics.DrawFrame(BuffsAndDebuffsRectangle, Color.Yellow, 1);
            }
        }

        private void RenderAltarComponents()
        {
            List<PrimaryAltarComponent> altarSnapshot = altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
            bool clickEater = Settings.ClickEaterAltars;
            bool clickExarch = Settings.ClickExarchAltars;
            bool leftHanded = Settings.LeftHanded;
            Vector2 windowTopLeft = GameController.Window.GetWindowRectangleTimeCache.TopLeft;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                RenderSingleAltar(altar, clickEater, clickExarch, leftHanded, windowTopLeft);
            }
        }

        private void RenderSingleAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch, bool leftHanded, Vector2 windowTopLeft)
        {
            var altarWeights = CalculateAltarWeights(altar);
            RectangleF topModsRect = altar.TopMods.Element.GetClientRect();
            RectangleF bottomModsRect = altar.BottomMods.Element.GetClientRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;
            Vector2 bottomModsTopLeft = bottomModsRect.TopLeft;

            Element? boxToClick = DetermineAltarChoice(altar, altarWeights, topModsRect, bottomModsRect, topModsTopLeft);

            DrawWeightTexts(altarWeights, topModsTopLeft, bottomModsTopLeft);

        }

        private AltarWeights CalculateAltarWeights(PrimaryAltarComponent altar)
        {
            decimal TopUpsideWeight = CalculateUpsideWeight(altar.TopMods.Upsides);
            decimal TopDownsideWeight = CalculateDownsideWeight(altar.TopMods.Downsides);
            decimal BottomUpsideWeight = CalculateUpsideWeight(altar.BottomMods.Upsides);
            decimal BottomDownsideWeight = CalculateDownsideWeight(altar.BottomMods.Downsides);

            return new AltarWeights
            {
                TopUpsideWeight = TopUpsideWeight,
                TopDownsideWeight = TopDownsideWeight,
                BottomUpsideWeight = BottomUpsideWeight,
                BottomDownsideWeight = BottomDownsideWeight,
                TopDownside1Weight = CalculateDownsideWeight([altar.TopMods.FirstDownside]),
                TopDownside2Weight = CalculateDownsideWeight([altar.TopMods.SecondDownside]),
                BottomDownside1Weight = CalculateDownsideWeight([altar.BottomMods.FirstDownside]),
                BottomDownside2Weight = CalculateDownsideWeight([altar.BottomMods.SecondDownside]),
                TopUpside1Weight = CalculateUpsideWeight([altar.TopMods.FirstUpside]),
                TopUpside2Weight = CalculateUpsideWeight([altar.TopMods.SecondUpside]),
                BottomUpside1Weight = CalculateUpsideWeight([altar.BottomMods.FirstUpside]),
                BottomUpside2Weight = CalculateUpsideWeight([altar.BottomMods.SecondUpside]),
                TopWeight = Math.Round(TopUpsideWeight / TopDownsideWeight, 2),
                BottomWeight = Math.Round(BottomUpsideWeight / BottomDownsideWeight, 2)
            };
        }

        private Element? DetermineAltarChoice(PrimaryAltarComponent altar, AltarWeights weights, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 topModsTopLeft)
        {
            Vector2 offset120_Minus60 = new(120, -70);
            Vector2 offset120_Minus25 = new(120, -25);

            if (weights.TopUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top upside", altar.TopMods.FirstUpside, altar.TopMods.SecondUpside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.TopDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Top downside", altar.TopMods.FirstDownside, altar.TopMods.SecondDownside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.BottomUpsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom upside", altar.BottomMods.FirstUpside, altar.BottomMods.SecondUpside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            if (weights.BottomDownsideWeight <= 0)
            {
                DrawUnrecognizedWeightText("Bottom downside", altar.BottomMods.FirstDownside, altar.BottomMods.SecondDownside, topModsTopLeft + offset120_Minus60);
                DrawYellowFrames(topModsRect, bottomModsRect);
                return null;
            }

            return EvaluateAltarWeights(weights, altar, topModsRect, bottomModsRect, topModsTopLeft + offset120_Minus60, topModsTopLeft + offset120_Minus25);
        }
        private Element? EvaluateAltarWeights(AltarWeights weights, PrimaryAltarComponent altar, RectangleF topModsRect, RectangleF bottomModsRect, Vector2 textPos1, Vector2 textPos2)
        {

            if ((weights.TopDownside1Weight >= 90 || weights.TopDownside2Weight >= 90) && (weights.BottomDownside1Weight >= 90 || weights.BottomDownside2Weight >= 90))
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nBoth options have downsides with a weight of 90+ that may brick your build.", textPos1, Color.Orange, 30);
                Graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                Graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return null;
            }

            if (weights.TopUpside1Weight >= 90 || weights.TopUpside2Weight >= 90)
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the top upsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                Graphics.DrawFrame(topModsRect, Color.LawnGreen, 3);
                Graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return altar.TopButton.Element;
            }

            if (weights.BottomUpside1Weight >= 90 || weights.BottomUpside2Weight >= 90)
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the bottom upsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                Graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                Graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 3);
                return altar.BottomButton.Element;
            }

            if (weights.TopDownside1Weight >= 90 || weights.TopDownside2Weight >= 90)
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nBottom has been chosen because one of the top downsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                Graphics.DrawFrame(topModsRect, Color.OrangeRed, 3);
                Graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 2);
                return altar.BottomButton.Element;
            }

            if (weights.BottomDownside1Weight >= 90 || weights.BottomDownside2Weight >= 90)
            {
                _ = Graphics.DrawText("Weighting has been overridden\n\nTop has been chosen because one of the bottom downsides has a weight of 90+", textPos1, Color.LawnGreen, 30);
                Graphics.DrawFrame(topModsRect, Color.LawnGreen, 2);
                Graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 3);
                return altar.TopButton.Element;
            }

            if (weights.TopWeight > weights.BottomWeight)
            {
                Graphics.DrawFrame(topModsRect, Color.LawnGreen, 3);
                Graphics.DrawFrame(bottomModsRect, Color.OrangeRed, 2);
                return altar.TopButton.Element;
            }

            if (weights.BottomWeight > weights.TopWeight)
            {
                Graphics.DrawFrame(topModsRect, Color.OrangeRed, 2);
                Graphics.DrawFrame(bottomModsRect, Color.LawnGreen, 3);
                return altar.BottomButton.Element;
            }

            _ = Graphics.DrawText("Mods have equal weight, you should choose.", textPos2, Color.Orange, 30);
            DrawYellowFrames(topModsRect, bottomModsRect);
            return null;
        }

        private void DrawUnrecognizedWeightText(string weightType, string mod1, string mod2, Vector2 position)
        {
            _ = Graphics.DrawText($"{weightType} weights couldn't be recognised\n1:{mod1}\n2:{mod2}{ReportBugMessage}", position, Color.Orange, 30);
        }

        private void DrawYellowFrames(RectangleF topModsRect, RectangleF bottomModsRect)
        {
            Graphics.DrawFrame(topModsRect, Color.Yellow, 2);
            Graphics.DrawFrame(bottomModsRect, Color.Yellow, 2);
        }

        private void DrawWeightTexts(AltarWeights weights, Vector2 topModsTopLeft, Vector2 bottomModsTopLeft)
        {
            Vector2 offset5_Minus32 = new(5, -32);
            Vector2 offset5_Minus20 = new(5, -20);
            Vector2 offset10_Minus32 = new(10, -32);
            Vector2 offset10_Minus20 = new(10, -20);
            Vector2 offset10_5 = new(10, 5);
            Color colorLawnGreen = Color.LawnGreen;
            Color colorOrangeRed = Color.OrangeRed;
            Color colorYellow = Color.Yellow;

            _ = Graphics.DrawText("Upside: " + weights.TopUpsideWeight, topModsTopLeft + offset5_Minus32, colorLawnGreen, 14);
            _ = Graphics.DrawText("Downside: " + weights.TopDownsideWeight, topModsTopLeft + offset5_Minus20, colorOrangeRed, 14);
            _ = Graphics.DrawText("Upside: " + weights.BottomUpsideWeight, bottomModsTopLeft + offset10_Minus32, colorLawnGreen, 14);
            _ = Graphics.DrawText("Downside: " + weights.BottomDownsideWeight, bottomModsTopLeft + offset10_Minus20, colorOrangeRed, 14);

            Color topWeightColor = GetWeightColor(weights.TopWeight, weights.BottomWeight, colorLawnGreen, colorOrangeRed, colorYellow);
            Color bottomWeightColor = GetWeightColor(weights.BottomWeight, weights.TopWeight, colorLawnGreen, colorOrangeRed, colorYellow);

            _ = Graphics.DrawText("" + weights.TopWeight, topModsTopLeft + offset10_5, topWeightColor, 18);
            _ = Graphics.DrawText("" + weights.BottomWeight, bottomModsTopLeft + offset10_5, bottomWeightColor, 18);
        }

        private static Color GetWeightColor(decimal weight1, decimal weight2, Color winColor, Color loseColor, Color tieColor)
        {
            if (weight1 > weight2) return winColor;
            if (weight2 > weight1) return loseColor;
            return tieColor;
        }

        private struct AltarWeights
        {
            public decimal TopUpsideWeight;
            public decimal TopDownsideWeight;
            public decimal BottomUpsideWeight;
            public decimal BottomDownsideWeight;
            public decimal TopDownside1Weight;
            public decimal TopDownside2Weight;
            public decimal BottomDownside1Weight;
            public decimal BottomDownside2Weight;
            public decimal TopUpside1Weight;
            public decimal TopUpside2Weight;
            public decimal BottomUpside1Weight;
            public decimal BottomUpside2Weight;
            public decimal TopWeight;
            public decimal BottomWeight;
        }


        public void LogMessage(string message, int frame = 0)
        {
            if (Settings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }

        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            if (localDebug && Settings.DebugMode)
            {
                base.LogMessage(message, frame);
            }
        }

        public void LogError(string message, int frame = 0)
        {
            if (Settings.DebugMode)
            {
                base.LogError(message, frame);
                TrackError(message);
            }
        }

        public void LogError(bool localDebug, string message, int frame = 0)
        {
            if (localDebug && Settings.DebugMode)
            {
                base.LogError(message, frame);
                TrackError(message);
            }
        }

        private void TrackError(string errorMessage)
        {
            try
            {
                string timestampedError = $"[{DateTime.Now:HH:mm:ss}] {errorMessage}";
                recentErrors.Add(timestampedError);

                // Keep only the most recent errors
                if (recentErrors.Count > MAX_ERRORS_TO_TRACK)
                {
                    recentErrors.RemoveAt(0);
                }
            }
            catch
            {
                // Don't let error tracking cause more errors
            }
        }

        private (string text, Color color) GetStatusText(bool isTrue, string trueText = "True", string falseText = "False")
        {
            return isTrue ? (trueText, Color.LightGreen) : (falseText, Color.Red);
        }

        private bool canClick()
        {
            return inputHandler?.CanClick(GameController) ?? false;
        }

        private void SafeBlockInput(bool block)
        {
            try
            {
                if (block)
                {
                    if (!isInputCurrentlyBlocked)
                    {
                        Mouse.blockInput(true);
                        isInputCurrentlyBlocked = true;
                    }
                }
                else
                {
                    if (isInputCurrentlyBlocked)
                    {
                        Mouse.blockInput(false);
                        isInputCurrentlyBlocked = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ForceUnblockInput($"SafeBlockInput exception: {ex.Message}");
            }
        }

        private void ForceUnblockInput(string reason)
        {
            try
            {
                Mouse.blockInput(false);
                isInputCurrentlyBlocked = false;
                LogError($"EMERGENCY INPUT UNBLOCK: {reason} at {DateTime.Now:HH:mm:ss.fff}", 1);
            }
            catch (Exception ex)
            {
                // Last resort - log the error but we can't do much more
                LogError($"CRITICAL: Failed to force unblock input: {ex.Message}", 1);
            }
        }

        private IEnumerator InputSafetyCoroutine()
        {
            while (Settings.Enable)
            {
                bool shouldWaitLong = false;

                try
                {
                    PerformSafetyChecks();
                }
                catch (Exception ex)
                {
                    ForceUnblockInput($"InputSafetyCoroutine exception: {ex.Message}");
                    shouldWaitLong = true;
                }

                if (shouldWaitLong)
                {
                    yield return new WaitTime(1000); // Wait longer after an exception
                }
                else
                {
                    yield return new WaitTime(100); // Check every 100ms for responsiveness
                }
            }

            ForceUnblockInput("Plugin disabled - cleanup");
        }

        private void PerformSafetyChecks()
        {
#pragma warning disable CS0618
            bool currentHotkeyState = ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value);
#pragma warning restore CS0618

            if (currentHotkeyState != lastHotkeyState)
            {
                if (!currentHotkeyState)
                {
                    lastHotkeyReleaseTimer.Restart();
                    ///LogMessage($"Hotkey released at {DateTime.Now:HH:mm:ss.fff}", 3);
                }
                else
                {
                    lastHotkeyReleaseTimer.Stop();
                    ///LogMessage($"Hotkey pressed at {DateTime.Now:HH:mm:ss.fff}", 3);
                }
                lastHotkeyState = currentHotkeyState;
            }

            if (!currentHotkeyState && isInputCurrentlyBlocked &&
                lastHotkeyReleaseTimer.IsRunning &&
                lastHotkeyReleaseTimer.ElapsedMilliseconds > HOTKEY_RELEASE_FAILSAFE_MS)
            {
                ForceUnblockInput($"Hotkey released for {lastHotkeyReleaseTimer.ElapsedMilliseconds}ms");
            }

            if (isInputCurrentlyBlocked && GameController?.Game?.IngameState?.InGame != true)
            {
                ForceUnblockInput("Not in game");
            }
        }

        private IEnumerator ScanForAltarsLogic()
        {
            altarCoroutineTimer.Restart();
            altarService?.ProcessAltarScanningLogic(LogMessage, LogError);
            altarCoroutineTimer.Stop();
            lastAltarCoroutineMs = altarCoroutineTimer.Elapsed.TotalMilliseconds;

            altarCoroutine?.Pause();
            yield break;
        }





        private bool GroundItemsVisible()
        {
            if (CachedLabels?.Value?.Count < 1)
            {
                LogMessage("(ClickIt) No ground items found");

                return false;
            }

            return true;
        }





        public static List<Element> GetElementsByStringContains(Element label, string str)
        {
            return Services.ElementService.GetElementsByStringContains(label, str);
        }

        private bool workFinished;

        public override Job? Tick()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value))
#pragma warning restore CS0618 // Type or member is obsolete
            {

                if (clickLabelCoroutine?.IsDone == true)
                {
                    Coroutine firstOrDefault = Core.ParallelRunner.Coroutines.FirstOrDefault(x => x.Name == "ClickIt.ClickLogic");

                    if (firstOrDefault != null)
                    {
                        clickLabelCoroutine = firstOrDefault;
                    }
                }

                clickLabelCoroutine?.Resume();
                workFinished = false;
            }
            else
            {
                if (workFinished)
                {
                    clickLabelCoroutine?.Pause();
                }
            }
            if (SecondTimer.ElapsedMilliseconds > 500)
            {
                altarCoroutine?.Resume();
                SecondTimer.Restart();
            }
            return null;
        }

        // we need these here to keep the coroutines alive after finishing the work
        private IEnumerator MainClickLabelCoroutine()
        {
            while (Settings.Enable)
            {
                yield return ClickLabel();
            }
        }

        private IEnumerator MainScanForAltarsLogic()
        {
            while (Settings.Enable)
            {
                yield return ScanForAltarsLogic();
            }
        }







        private List<LabelOnGround> UpdateLabelComponent()
        {
            List<LabelOnGround> result = [];
            IList<LabelOnGround>? groundLabels = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabels;

            if (groundLabels == null)
            {
                return result;
            }

            result.Capacity = Math.Min(groundLabels.Count, 1000);

            for (int i = 0; i < groundLabels.Count; i++)
            {
                LabelOnGround label = groundLabels[i];
                if (label == null || label.ItemOnGround?.Path == null ||
                    !label.IsVisible || !label.Label.IsVisible)
                {
                    continue;
                }

                // Cache frequently accessed values
                Vector2 labelCenter = label.Label.GetClientRect().Center;
                if (!PointIsInClickableArea(labelCenter))
                {
                    continue;
                }

                Entity item = label.ItemOnGround;
                EntityType type = item.Type;
                string path = item.Path;

                // Check type and path conditions efficiently
                bool isValidType = type == EntityType.WorldItem ||
                                 (type == EntityType.Chest && !item.GetComponent<Chest>().OpenOnDamage) ||
                                 type == EntityType.AreaTransition;

                bool isValidPath = !string.IsNullOrEmpty(path) && (
                    path.Contains("DelveMineral") ||
                    path.Contains("AzuriteEncounterController") ||
                    path.Contains("Harvest/Irrigator") ||
                    path.Contains("Harvest/Extractor") ||
                    path.Contains(CleansingFireAltar) ||
                    path.Contains(TangleAltar) ||
                    path.Contains("CraftingUnlocks") ||
                    path.Contains(Brequel) ||
                    path.Contains(CrimsonIron) ||
                    path.Contains(CopperAltar) ||
                    path.Contains(PetrifiedWood) ||
                    path.Contains(Bismuth) ||
                    path.Contains(Verisium));

                if (isValidType || isValidPath || GetElementByString(label.Label, "The monster is imprisoned by powerful Essences.") != null)
                {
                    result.Add(label);
                }
            }

            // Sort by distance if we have items
            if (result.Count > 1)
            {
                result.Sort((a, b) => a.ItemOnGround.DistancePlayer.CompareTo(b.ItemOnGround.DistancePlayer));
            }

            return result;
        }

        private IEnumerator ClickLabel(Element? altar = null)
        {
            if (Timer.ElapsedMilliseconds < 60 + Random.Next(0, 10) || !canClick())
            {
                workFinished = true;
                yield break;
            }

            Timer.Restart();
            clickCoroutineTimer.Restart();

            if (altar != null)
            {
                // Handle altar clicks through the service
                yield return ProcessAltarClickSimple(altar);
            }
            else
            {
                // Handle regular clicks
                yield return ProcessRegularClickSimple();
            }

            clickCoroutineTimer.Stop();
            lastClickCoroutineMs = clickCoroutineTimer.Elapsed.TotalMilliseconds;
            workFinished = true;
        }

        private IEnumerator ProcessAltarClickSimple(Element altar)
        {
            if (!(inputHandler?.CanClick(GameController) ?? false))
            {
                yield break;
            }

            RectangleF windowArea = GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 clickPos = altar.GetClientRect().Center + windowTopLeft;

            if (Settings.BlockUserInput.Value)
            {
                SafeBlockInput(true);
            }

            ExileCore.Input.SetCursorPos(clickPos);
            if (Settings.LeftHanded.Value)
            {
                Mouse.RightClick();
            }
            else
            {
                Mouse.LeftClick();
            }

            SafeBlockInput(false);
            yield return new WaitTime(Random.Next(50, 150));
        }

        private IEnumerator ProcessRegularClickSimple()
        {
            if (!(inputHandler?.CanClick(GameController) ?? false))
            {
                yield break;
            }

            // First check for altars that need to be clicked
            yield return ProcessAltarClicking();

            // Then check for ground items
            if (!GroundItemsVisible())
            {
                yield break;
            }

            LabelOnGround? nextLabel = labelFilterService?.GetNextLabelToClick(CachedLabels?.Value ?? new List<LabelOnGround>());
            if (nextLabel == null)
            {
                yield break;
            }

            Entity item = nextLabel.ItemOnGround;
            if (item.DistancePlayer > Settings.ClickDistance)
            {
                yield break;
            }

            // Check if this is Verisium and handle it specially
            string path = item.Path ?? "";
            bool isVerisium = Settings.ClickVerisium.Value && path.Contains(Verisium);

            // Skip if this is an altar - altars are handled by ProcessAltarClicking()
            bool isAltar = path.Contains(CleansingFireAltar) || path.Contains(TangleAltar);
            if (isAltar)
            {
                yield break;
            }

            RectangleF windowArea = GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 clickPos = nextLabel.Label.GetClientRect().Center + windowTopLeft;

            if (isVerisium)
            {
                yield return ProcessVerisiumHoldClick(clickPos);
            }
            else
            {
                // Regular click logic
                if (Settings.BlockUserInput.Value)
                {
                    SafeBlockInput(true);
                }

                ExileCore.Input.SetCursorPos(clickPos);
                if (Settings.LeftHanded.Value)
                {
                    Mouse.RightClick();
                }
                else
                {
                    Mouse.LeftClick();
                }

                SafeBlockInput(false);
                yield return new WaitTime(Random.Next(50, 150));
            }
        }

        private IEnumerator ProcessAltarClicking()
        {
            List<PrimaryAltarComponent> altarSnapshot = altarService?.GetAltarComponents() ?? new List<PrimaryAltarComponent>();
            bool clickEater = Settings.ClickEaterAltars;
            bool clickExarch = Settings.ClickExarchAltars;
            bool leftHanded = Settings.LeftHanded;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                // Only process altars of the types we want to click
                if (!((altar.AltarType == AltarType.EaterOfWorlds && clickEater) ||
                      (altar.AltarType == AltarType.SearingExarch && clickExarch)))
                {
                    continue;
                }

                // Calculate weights and determine which button to click
                var altarWeights = CalculateAltarWeights(altar);
                RectangleF topModsRect = altar.TopMods.Element.GetClientRect();
                RectangleF bottomModsRect = altar.BottomMods.Element.GetClientRect();
                Vector2 topModsTopLeft = topModsRect.TopLeft;

                Element? boxToClick = DetermineAltarChoice(altar, altarWeights, topModsRect, bottomModsRect, topModsTopLeft);

                if (boxToClick != null &&
                    PointIsInClickableArea(boxToClick.GetClientRect().Center, altar.AltarType.ToString()) &&
                    boxToClick.IsVisible)
                {
                    // Click the determined button
                    RectangleF windowArea = GameController.Window.GetWindowRectangleTimeCache;
                    Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                    Vector2 clickPos = boxToClick.GetClientRect().Center + windowTopLeft;

                    if (Settings.BlockUserInput.Value)
                    {
                        SafeBlockInput(true);
                    }

                    ExileCore.Input.SetCursorPos(clickPos);

                    // Add a small delay to ensure cursor position is set
                    yield return new WaitTime(20);

                    if (leftHanded)
                    {
                        Mouse.RightClick();
                    }
                    else
                    {
                        Mouse.LeftClick();
                    }

                    SafeBlockInput(false);

                    // Only click one altar per cycle to prevent rapid clicking of multiple altars
                    yield return new WaitTime(Random.Next(200, 300));
                    yield break; // Exit after clicking one altar
                }
            }
        }
        private IEnumerator ProcessVerisiumHoldClick(Vector2 clickPos)
        {
            // Start holding if not already holding
            if (!isHoldingVerisiumClick)
            {
                LogMessage("Starting Verisium hold click", 3);

                if (Settings.BlockUserInput.Value)
                {
                    SafeBlockInput(true);
                }

                ExileCore.Input.SetCursorPos(clickPos);

                // Start holding left click (press down but don't release)
                Mouse.LeftMouseDown();

                isHoldingVerisiumClick = true;
                verisiumHoldTimer.Restart();
            }

            // Check if we should continue holding (hotkey still pressed and within failsafe time)
#pragma warning disable CS0618 // Type or member is obsolete
            bool hotkeyPressed = ExileCore.Input.GetKeyState(Settings.ClickLabelKey.Value);
#pragma warning restore CS0618 // Type or member is obsolete
            bool withinFailsafeTime = verisiumHoldTimer.ElapsedMilliseconds < VERISIUM_HOLD_FAILSAFE_MS;
            bool hasVerisiumOnScreen = labelFilterService?.HasVerisiumOnScreen(CachedLabels?.Value ?? new List<LabelOnGround>()) ?? false;

            if ((!hotkeyPressed || !withinFailsafeTime || !hasVerisiumOnScreen) && isHoldingVerisiumClick)
            {
                // Stop holding
                LogMessage($"Stopping Verisium hold click - Hotkey: {hotkeyPressed}, Time: {withinFailsafeTime}, HasVerisium: {hasVerisiumOnScreen}", 3);

                Mouse.LeftMouseUp();

                if (Settings.BlockUserInput.Value)
                {
                    SafeBlockInput(false);
                }

                isHoldingVerisiumClick = false;
                verisiumHoldTimer.Stop();
            }

            yield return new WaitTime(50); // Short delay for responsiveness
        }

        public static Element? GetElementByString(Element? root, string str)
        {
            return Services.ElementService.GetElementByString(root, str);
        }

        private decimal CalculateUpsideWeight(List<string> upsides)
        {
            decimal totalWeight = 0;
            if (upsides == null) return totalWeight;

            foreach (string upside in upsides)
            {
                if (string.IsNullOrEmpty(upside)) continue;

                // Use the ModTiers dictionary from settings instead of reflection
                int weight = Settings.GetModTier(upside);
                totalWeight += weight;
            }
            return totalWeight;
        }

        private decimal CalculateDownsideWeight(List<string> downsides)
        {
            decimal totalWeight = 1; // Start with 1 to avoid division by zero
            if (downsides == null) return totalWeight;

            foreach (string downside in downsides)
            {
                if (string.IsNullOrEmpty(downside)) continue;

                // Use the ModTiers dictionary from settings instead of reflection
                int weight = Settings.GetModTier(downside);
                totalWeight += weight;
            }
            return totalWeight;
        }

        public enum AltarType
        {
            SearingExarch,
            EaterOfWorlds,
            Unknown
        }
    }
}
