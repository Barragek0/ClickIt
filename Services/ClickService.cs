
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExileCore;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ClickIt.Components;
using ClickIt.Utils;
using SharpDX;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

#nullable enable

namespace ClickIt.Services
{

    public class ClickService
    {
        private readonly ClickItSettings settings;
        private readonly GameController gameController;
        private readonly Action<string> logMessage;
        private readonly Action<string, int> logError;
        private readonly AltarService altarService;
        private readonly WeightCalculator weightCalculator;
        private readonly Rendering.AltarDisplayRenderer altarDisplayRenderer;
        private readonly Func<Vector2, string, bool> pointIsInClickableArea;
        private readonly InputHandler inputHandler;
        private readonly LabelFilterService labelFilterService;
        private readonly Func<bool> groundItemsVisible;
        private readonly TimeCache<List<LabelOnGround>> cachedLabels;

        // Thread safety lock to prevent race conditions during element access
        private readonly object _elementAccessLock = new object();
        // Public method to expose the lock for external synchronization
        public object GetElementAccessLock()
        {
            return _elementAccessLock;
        }


        public ClickService(
            ClickItSettings settings,
            GameController gameController,
            Action<string> logMessage,
            Action<string, int> logError,
            AltarService altarService,
            WeightCalculator weightCalculator,
            Rendering.AltarDisplayRenderer altarDisplayRenderer,
            Func<Vector2, string, bool> pointIsInClickableArea,
            InputHandler inputHandler,
            LabelFilterService labelFilterService,
            Func<bool> groundItemsVisible,
            TimeCache<List<LabelOnGround>> cachedLabels)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
            this.logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
            this.logError = logError ?? throw new ArgumentNullException(nameof(logError));
            this.altarService = altarService ?? throw new ArgumentNullException(nameof(altarService));
            this.weightCalculator = weightCalculator ?? throw new ArgumentNullException(nameof(weightCalculator));
            this.altarDisplayRenderer = altarDisplayRenderer ?? throw new ArgumentNullException(nameof(altarDisplayRenderer));
            this.pointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));
            this.inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
            this.labelFilterService = labelFilterService ?? throw new ArgumentNullException(nameof(labelFilterService));
            this.groundItemsVisible = groundItemsVisible ?? throw new ArgumentNullException(nameof(groundItemsVisible));
            this.cachedLabels = cachedLabels;
        }

        public IEnumerator ProcessAltarClicking()
        {
            logMessage("[ProcessAltarClicking] Starting altar clicking process...");

            var altarSnapshot = altarService.GetAltarComponentsReadOnly();
            logMessage($"[ProcessAltarClicking] Retrieved {altarSnapshot.Count} altar snapshots");

            if (altarSnapshot.Count == 0)
            {
                logMessage("[ProcessAltarClicking] No altars found, breaking");
                yield break;
            }

            bool clickEater = settings.ClickEaterAltars;
            bool clickExarch = settings.ClickExarchAltars;
            bool leftHanded = settings.LeftHanded;

            logMessage($"[ProcessAltarClicking] Settings - Eater: {clickEater}, Exarch: {clickExarch}, LeftHanded: {leftHanded}");

            var altarsToClick = altarSnapshot.Where(altar => ShouldClickAltar(altar, clickEater, clickExarch)).ToList();
            logMessage($"[ProcessAltarClicking] Found {altarsToClick.Count} altars to click");

            foreach (PrimaryAltarComponent altar in altarsToClick)
            {
                logMessage($"[ProcessAltarClicking] Processing altar: {altar.AltarType}");

                Element boxToClick = GetAltarElementToClick(altar);
                if (boxToClick != null)
                {
                    logMessage($"[ProcessAltarClicking] Clicking altar element, type: {altar.AltarType}");
                    yield return ClickAltarElement(boxToClick, leftHanded);
                    logMessage($"[ProcessAltarClicking] Finished clicking altar: {altar.AltarType}");
                }
                else
                {
                    logMessage($"[ProcessAltarClicking] No clickable element found for altar: {altar.AltarType}");
                }
            }

            logMessage("[ProcessAltarClicking] Completed altar clicking process");
        }

        public bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
        {
            // First check if altar type is enabled
            bool isEnabledType = (altar.AltarType == ClickIt.AltarType.EaterOfWorlds && clickEater) ||
                                (altar.AltarType == ClickIt.AltarType.SearingExarch && clickExarch);

            if (!isEnabledType)
                return false;

            if (!altar.IsValidCached())
            {
                logMessage("Skipping altar - Validation failed");
                return false;
            }

            if ((altar.TopMods?.Element?.IsValid != true) || (altar.BottomMods?.Element?.IsValid != true))
            {
                logMessage("Skipping altar - Elements are not valid");
                return false;
            }

            // Check for unmatched mods
            if ((altar.TopMods?.HasUnmatchedMods == true) || (altar.BottomMods?.HasUnmatchedMods == true))
            {
                logMessage("Skipping altar - Unmatched mods present");
                return false;
            }

            var altarWeights = altar.GetCachedWeights(pc => weightCalculator.CalculateAltarWeights(pc));
            if (!altarWeights.HasValue)
            {
                logMessage("Skipping altar - Weight calculation failed");
                return false;
            }

            RectangleF topModsRect = altar.GetTopModsRect();
            RectangleF bottomModsRect = altar.GetBottomModsRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;

            // Check if we can determine a valid choice
            Element? boxToClick = altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);

            if (boxToClick == null)
            {
                logMessage("Skipping altar - No valid choice could be determined");
                return false;
            }

            // Final check: ensure the choice is clickable
            if (!pointIsInClickableArea(boxToClick.GetClientRect().Center, altar.AltarType.ToString()) ||
                !boxToClick.IsVisible)
            {
                logMessage("Skipping altar - Choice is not clickable or visible");
                return false;
            }

            return true;
        }

        public Element GetAltarElementToClick(PrimaryAltarComponent altar)
        {
            // All validation is now done in ShouldClickAltar, so we can proceed directly
            var altarWeights = altar.GetCachedWeights(pc => weightCalculator.CalculateAltarWeights(pc));
            if (!altarWeights.HasValue) return null;

            RectangleF topModsRect = altar.GetTopModsRect();
            RectangleF bottomModsRect = altar.GetBottomModsRect();

            Vector2 topModsTopLeft = topModsRect.TopLeft;

            // We know this will succeed since ShouldClickAltar already validated it
            Element? boxToClick = altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);

            return boxToClick ?? throw new InvalidOperationException("Altar choice should not be null when ShouldClickAltar returns true");
        }

        private IEnumerator ClickAltarElement(Element element, bool leftHanded)
        {
            logMessage("[ClickAltarElement] === STARTING ALTAR CLICK PROCESS ===");

            if (element == null)
            {
                logError("CRITICAL: Altar element is null", 10);
                yield break;
            }

            logMessage("[ClickAltarElement] Element validation starting...");
            if (!element.IsValid)
            {
                logError("CRITICAL: Altar element is not valid", 10);
                yield break;
            }

            if (!element.IsVisible)
            {
                logError("CRITICAL: Altar element is not visible", 10);
                yield break;
            }
            logMessage("[ClickAltarElement] Element validation completed successfully");

            logMessage("[ClickAltarElement] Getting window rectangle...");
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            logMessage($"[ClickAltarElement] Window rectangle: {windowArea}");

            // Calculate initial click position
            logMessage("[ClickAltarElement] Calculating click position...");
            RectangleF elementRect = element.GetClientRect();
            Vector2 clickPos = elementRect.Center + windowTopLeft;
            logMessage($"[ClickAltarElement] Click position calculated: {clickPos}");

            bool didClick = false;
            Vector2 clickPosUsed = clickPos;

            // Perform the click while holding the element access lock to avoid races
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_elementAccessLock))
                {
                    logMessage("[ClickAltarElement] Lock acquired, performing click...");
                    try
                    {
                        logMessage("[ClickAltarElement] Re-validating element inside lock before click...");
                        if (!element.IsValid || !element.IsVisible)
                        {
                            logError("[ClickAltarElement] Element became invalid or invisible before click", 10);
                        }
                        else
                        {
                            RectangleF elementRectInside = element.GetClientRect();
                            clickPosUsed = elementRectInside.Center + windowTopLeft;
                            logMessage($"[ClickAltarElement] Recomputed click pos: {clickPosUsed}");

                            logMessage("[ClickAltarElement] === ABOUT TO PERFORM CLICK - THIS IS WHERE FREEZE MAY OCCUR ===");
                            inputHandler.PerformClick(clickPosUsed);
                            logMessage("[ClickAltarElement] Click performed successfully");
                            didClick = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        logError($"[ClickAltarElement] Exception during click: {ex.GetType().Name}: {ex.Message}", 10);
                        try { logError($"[ClickAltarElement] Stack: {ex.StackTrace}", 10); } catch { }
                    }
                }
            }
            else
            {
                // Locking disabled - perform click without synchronization
                logMessage("[ClickAltarElement] Locking disabled, performing click without lock...");
                try
                {
                    logMessage("[ClickAltarElement] Re-validating element before click...");
                    if (!element.IsValid || !element.IsVisible)
                    {
                        logError("[ClickAltarElement] Element became invalid or invisible before click", 10);
                    }
                    else
                    {
                        RectangleF elementRectInside = element.GetClientRect();
                        clickPosUsed = elementRectInside.Center + windowTopLeft;
                        logMessage($"[ClickAltarElement] Recomputed click pos: {clickPosUsed}");

                        logMessage("[ClickAltarElement] === ABOUT TO PERFORM CLICK - THIS IS WHERE FREEZE MAY OCCUR ===");
                        inputHandler.PerformClick(clickPosUsed);
                        logMessage("[ClickAltarElement] Click performed successfully");
                        didClick = true;
                    }
                }
                catch (Exception ex)
                {
                    logError($"[ClickAltarElement] Exception during click: {ex.GetType().Name}: {ex.Message}", 10);
                    try { logError($"[ClickAltarElement] Stack: {ex.StackTrace}", 10); } catch { }
                }
            }

            if (didClick)
            {
                logMessage("[ClickAltarElement] === CLICK COMPLETED - ADDING DELAY TO PREVENT INTERFERENCE ===");
                yield return new WaitTime(70);
                logMessage("[ClickAltarElement] Delay completed after altar click");
            }

            logMessage("[ClickAltarElement] Yield completed");
        }

        public IEnumerator ProcessRegularClick()
        {
            logMessage("[ProcessRegularClick] ==================== STARTING REGULAR CLICK PROCESS ====================");

            // Process altar clicking first
            logMessage("[ProcessRegularClick] Processing altar clicking...");
            yield return ProcessAltarClicking();
            logMessage("[ProcessRegularClick] Altar clicking completed");
            logMessage("[ProcessRegularClick] Back from altar clicking, continuing with regular items...");
            if (!groundItemsVisible())
            {
                logMessage("[ProcessRegularClick] Ground items not visible, breaking");
                yield break;
            }

            logMessage("[ProcessRegularClick] Getting cached labels...");
            var allLabels = cachedLabels?.Value ?? new List<LabelOnGround>();
            logMessage($"[ProcessRegularClick] Retrieved {allLabels.Count} cached labels");

            logMessage("[ProcessRegularClick] Getting next label to click...");
            LabelOnGround? nextLabel = labelFilterService.GetNextLabelToClick(allLabels);

            if (nextLabel == null)
            {
                logMessage("[ProcessRegularClick] No label to click found, breaking");
                yield break;
            }
            logMessage("[ProcessRegularClick] Found label to click");

            Entity item = nextLabel.ItemOnGround;
            string path = item.Path ?? "";
            bool isAltar = path.Contains("CleansingFireAltar") || path.Contains("TangleAltar");

            if (isAltar)
            {
                logMessage("[ProcessRegularClick] Item is an altar, breaking");
                yield break;
            }

            logMessage("[ProcessRegularClick] Getting window rectangle...");
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            logMessage($"[ProcessRegularClick] Window rectangle: {windowArea}");

            // Handle essence corruption
            if (settings.ClickEssences && labelFilterService.ShouldCorruptEssence(nextLabel))
            {
                logMessage("[ProcessRegularClick] Handling essence corruption...");
                Vector2? corruptionPos = labelFilterService.GetCorruptionClickPosition(nextLabel, windowTopLeft);
                if (corruptionPos.HasValue)
                {
                    logMessage("[ProcessRegularClick] === ACQUIRING LOCK FOR CORRUPTION CLICK ===");
                    // Thread-safe locking to prevent race conditions with altar clicking
                    var gm = global::ClickIt.Utils.LockManager.Instance;
                    if (gm != null)
                    {
                        using (gm.Acquire(_elementAccessLock))
                        {
                            logMessage($"[ProcessRegularClick] Performing corruption click at {corruptionPos.Value}");
                            inputHandler.PerformClick(corruptionPos.Value);
                            logMessage("[ProcessRegularClick] Corruption click performed successfully");
                        }
                    }
                    else
                    {
                        logMessage($"[ProcessRegularClick] Performing corruption click at {corruptionPos.Value}");
                        inputHandler.PerformClick(corruptionPos.Value);
                        logMessage("[ProcessRegularClick] Corruption click performed successfully");
                    }
                    yield break;
                }
            }

            // Use proper click position calculation (includes chest height offset)
            logMessage("[ProcessRegularClick] Calculating click position...");
            Vector2 clickPos = inputHandler.CalculateClickPosition(nextLabel, windowTopLeft);
            logMessage($"[ProcessRegularClick] Click position calculated: {clickPos}");

            logMessage("[ProcessRegularClick] === ACQUIRING LOCK FOR REGULAR CLICK ===");
            // Thread-safe locking to prevent race conditions with altar clicking
            var gm2 = global::ClickIt.Utils.LockManager.Instance;
            if (gm2 != null)
            {
                using (gm2.Acquire(_elementAccessLock))
                {
                    logMessage("[ProcessRegularClick] Lock acquired, performing regular click...");
                    inputHandler.PerformClick(clickPos);
                    logMessage("[ProcessRegularClick] Click performed successfully");
                }
            }
            else
            {
                logMessage("[ProcessRegularClick] Locking disabled, performing regular click without lock...");
                inputHandler.PerformClick(clickPos);
                logMessage("[ProcessRegularClick] Click performed successfully");
            }

            if (inputHandler.TriggerToggleItems())
            {
                logMessage("[ProcessRegularClick] Triggering toggle items...");
                yield return new WaitTime(20);
                logMessage("[ProcessRegularClick] Toggle items completed");
            }

            logMessage("[ProcessRegularClick] Regular click process completed");
        }
    }
}
