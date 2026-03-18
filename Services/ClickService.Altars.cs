using System.Collections;
using ClickIt.Components;
using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using System.Diagnostics.CodeAnalysis;

namespace ClickIt.Services
{
    public partial class ClickService
    {
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
            bool clickEater = settings.ClickEaterAltars;
            bool clickExarch = settings.ClickExarchAltars;
            if (!ShouldEvaluateAltarScan(clickEater, clickExarch))
                return false;

            if (altarService == null)
                return false;

            var altarSnapshot = altarService.GetAltarComponentsReadOnly();
            if (altarSnapshot.Count == 0)
                return false;

            for (int i = 0; i < altarSnapshot.Count; i++)
            {
                if (TryGetClickableAltarElement(altarSnapshot[i], clickEater, clickExarch, out _))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldEvaluateAltarScan(bool clickEaterEnabled, bool clickExarchEnabled)
        {
            return clickEaterEnabled || clickExarchEnabled;
        }

        private bool TryClickManualCursorPreferredAltarOption(Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            var altarSnapshot = altarService.GetAltarComponentsReadOnly();
            if (altarSnapshot.Count == 0)
                return false;

            bool clickEater = settings.ClickEaterAltars;
            bool clickExarch = settings.ClickExarchAltars;

            for (int i = 0; i < altarSnapshot.Count; i++)
            {
                if (!TryGetClickableAltarElement(altarSnapshot[i], clickEater, clickExarch, out Element? boxToClick))
                    continue;

                if (boxToClick == null)
                    continue;

                RectangleF optionRect = boxToClick.GetClientRect();
                if (!IsPointInsideRectInEitherSpace(optionRect, cursorAbsolute, windowTopLeft))
                    continue;

                if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
                {
                    DebugLog(() => "[TryClickManualCursorPreferredAltarOption] Skipping click - cursor is outside the PoE window");
                    return false;
                }

                return TryPerformClick(boxToClick, windowTopLeft, allowWhenHotkeyInactive: true, avoidCursorMove: true);
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

            bool isEnabledType = (altar.AltarType == AltarType.EaterOfWorlds && clickEater)
                || (altar.AltarType == AltarType.SearingExarch && clickExarch);
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

            if ((altar.TopMods?.HasUnmatchedMods == true) || (altar.BottomMods?.HasUnmatchedMods == true))
            {
                DebugLog(() => "Skipping altar - Unmatched mods present");
                return false;
            }

            if (!AreBothAltarOptionsVisibleAndClickable(altar))
            {
                DebugLog(() => "Skipping altar - Top and bottom options are not both visible/clickable yet");
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

            boxToClick = altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);
            if (boxToClick == null)
            {
                DebugLog(() => "Skipping altar - No valid choice could be determined");
                return false;
            }

            if (!IsClickableInEitherSpace(boxToClick.GetClientRect().Center, altar.AltarType.ToString())
                || !boxToClick.IsVisible)
            {
                DebugLog(() => "Skipping altar - Choice is not clickable or visible");
                return false;
            }

            return true;
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

            bool clicked;
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

            if (!clicked)
                yield break;

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

        private static bool IsValidVisible(Element el)
            => el != null && el.IsValid && el.IsVisible;

        private bool AreBothAltarOptionsVisibleAndClickable(PrimaryAltarComponent altar)
        {
            string altarPath = altar.AltarType.ToString();
            bool topVisibleAndClickable = IsAltarOptionVisibleAndClickable(altar.TopMods?.Element, altarPath);
            bool bottomVisibleAndClickable = IsAltarOptionVisibleAndClickable(altar.BottomMods?.Element, altarPath);
            return AreBothAltarOptionsActionable(topVisibleAndClickable, bottomVisibleAndClickable);
        }

        internal static bool AreBothAltarOptionsActionable(bool topVisibleAndClickable, bool bottomVisibleAndClickable)
            => topVisibleAndClickable && bottomVisibleAndClickable;

        private bool IsAltarOptionVisibleAndClickable(Element? optionElement, string altarPath)
        {
            if (optionElement == null || !optionElement.IsValid || !optionElement.IsVisible)
                return false;

            RectangleF optionRect;
            try
            {
                optionRect = optionElement.GetClientRect();
            }
            catch
            {
                return false;
            }

            if (optionRect.Width <= 0 || optionRect.Height <= 0)
                return false;

            return IsClickableInEitherSpace(optionRect.Center, altarPath);
        }

        private bool IsValidVisibleUnderLock(Element el)
        {
            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                return el != null && el.IsValid && el.IsVisible;
            }
        }

        private bool TryPerformClick(
            Element el,
            Vector2 windowTopLeft,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
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
                PerformLockedClick(clickPos, el, gameController, forceUiHoverVerification: false, allowWhenHotkeyInactive: allowWhenHotkeyInactive, avoidCursorMove: avoidCursorMove);
                performanceMonitor.RecordClickInterval();
                DebugLog(() => "[ClickAltarElement] Click performed");
                return true;
            }
        }
    }
}