
using System.Collections;
using ExileCore;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ClickIt.Components;
using ClickIt.Utils;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;

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

            // Avoid materializing a list just to check presence; do a quick indexed scan instead
            bool anyAltarToClick = false;
            for (int i = 0; i < altarSnapshot.Count; i++)
            {
                if (ShouldClickAltar(altarSnapshot[i], clickEater, clickExarch))
                {
                    anyAltarToClick = true;
                    break;
                }
            }
            if (!anyAltarToClick)
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
            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                return el != null && el.IsValid && el.IsVisible;
            }
        }

        private bool TryPerformClick(Element el, Vector2 windowTopLeft)
        {
            using (LockManager.AcquireStatic(_elementAccessLock))
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

        public IEnumerator ProcessRegularClick()
        {

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

            var allLabels = cachedLabels?.Value;
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            // For essences: use the game's UIHoverElement as the authoritative front-most target.
            // If the player's current UIHover element corresponds to a visible label, prefer it
            // over our candidate.
            if (nextLabel != null && IsEssenceLabel(nextLabel))
            {
                var uiHover = gameController?.IngameState?.UIHoverElement;
                if (uiHover != null && allLabels != null)
                {
                    // Prefer the hovered label (if it matches any label in our scan)
                    var hovered = allLabels.FirstOrDefault(l => l?.Label != null && l.Label.Address == uiHover.Address);
                    if (hovered != null && !ReferenceEquals(hovered, nextLabel) && IsEssenceLabel(hovered))
                    {
                        DebugLog(() => "[ProcessRegularClick] UIHover-first: switching target to UIHover label");
                        nextLabel = hovered;
                    }
                }
            }

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


            if (TryCorruptEssence(nextLabel, windowTopLeft))
                yield break;

            Vector2 clickPos = inputHandler.CalculateClickPosition(nextLabel, windowTopLeft);
            PerformLabelClick(clickPos, nextLabel.Label, gameController);


            if (inputHandler.TriggerToggleItems())
            {
                yield return new WaitTime(20);
            }
        }

        private static bool IsEssenceLabel(LabelOnGround lbl)
        {
            if (lbl == null || lbl.Label == null) return false;
            return LabelUtils.HasEssenceImprisonmentText(lbl);
        }

        private LabelOnGround? FindNextLabelToClick(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null || allLabels.Count == 0) return null;

            int[] caps = [1, 5, 25, 100];
            foreach (int cap in caps)
            {
                int limit = Math.Min(cap, allLabels.Count);
                var label = labelFilterService.GetNextLabelToClick(allLabels, 0, limit);
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
                // If the corruption control (vaal orb element) is overlapped by any other label
                // on-screen, skip corrupting this essence now — it prevents the plugin from
                // attempting to click a target that is obscured by another label (causing
                // mis-clicks on the wrong UI element). See GitHub issue #27.
                if (IsCorruptionBlockedByOverlappingLabels(label, windowTopLeft))
                {
                    return false;
                }
                Vector2? corruptionPos = LabelFilterService.GetCorruptionClickPosition(label, windowTopLeft);
                if (corruptionPos.HasValue)
                {
                    DebugLog(() => $"[ProcessRegularClick] Corruption click at {corruptionPos.Value}");
                    using (LockManager.AcquireStatic(_elementAccessLock))
                    {
                        inputHandler.PerformClick(corruptionPos.Value);
                    }
                    performanceMonitor.RecordClickInterval();
                    return true;
                }
            }
            return false;
        }

        // Determine whether the corruption target of the given label is occluded by
        // any other label on-screen. This helps avoid clicking the wrong UI element
        // when labels overlap (see GitHub issue #27).
        private bool IsCorruptionBlockedByOverlappingLabels(LabelOnGround label, Vector2 windowTopLeft)
        {
            try
            {
                var corruptEl = label.Label?.GetChildAtIndex(2)?.GetChildAtIndex(0)?.GetChildAtIndex(0);
                if (corruptEl == null) return false;

                var corruptRect = corruptEl.GetClientRect();
                // translate to screen coords
                var targetRect = new SharpDX.RectangleF(corruptRect.X + windowTopLeft.X, corruptRect.Y + windowTopLeft.Y, corruptRect.Width, corruptRect.Height);

                var all = cachedLabels?.Value;
                if (all == null || all.Count == 0) return false;

                for (int i = 0; i < all.Count; i++)
                {
                    var other = all[i];
                    if (other == null || object.ReferenceEquals(other, label)) continue;
                    var otherEl = other.Label;
                    if (otherEl == null) continue;
                    var r = otherEl.GetClientRect();
                    var otherRect = new SharpDX.RectangleF(r.X + windowTopLeft.X, r.Y + windowTopLeft.Y, r.Width, r.Height);
                    if (AreRectanglesOverlapping(targetRect, otherRect))
                        return true;
                }

                return false;
            }
            catch
            {
                // Be defensive in case the Element graph access throws — prefer ignoring than clicking blindly
                return true;
            }
        }

        private static bool AreRectanglesOverlapping(SharpDX.RectangleF a, SharpDX.RectangleF b)
        {
            // Rectangles overlap when their projections on both axes intersect
            if (a.Right <= b.Left) return false;
            if (a.Left >= b.Right) return false;
            if (a.Bottom <= b.Top) return false;
            if (a.Top >= b.Bottom) return false;
            return true;
        }

        // Test-only helper moved to ClickService.Seams.cs

        private void PerformLabelClick(Vector2 clickPos, Element? expectedElement, GameController? gameController)
        {
            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                inputHandler.PerformClick(clickPos, expectedElement, gameController);
            }

            // Record the click interval after the actual click
            // This ensures we measure time between actual clicks, not between hotkey presses
            performanceMonitor.RecordClickInterval();
        }
    }
}
