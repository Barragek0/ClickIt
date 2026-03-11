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
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace ClickIt.Services
{

    public partial class ClickService(
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
        private ulong _lastLeverKey;
        private long _lastLeverClickTimestampMs;

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

            foreach (PrimaryAltarComponent altar in altarSnapshot)
            {
                if (!TryGetClickableAltarElement(altar, clickEater, clickExarch, out Element? boxToClick))
                    continue;

                yield return ClickAltarElement(boxToClick, leftHanded);
            }
        }

        public bool HasClickableAltars()
        {
            if (altarService == null)
                return false;

            var altarSnapshot = altarService.GetAltarComponentsReadOnly();
            if (altarSnapshot.Count == 0)
                return false;

            bool clickEater = settings.ClickEaterAltars;
            bool clickExarch = settings.ClickExarchAltars;

            for (int i = 0; i < altarSnapshot.Count; i++)
            {
                if (TryGetClickableAltarElement(altarSnapshot[i], clickEater, clickExarch, out _))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
        {
            return TryGetClickableAltarElement(altar, clickEater, clickExarch, out _);
        }

        private bool TryGetClickableAltarElement(PrimaryAltarComponent altar, bool clickEater, bool clickExarch, [NotNullWhen(true)] out Element? boxToClick)
        {
            boxToClick = null;

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
            boxToClick = altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);

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
            var altarWeights = altar.GetCachedWeights(pc => weightCalculator.CalculateAltarWeights(pc));
            if (!altarWeights.HasValue) return null;

            RectangleF topModsRect = altar.GetTopModsRect();
            RectangleF bottomModsRect = altar.GetBottomModsRect();
            Vector2 topModsTopLeft = topModsRect.TopLeft;
            return altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);
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
            // Verify cursor is inside game window before clicking when setting enabled
            if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true)
            {
                if (!IsCursorInsideGameWindow())
                {
                    DebugLog(() => "[ClickAltarElement] Skipping click - cursor is outside the PoE window");
                    clicked = false;
                }
                else
                {
                    clicked = TryPerformClick(element, windowTopLeft);
                }
            }
            else
            {
                clicked = TryPerformClick(element, windowTopLeft);
            }

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
                PerformLockedClick(clickPos, el, gameController);
                performanceMonitor.RecordClickInterval();
                DebugLog(() => "[ClickAltarElement] Click performed");
                return true;
            }
        }

        private bool EnsureCursorInsideGameWindowForClick(string outsideWindowLogMessage)
        {
            if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(() => outsideWindowLogMessage);
                return false;
            }

            return true;
        }

        private void PerformLockedClick(Vector2 clickPos, Element? expectedElement, GameController? controller)
        {
            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                inputHandler.PerformClick(clickPos, expectedElement, controller);
            }
        }

        private bool IsCursorInsideGameWindow()
        {
            try
            {
                var winRect = gameController?.Window.GetWindowRectangleTimeCache;
                if (winRect == null) return true;
                var cursor = Mouse.GetCursorPosition();
                return cursor.X >= winRect.Value.X && cursor.Y >= winRect.Value.Y && cursor.X <= winRect.Value.X + winRect.Value.Width && cursor.Y <= winRect.Value.Y + winRect.Value.Height;
            }
            catch
            {
                // If we cannot determine the cursor/window bounds assume it's fine so we don't block clicks unexpectedly
                return true;
            }
        }

    }
}


