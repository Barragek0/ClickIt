
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
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using Serilog;

#nullable enable

namespace ClickIt.Services
{

    public class ClickService(
        ClickItSettings settings,
        GameController gameController,
        ErrorHandler errorHandler,
        AltarService altarService,
        WeightCalculator weightCalculator,
        Rendering.AltarDisplayRenderer altarDisplayRenderer,
        Func<Vector2, string, bool> pointIsInClickableArea,
        InputHandler inputHandler,
        LabelFilterService labelFilterService,
        Func<bool> groundItemsVisible,
        TimeCache<List<LabelOnGround>> cachedLabels,
        PerformanceMonitor performanceMonitor)
    {
        private readonly ClickItSettings settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly GameController gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
        private readonly ErrorHandler errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        private readonly AltarService altarService = altarService ?? throw new ArgumentNullException(nameof(altarService));
        private readonly WeightCalculator weightCalculator = weightCalculator ?? throw new ArgumentNullException(nameof(weightCalculator));
        private readonly Rendering.AltarDisplayRenderer altarDisplayRenderer = altarDisplayRenderer ?? throw new ArgumentNullException(nameof(altarDisplayRenderer));
        private readonly Func<Vector2, string, bool> pointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));
        private readonly InputHandler inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
        private readonly LabelFilterService labelFilterService = labelFilterService ?? throw new ArgumentNullException(nameof(labelFilterService));
        private readonly Func<bool> groundItemsVisible = groundItemsVisible ?? throw new ArgumentNullException(nameof(groundItemsVisible));
        private readonly TimeCache<List<LabelOnGround>> cachedLabels = cachedLabels;
        private readonly PerformanceMonitor performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));

        // Thread safety lock to prevent race conditions during element access
        private readonly object _elementAccessLock = new();
        // Public method to expose the lock for external synchronization
        public object GetElementAccessLock()
        {
            return _elementAccessLock;
        }

        // Helper to avoid allocating debug message strings when debug logging is disabled
        private void DebugLog(Func<string> messageFactory)
        {
            if (settings.DebugMode?.Value == true)
            {
                errorHandler.LogMessage(messageFactory());
            }
        }

        public IEnumerator ProcessAltarClicking()
        {
            var altarSnapshot = altarService.GetAltarComponentsReadOnly();
            if (altarSnapshot.Count == 0)
                yield break;

            bool clickEater = settings.ClickEaterAltars;
            bool clickExarch = settings.ClickExarchAltars;
            bool leftHanded = settings.LeftHanded;

            var altarsToClick = altarSnapshot.Where(altar => ShouldClickAltar(altar, clickEater, clickExarch)).ToList();
            if (altarsToClick.Count == 0)
                yield break;

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                if (!ShouldClickAltar(altar, clickEater, clickExarch))
                    continue;

                Element? boxToClick = GetAltarElementToClick(altar);
                if (boxToClick == null)
                    continue;

                yield return ClickAltarElement(boxToClick, leftHanded);
            }
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
                DebugLog(() => "Skipping altar - Validation failed");
                return false;
            }

            if ((altar.TopMods?.Element?.IsValid != true) || (altar.BottomMods?.Element?.IsValid != true))
            {
                DebugLog(() => "Skipping altar - Elements are not valid");
                return false;
            }

            // Check for unmatched mods
            if ((altar.TopMods?.HasUnmatchedMods == true) || (altar.BottomMods?.HasUnmatchedMods == true))
            {
                DebugLog(() => "Skipping altar - Unmatched mods present");
                return false;
            }

            var altarWeights = altar.GetCachedWeights(pc => weightCalculator.CalculateAltarWeights(pc));
            if (!altarWeights.HasValue)
            {
                DebugLog(() => "Skipping altar - Weight calculation failed");
                return false;
            }

            RectangleF topModsRect = altar.GetTopModsRect();
            RectangleF bottomModsRect = altar.GetBottomModsRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;

            // Check if we can determine a valid choice
            Element? boxToClick = altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);

            if (boxToClick == null)
            {
                DebugLog(() => "Skipping altar - No valid choice could be determined");
                return false;
            }

            // Final check: ensure the choice is clickable
            if (!pointIsInClickableArea(boxToClick.GetClientRect().Center, altar.AltarType.ToString()) ||
                !boxToClick.IsVisible)
            {
                DebugLog(() => "Skipping altar - Choice is not clickable or visible");
                return false;
            }

            return true;
        }

        public Element? GetAltarElementToClick(PrimaryAltarComponent altar)
        {
            // All validation is now done in ShouldClickAltar, so we can proceed directly
            var altarWeights = altar.GetCachedWeights(pc => weightCalculator.CalculateAltarWeights(pc));
            if (!altarWeights.HasValue) return null;

            RectangleF topModsRect = altar.GetTopModsRect();
            RectangleF bottomModsRect = altar.GetBottomModsRect();

            Vector2 topModsTopLeft = topModsRect.TopLeft;

            // We know this will succeed since ShouldClickAltar already validated it
            Element? boxToClick = altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);

            return boxToClick;
        }

        private IEnumerator ClickAltarElement(Element element, bool leftHanded)
        {
            DebugLog(() => "[ClickAltarElement] Starting");

            if (element == null)
            {
                errorHandler.LogError("CRITICAL: Altar element is null", 10);
                yield break;
            }

            if (!IsValidVisible(element))
            {
                errorHandler.LogError("CRITICAL: Altar element invalid or not visible before click", 10);
                yield break;
            }

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            bool clicked = false;
            clicked = TryPerformClick(element, windowTopLeft);

            if (clicked)
            {
                yield return new WaitTime(70);

                bool stillVisible = IsValidVisibleUnderLock(element);
                if (!stillVisible)
                {
                    altarService.RemoveAltarComponentsByElement(element);
                    DebugLog(() => "[ClickAltarElement] Removed clicked altar from tracking (no longer visible)");
                }
                else
                {
                    DebugLog(() => "[ClickAltarElement] Altar still visible after click; not removing (possible missclick)");
                }
            }
        }

        private static bool IsValidVisible(Element el)
        {
            return el != null && el.IsValid && el.IsVisible;
        }

        private bool IsValidVisibleUnderLock(Element el)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (LockManager.Acquire(_elementAccessLock))
                {
                    return el != null && el.IsValid && el.IsVisible;
                }
            }
            return el != null && el.IsValid && el.IsVisible;
        }

        private bool TryPerformClick(Element el, Vector2 windowTopLeft)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (LockManager.Acquire(_elementAccessLock))
                {
                    if (!IsValidVisible(el))
                    {
                        errorHandler.LogError("[ClickAltarElement] Element became invalid or invisible before click", 10);
                        return false;
                    }
                    RectangleF r = el.GetClientRect();
                    Vector2 clickPos = r.Center + windowTopLeft;
                    inputHandler.PerformClick(clickPos, el, gameController);
                    performanceMonitor.RecordClickInterval();
                    DebugLog(() => "[ClickAltarElement] Click performed");
                    return true;
                }
            }

            // No lock path
            if (!IsValidVisible(el))
            {
                errorHandler.LogError("[ClickAltarElement] Element became invalid or invisible before click", 10);
                return false;
            }
            RectangleF rect = el.GetClientRect();
            Vector2 pos = rect.Center + windowTopLeft;
            inputHandler.PerformClick(pos, el, gameController);
            performanceMonitor.RecordClickInterval();
            DebugLog(() => "[ClickAltarElement] Click performed");
            return true;
        }

        public IEnumerator ProcessRegularClick()
        {
            DebugLog(() => "[ProcessRegularClick] Starting process regular click");

            // Check if there are clickable altars
            var altarSnapshot = altarService.GetAltarComponentsReadOnly();
            bool clickEater = settings.ClickEaterAltars;
            bool clickExarch = settings.ClickExarchAltars;
            bool hasClickableAltars = altarSnapshot.Any(altar => ShouldClickAltar(altar, clickEater, clickExarch));

            if (hasClickableAltars)
            {
                // If altars are present and clickable, only do altar clicking
                yield return ProcessAltarClicking();
                yield break;
            }

            // No clickable altars, check for shrines
            // Note: We can't access shrineService here, so this check is done in CoroutineManager

            // No altars, proceed with item clicking
            if (!groundItemsVisible())
            {
                DebugLog(() => "[ProcessRegularClick] Ground items not visible, breaking");
                yield break;
            }

            var allLabels = cachedLabels?.Value ?? [];
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);

            if (nextLabel == null)
            {
                DebugLog(() => "[ProcessRegularClick] No label to click found, breaking");
                yield break;
            }

            if (IsAltarLabel(nextLabel))
            {
                DebugLog(() => "[ProcessRegularClick] Item is an altar, breaking");
                yield break;
            }

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            if (TryCorruptEssence(nextLabel, windowTopLeft))
                yield break;

            Vector2 clickPos = inputHandler.CalculateClickPosition(nextLabel, windowTopLeft);
            PerformLabelClick(clickPos, nextLabel.Label, gameController);

            DebugLog(() => $"[ProcessRegularClick] Clicked label at distance {nextLabel.ItemOnGround.DistancePlayer:F1}");

            if (inputHandler.TriggerToggleItems())
            {
                yield return new WaitTime(20);
            }
        }

        private LabelOnGround? FindNextLabelToClick(List<LabelOnGround> allLabels)
        {
            if (allLabels.Count == 0) return null;

            int[] caps = [1, 5, 25, 100];
            foreach (int cap in caps)
            {
                int limit = Math.Min(cap, allLabels.Count);
                /*for (int i = 0; i < limit; i++)
                {
                    var it = allLabels[i].ItemOnGround;
                    string path = it?.Path ?? string.Empty;
                    if (!string.IsNullOrEmpty(path))
                    {
                        float dist = it?.DistancePlayer ?? 0f;
                        DebugLog(() => $"[LabelPaths{cap}] {path} (dist={dist:F1})");
                    }
                    else
                    {
                        DebugLog(() => $"[LabelPaths{cap}] <no path> (dist={it?.DistancePlayer ?? 0f:F1})");
                    }
                }*/

                var slice = allLabels.GetRange(0, limit);
                var label = labelFilterService.GetNextLabelToClick(slice);
                if (label != null)
                    return label;
            }

            // Fallback to full scan (rare)
            return labelFilterService.GetNextLabelToClick(allLabels);
        }

        private static bool IsAltarLabel(LabelOnGround label)
        {
            var item = label.ItemOnGround;
            string path = item.Path ?? "";
            return path.Contains("CleansingFireAltar") || path.Contains("TangleAltar");
        }

        private bool TryCorruptEssence(LabelOnGround label, Vector2 windowTopLeft)
        {
            if (settings.ClickEssences && labelFilterService.ShouldCorruptEssence(label))
            {
                Vector2? corruptionPos = labelFilterService.GetCorruptionClickPosition(label, windowTopLeft);
                if (corruptionPos.HasValue)
                {
                    DebugLog(() => $"[ProcessRegularClick] Corruption click at {corruptionPos.Value}");
                    var gm = LockManager.Instance;
                    if (gm != null)
                    {
                        using (LockManager.Acquire(_elementAccessLock))
                        {
                            inputHandler.PerformClick(corruptionPos.Value);
                        }
                    }
                    else
                    {
                        inputHandler.PerformClick(corruptionPos.Value);
                    }
                    performanceMonitor.RecordClickInterval();
                    return true;
                }
            }
            return false;
        }

        private void PerformLabelClick(Vector2 clickPos, Element? expectedElement, GameController? gameController)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (LockManager.Acquire(_elementAccessLock))
                {
                    inputHandler.PerformClick(clickPos, expectedElement, gameController);
                }
            }
            else
            {
                inputHandler.PerformClick(clickPos, expectedElement, gameController);
            }

            // Record the click interval after the actual click
            // This ensures we measure time between actual clicks, not between hotkey presses
            performanceMonitor.RecordClickInterval();
        }
    }
}
