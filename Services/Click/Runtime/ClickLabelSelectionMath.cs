using ExileCore.PoEMemory.Elements;
using ClickIt.Utils;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services.Click.Runtime
{
    internal static class ClickLabelSelectionMath
    {
        internal static bool ShouldContinuePathingForSpecialAltarLabel(
            bool walkTowardOffscreenLabelsEnabled,
            bool hasBackingEntity,
            bool isBackingEntityHidden,
            bool hasClickableAltars)
        {
            return walkTowardOffscreenLabelsEnabled
                && hasBackingEntity
                && !isBackingEntityHidden
                && !hasClickableAltars;
        }

        internal static bool IsEssenceLabel(LabelOnGround lbl)
        {
            if (lbl == null || lbl.Label == null)
                return false;

            return LabelUtils.HasEssenceImprisonmentText(lbl);
        }

        internal static bool ShouldAttemptSpecialEssenceCorruption(bool corruptionPointInWindow, bool corruptionPointClickable)
            => corruptionPointInWindow && corruptionPointClickable;

        internal static int GetGroundLabelSearchLimit(int totalVisibleLabels)
            => Math.Max(0, totalVisibleLabels);

        internal static LabelOnGround? FindLabelByAddress(IReadOnlyList<LabelOnGround> labels, long address)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label?.Label != null && label.Label.Address == address)
                    return label;
            }

            return null;
        }

        internal static int IndexOfLabelReference(IReadOnlyList<LabelOnGround> labels, LabelOnGround target, int start, int endExclusive)
        {
            for (int i = start; i < endExclusive; i++)
            {
                if (ReferenceEquals(labels[i], target))
                    return i;
            }

            return -1;
        }

        internal static bool IsLeverClickSuppressedByCooldown(ulong lastLeverKey, long lastLeverClickTimestampMs, ulong currentLeverKey, long now, int cooldownMs)
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

        internal static bool IsLeverLabel(LabelOnGround? label)
        {
            string? path = label?.ItemOnGround?.Path;
            return !string.IsNullOrWhiteSpace(path)
                && path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);
        }

        internal static ulong GetLeverIdentityKey(LabelOnGround label)
        {
            ulong itemAddress = unchecked((ulong)(label.ItemOnGround?.Address ?? 0));
            if (itemAddress != 0)
                return itemAddress;

            ulong elementAddress = unchecked((ulong)(label.Label?.Address ?? 0));
            if (elementAddress != 0)
                return elementAddress;

            return 0;
        }

        internal static bool IsAltarLabel(LabelOnGround label)
        {
            var item = label.ItemOnGround;
            string path = item.Path ?? string.Empty;
            return path.Contains("CleansingFireAltar") || path.Contains("TangleAltar");
        }

        internal static bool IsInsideWindowInEitherSpace(Vector2 point, RectangleF windowArea)
        {
            bool inClientSpace = point.X >= 0f
                && point.Y >= 0f
                && point.X <= windowArea.Width
                && point.Y <= windowArea.Height;

            bool inScreenSpace = point.X >= windowArea.Left
                && point.Y >= windowArea.Top
                && point.X <= windowArea.Right
                && point.Y <= windowArea.Bottom;

            return inClientSpace || inScreenSpace;
        }

        internal static bool ShouldSuppressPathfindingLabel(bool suppressLeverClick, bool suppressInactiveUltimatum)
            => suppressLeverClick || suppressInactiveUltimatum;

        internal static IReadOnlyList<LabelOnGround>? ResolveVisibleLabelsWithoutForcedCopy(object? rawVisibleLabels)
        {
            if (rawVisibleLabels is IReadOnlyList<LabelOnGround> visibleList)
            {
                return visibleList.Count > 0 ? visibleList : null;
            }

            if (rawVisibleLabels is IEnumerable<LabelOnGround> visibleEnumerable)
            {
                List<LabelOnGround> snapshot = [.. visibleEnumerable];
                return snapshot.Count > 0 ? snapshot : null;
            }

            return null;
        }
    }
}