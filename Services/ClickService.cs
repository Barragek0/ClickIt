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

        public IEnumerator ProcessRegularClick()
        {

            if (HasClickableAltars())
            {
                // If altars are present and clickable, only do altar clicking
                yield return ProcessAltarClicking();
                yield break;
            }

            // No clickable altars, check for shrines
            // Note: We can't access shrineService here, so this check is done in CoroutineManager

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            // Handle the dedicated Ultimatum panel UI (post-round choices) before ground-label logic.
            if (TryHandleUltimatumPanelUi(windowTopLeft))
                yield break;

            // No altars, proceed with item clicking
            if (!groundItemsVisible())
            {
                DebugLog(() => "[ProcessRegularClick] Ground items not visible, breaking");
                yield break;
            }

            var allLabels = cachedLabels?.Value;
            LabelOnGround? nextLabel = ResolveNextLabelCandidate(allLabels);

            if (nextLabel == null)
            {
                DebugLog(() => "[ProcessRegularClick] No label to click found, breaking");
                yield break;
            }

            if (ShouldSkipOrHandleSpecialLabel(nextLabel, windowTopLeft))
                yield break;

            Vector2 clickPos = inputHandler.CalculateClickPosition(nextLabel, windowTopLeft);
            bool clicked = PerformLabelClick(clickPos, nextLabel.Label, gameController);
            if (clicked)
            {
                MarkLeverClicked(nextLabel);
            }

            if (inputHandler.TriggerToggleItems())
            {
                yield return new WaitTime(20);
            }
        }

        private LabelOnGround? ResolveNextLabelCandidate(System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels)
        {
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);
            return PreferUiHoverEssenceLabel(nextLabel, allLabels);
        }

        private LabelOnGround? PreferUiHoverEssenceLabel(LabelOnGround? nextLabel, System.Collections.Generic.IReadOnlyList<LabelOnGround>? allLabels)
        {
            // For essences: use the game's UIHoverElement as the authoritative front-most target.
            // If the player's current UIHover element corresponds to a visible label, prefer it over our candidate.
            if (nextLabel == null || !IsEssenceLabel(nextLabel) || allLabels == null)
                return nextLabel;

            var uiHover = gameController?.IngameState?.UIHoverElement;
            if (uiHover == null)
                return nextLabel;

            var hovered = allLabels.FirstOrDefault(l => l?.Label != null && l.Label.Address == uiHover.Address);
            if (hovered != null && !ReferenceEquals(hovered, nextLabel) && IsEssenceLabel(hovered))
            {
                DebugLog(() => "[ProcessRegularClick] UIHover-first: switching target to UIHover label");
                return hovered;
            }

            return nextLabel;
        }

        private bool ShouldSkipOrHandleSpecialLabel(LabelOnGround nextLabel, Vector2 windowTopLeft)
        {
            if (IsAltarLabel(nextLabel))
            {
                DebugLog(() => "[ProcessRegularClick] Item is an altar, breaking");
                return true;
            }

            if (TryCorruptEssence(nextLabel, windowTopLeft))
                return true;

            if (!settings.ClickUltimatum.Value || !IsUltimatumLabel(nextLabel))
                return false;

            if (TryClickPreferredUltimatumModifier(nextLabel, windowTopLeft))
                return true;

            DebugLog(() => "[ProcessRegularClick] Ultimatum label detected but no preferred modifier matched; skipping generic label click");
            return true;
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
                LabelOnGround? candidate = FindLabelInRange(allLabels, 0, limit);
                if (candidate != null)
                    return candidate;
            }

            // Fallback to full scan (rare)
            return FindLabelInRange(allLabels, 0, allLabels.Count);
        }

        private LabelOnGround? FindLabelInRange(System.Collections.Generic.IReadOnlyList<LabelOnGround> allLabels, int start, int endExclusive)
        {
            int currentStart = start;
            while (currentStart < endExclusive)
            {
                var label = labelFilterService.GetNextLabelToClick(allLabels, currentStart, endExclusive - currentStart);
                if (label == null)
                    return null;

                if (!ShouldSuppressLeverClick(label) && !ShouldSuppressInactiveUltimatumLabel(label))
                    return label;

                int idx = IndexOfLabelReference(allLabels, label, currentStart, endExclusive);
                if (idx < 0)
                    return null;

                currentStart = idx + 1;
            }

            return null;
        }

        private static int IndexOfLabelReference(System.Collections.Generic.IReadOnlyList<LabelOnGround> labels, LabelOnGround target, int start, int endExclusive)
        {
            for (int i = start; i < endExclusive; i++)
            {
                if (ReferenceEquals(labels[i], target))
                    return i;
            }
            return -1;
        }

        private bool ShouldSuppressLeverClick(LabelOnGround label)
        {
            if (!settings.LazyMode.Value)
                return false;
            if (!IsLeverLabel(label))
                return false;

            int cooldownMs = settings.LazyModeLeverReclickDelay?.Value ?? 1200;
            ulong currentLeverKey = GetLeverIdentityKey(label);
            long now = Environment.TickCount64;

            return IsLeverClickSuppressedByCooldown(_lastLeverKey, _lastLeverClickTimestampMs, currentLeverKey, now, cooldownMs);
        }

        private static bool IsLeverClickSuppressedByCooldown(ulong lastLeverKey, long lastLeverClickTimestampMs, ulong currentLeverKey, long now, int cooldownMs)
        {
            if (cooldownMs <= 0)
                return false;
            if (currentLeverKey == 0 || lastLeverKey == 0)
                return false;
            if (currentLeverKey != lastLeverKey)
                return false;
            if (lastLeverClickTimestampMs <= 0)
                return false;

            long elapsed = now - lastLeverClickTimestampMs;
            return elapsed >= 0 && elapsed < cooldownMs;
        }

        private void MarkLeverClicked(LabelOnGround label)
        {
            if (!settings.LazyMode.Value)
                return;
            if (!IsLeverLabel(label))
                return;

            ulong key = GetLeverIdentityKey(label);
            if (key == 0)
                return;

            _lastLeverKey = key;
            _lastLeverClickTimestampMs = Environment.TickCount64;
        }

        private static bool IsLeverLabel(LabelOnGround? label)
        {
            string? path = label?.ItemOnGround?.Path;
            return !string.IsNullOrWhiteSpace(path)
                && path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);
        }

        private static ulong GetLeverIdentityKey(LabelOnGround label)
        {
            ulong itemAddress = unchecked((ulong)(label.ItemOnGround?.Address ?? 0));
            if (itemAddress != 0)
                return itemAddress;

            ulong elementAddress = unchecked((ulong)(label.Label?.Address ?? 0));
            if (elementAddress != 0)
                return elementAddress;

            return 0;
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
                Vector2? corruptionPos = LabelFilterService.GetCorruptionClickPosition(label, windowTopLeft);
                if (corruptionPos.HasValue)
                {
                    // Respect setting: ensure cursor currently in PoE window before attempting a click
                    if (!EnsureCursorInsideGameWindowForClick("[TryCorruptEssence] Skipping corruption click - cursor outside PoE window"))
                    {
                        return false;
                    }
                    DebugLog(() => $"[ProcessRegularClick] Corruption click at {corruptionPos.Value}");
                    PerformLockedClick(corruptionPos.Value, null, gameController);
                    performanceMonitor.RecordClickInterval();
                    return true;
                }
            }
            return false;
        }

        private bool PerformLabelClick(Vector2 clickPos, Element? expectedElement, GameController? gameController)
        {
            // Optionally skip clicks if the OS cursor is outside the PoE window
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelClick] Skipping label click - cursor outside PoE window"))
            {
                return false;
            }
            PerformLockedClick(clickPos, expectedElement, gameController);

            // Record the click interval after the actual click
            // This ensures we measure time between actual clicks, not between hotkey presses
            performanceMonitor.RecordClickInterval();
            return true;
        }
    }
}


