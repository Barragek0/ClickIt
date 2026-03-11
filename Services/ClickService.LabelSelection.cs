using System.Collections;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;
using ClickIt.Utils;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

#nullable enable

namespace ClickIt.Services
{
    public partial class ClickService
    {
        public IEnumerator ProcessRegularClick()
        {
            if (HasClickableAltars())
            {
                // If altars are present and clickable, only do altar clicking.
                yield return ProcessAltarClicking();
                yield break;
            }

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            // Handle the dedicated Ultimatum panel UI (post-round choices) before ground-label logic.
            if (TryHandleUltimatumPanelUi(windowTopLeft))
                yield break;

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

        private LabelOnGround? ResolveNextLabelCandidate(IReadOnlyList<LabelOnGround>? allLabels)
        {
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);
            return PreferUiHoverEssenceLabel(nextLabel, allLabels);
        }

        private LabelOnGround? PreferUiHoverEssenceLabel(LabelOnGround? nextLabel, IReadOnlyList<LabelOnGround>? allLabels)
        {
            // For essences: use the game's UIHoverElement as the authoritative front-most target.
            // If the player's current UIHover element corresponds to a visible label, prefer it over our candidate.
            if (nextLabel == null || !IsEssenceLabel(nextLabel) || allLabels == null)
                return nextLabel;

            var uiHover = gameController?.IngameState?.UIHoverElement;
            if (uiHover == null)
                return nextLabel;

            LabelOnGround? hovered = allLabels.FirstOrDefault(l => l?.Label != null && l.Label.Address == uiHover.Address);
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
            if (lbl == null || lbl.Label == null)
                return false;

            return LabelUtils.HasEssenceImprisonmentText(lbl);
        }

        private LabelOnGround? FindNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            int[] caps = [1, 5, 25, 100];
            foreach (int cap in caps)
            {
                int limit = Math.Min(cap, allLabels.Count);
                LabelOnGround? candidate = FindLabelInRange(allLabels, 0, limit);
                if (candidate != null)
                    return candidate;
            }

            // Fallback to full scan (rare).
            return FindLabelInRange(allLabels, 0, allLabels.Count);
        }

        private LabelOnGround? FindLabelInRange(IReadOnlyList<LabelOnGround> allLabels, int start, int endExclusive)
        {
            int currentStart = start;
            while (currentStart < endExclusive)
            {
                LabelOnGround? label = labelFilterService.GetNextLabelToClick(allLabels, currentStart, endExclusive - currentStart);
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

        private static int IndexOfLabelReference(IReadOnlyList<LabelOnGround> labels, LabelOnGround target, int start, int endExclusive)
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
            string path = item.Path ?? string.Empty;
            return path.Contains("CleansingFireAltar") || path.Contains("TangleAltar");
        }

        private bool TryCorruptEssence(LabelOnGround label, Vector2 windowTopLeft)
        {
            if (settings.ClickEssences && labelFilterService.ShouldCorruptEssence(label))
            {
                Vector2? corruptionPos = LabelFilterService.GetCorruptionClickPosition(label, windowTopLeft);
                if (corruptionPos.HasValue)
                {
                    if (!EnsureCursorInsideGameWindowForClick("[TryCorruptEssence] Skipping corruption click - cursor outside PoE window"))
                        return false;

                    DebugLog(() => $"[ProcessRegularClick] Corruption click at {corruptionPos.Value}");
                    PerformLockedClick(corruptionPos.Value, null, gameController);
                    performanceMonitor.RecordClickInterval();
                    return true;
                }
            }

            return false;
        }

        private bool PerformLabelClick(Vector2 clickPos, Element? expectedElement, GameController? controller)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelClick] Skipping label click - cursor outside PoE window"))
                return false;

            PerformLockedClick(clickPos, expectedElement, controller);

            // Record the click interval after the actual click.
            performanceMonitor.RecordClickInterval();
            return true;
        }
    }
}