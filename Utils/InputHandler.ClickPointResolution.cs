using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using SharpDX;
using System.Windows.Forms;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Utils
{
    public partial class InputHandler
    {
        private static bool TryGetLabelClientRect(LabelOnGround? label, out RectangleF rect)
        {
            rect = default;

            Element? element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            object? maybeRect = element.GetClientRect();
            if (maybeRect is not RectangleF r)
                return false;

            if (r.Width <= 0 || r.Height <= 0)
                return false;

            rect = r;
            return true;
        }

        private static bool TryGetIntersection(RectangleF a, RectangleF b, out RectangleF intersection)
        {
            intersection = default;

            float left = Math.Max(a.Left, b.Left);
            float top = Math.Max(a.Top, b.Top);
            float right = Math.Min(a.Right, b.Right);
            float bottom = Math.Min(a.Bottom, b.Bottom);

            if (right <= left || bottom <= top)
                return false;

            intersection = new RectangleF(left, top, right - left, bottom - top);
            return true;
        }

        private static bool IsPointInsideRect(Vector2 point, RectangleF rect)
        {
            return point.X >= rect.Left
                && point.X <= rect.Right
                && point.Y >= rect.Top
                && point.Y <= rect.Bottom;
        }

        private static bool IsPointBlocked(Vector2 point, IReadOnlyList<RectangleF> blockedAreas)
        {
            for (int i = 0; i < blockedAreas.Count; i++)
            {
                if (IsPointInsideRect(point, blockedAreas[i]))
                    return true;
            }

            return false;
        }

        private static bool IsPointClickable(Vector2 point, Func<Vector2, bool>? isClickableArea)
        {
            return isClickableArea == null || isClickableArea(point);
        }

        internal static bool HasUnblockedOverlapProbePoint(RectangleF targetRect, Vector2 preferredPoint, IReadOnlyList<RectangleF> potentialBlockers)
        {
            if (potentialBlockers == null || potentialBlockers.Count == 0)
                return true;

            const float inset = 1f;
            float left = targetRect.Left + inset;
            float right = targetRect.Right - inset;
            float top = targetRect.Top + inset;
            float bottom = targetRect.Bottom - inset;
            float centerY = targetRect.Top + (targetRect.Height * 0.5f);

            Vector2[] probePoints =
            [
                ClampPointToRect(preferredPoint, targetRect),
                new Vector2(left, top),
                new Vector2(right, top),
                new Vector2(left, bottom),
                new Vector2(right, bottom),
                new Vector2(targetRect.Center.X, top),
                new Vector2(targetRect.Center.X, bottom),
                new Vector2(left, centerY),
                new Vector2(right, centerY)
            ];

            for (int i = 0; i < probePoints.Length; i++)
            {
                if (!IsPointBlocked(probePoints[i], potentialBlockers))
                    return true;
            }

            return false;
        }

        private static RectangleF GetVirtualScreenBounds()
        {
            var vs = SystemInformation.VirtualScreen;
            return new RectangleF(vs.Left, vs.Top, vs.Width, vs.Height);
        }

        internal static bool IsSafeAutomationPoint(Vector2 point, RectangleF gameWindowRect, RectangleF virtualScreenRect)
        {
            if (!IsPointInsideRect(point, virtualScreenRect))
                return false;

            if (gameWindowRect.Width <= 0 || gameWindowRect.Height <= 0)
                return true;

            if (!TryGetIntersection(gameWindowRect, virtualScreenRect, out RectangleF allowedRect))
                return false;

            return IsPointInsideRect(point, allowedRect);
        }

        private static bool TryValidateAutomationScreenPoint(Vector2 point, GameController? gameController, out string reason)
        {
            RectangleF virtualScreenRect = GetVirtualScreenBounds();
            if (!IsPointInsideRect(point, virtualScreenRect))
            {
                reason = $"outside virtual screen bounds {virtualScreenRect}";
                return false;
            }

            RectangleF gameWindowRect = gameController?.Window?.GetWindowRectangleTimeCache ?? RectangleF.Empty;
            if (!IsSafeAutomationPoint(point, gameWindowRect, virtualScreenRect))
            {
                reason = $"outside safe game window bounds {gameWindowRect}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static Vector2 ClampPointToRect(Vector2 point, RectangleF rect)
        {
            return new Vector2(
                Math.Clamp(point.X, rect.Left, rect.Right),
                Math.Clamp(point.Y, rect.Top, rect.Bottom));
        }

        internal static bool IsHeistContractWorldItem(string? itemPath, string? renderName)
        {
            bool byPath = !string.IsNullOrWhiteSpace(itemPath)
                && itemPath.IndexOf(HeistContractPathMarker, StringComparison.OrdinalIgnoreCase) >= 0;
            if (byPath)
                return true;

            return !string.IsNullOrWhiteSpace(renderName)
                && renderName.StartsWith(HeistContractNamePrefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsHeistBlueprintWorldItem(string? itemPath, string? renderName)
        {
            bool byPath = !string.IsNullOrWhiteSpace(itemPath)
                && (itemPath.IndexOf(HeistBlueprintPathMarker, StringComparison.OrdinalIgnoreCase) >= 0
                    || itemPath.IndexOf(HeistBlueprintCurrencyPathMarker, StringComparison.OrdinalIgnoreCase) >= 0);
            if (byPath)
                return true;

            return !string.IsNullOrWhiteSpace(renderName)
                && renderName.StartsWith(HeistBlueprintNamePrefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRoguesMarkerWorldItem(string? itemPath, string? renderName)
        {
            bool byPath = !string.IsNullOrWhiteSpace(itemPath)
                && itemPath.IndexOf(RoguesMarkerPathMarker, StringComparison.OrdinalIgnoreCase) >= 0;
            if (byPath)
                return true;

            return !string.IsNullOrWhiteSpace(renderName)
                && string.Equals(renderName, RoguesMarkerName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldForceUiHoverVerificationForWorldItem(string? itemPath, string? renderName)
        {
            return IsHeistContractWorldItem(itemPath, renderName)
                || IsHeistBlueprintWorldItem(itemPath, renderName)
                || IsRoguesMarkerWorldItem(itemPath, renderName);
        }

        internal static Vector2 ResolvePreferredLabelPoint(RectangleF rect, EntityType itemType, int chestHeightOffset, string? itemPath, string? renderName)
        {
            Vector2 preferredPoint = rect.Center;

            if (itemType == EntityType.Chest)
            {
                preferredPoint.Y -= chestHeightOffset;
            }

            if (itemType == EntityType.WorldItem && IsHeistContractWorldItem(itemPath, renderName))
            {
                float safeLowerY = rect.Top + (rect.Height * 0.84f);
                preferredPoint.Y = Math.Clamp(safeLowerY, rect.Top + 1f, rect.Bottom - 1f);
            }

            return preferredPoint;
        }

        internal static bool TryResolveVisibleClickPoint(RectangleF targetRect, Vector2 preferredPoint, IReadOnlyList<RectangleF> blockedAreas, out Vector2 resolvedPoint)
        {
            Vector2 clampedPreferred = ClampPointToRect(preferredPoint, targetRect);
            if (blockedAreas == null || blockedAreas.Count == 0 || !IsPointBlocked(clampedPreferred, blockedAreas))
            {
                resolvedPoint = clampedPreferred;
                return true;
            }

            const int cols = 7;
            const int rows = 5;
            float stepX = targetRect.Width / cols;
            float stepY = targetRect.Height / rows;

            Vector2 best = clampedPreferred;
            float bestDistanceSq = float.MaxValue;

            for (int y = 0; y < rows; y++)
            {
                float sampleY = targetRect.Top + ((y + 0.5f) * stepY);
                for (int x = 0; x < cols; x++)
                {
                    float sampleX = targetRect.Left + ((x + 0.5f) * stepX);
                    Vector2 candidate = new Vector2(sampleX, sampleY);
                    if (IsPointBlocked(candidate, blockedAreas))
                        continue;

                    float dx = candidate.X - clampedPreferred.X;
                    float dy = candidate.Y - clampedPreferred.Y;
                    float distanceSq = dx * dx + dy * dy;
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        best = candidate;
                    }
                }
            }

            if (bestDistanceSq < float.MaxValue)
            {
                resolvedPoint = best;
                return true;
            }

            resolvedPoint = clampedPreferred;
            return false;
        }

        internal static bool TryResolveVisibleClickablePoint(
            RectangleF targetRect,
            Vector2 preferredPoint,
            IReadOnlyList<RectangleF> blockedAreas,
            Func<Vector2, bool>? isClickableArea,
            out Vector2 resolvedPoint)
        {
            Vector2 clampedPreferred = ClampPointToRect(preferredPoint, targetRect);
            if ((blockedAreas == null || blockedAreas.Count == 0 || !IsPointBlocked(clampedPreferred, blockedAreas))
                && IsPointClickable(clampedPreferred, isClickableArea))
            {
                resolvedPoint = clampedPreferred;
                return true;
            }

            const int cols = 7;
            const int rows = 5;
            float stepX = targetRect.Width / cols;
            float stepY = targetRect.Height / rows;

            Vector2 best = clampedPreferred;
            float bestDistanceSq = float.MaxValue;

            for (int y = 0; y < rows; y++)
            {
                float sampleY = targetRect.Top + ((y + 0.5f) * stepY);
                for (int x = 0; x < cols; x++)
                {
                    float sampleX = targetRect.Left + ((x + 0.5f) * stepX);
                    Vector2 candidate = new Vector2(sampleX, sampleY);

                    if (blockedAreas != null && blockedAreas.Count > 0 && IsPointBlocked(candidate, blockedAreas))
                        continue;

                    if (!IsPointClickable(candidate, isClickableArea))
                        continue;

                    float dx = candidate.X - clampedPreferred.X;
                    float dy = candidate.Y - clampedPreferred.Y;
                    float distanceSq = dx * dx + dy * dy;
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        best = candidate;
                    }
                }
            }

            if (bestDistanceSq < float.MaxValue)
            {
                resolvedPoint = best;
                return true;
            }

            resolvedPoint = clampedPreferred;
            return false;
        }

        internal static Vector2 ResolveVisibleClickPoint(RectangleF targetRect, Vector2 preferredPoint, IReadOnlyList<RectangleF> blockedAreas)
        {
            TryResolveVisibleClickPoint(targetRect, preferredPoint, blockedAreas, out Vector2 resolvedPoint);
            return resolvedPoint;
        }

        internal static List<RectangleF> CollectPotentialBlockingLabelRects(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
        {
            List<RectangleF> potentialBlockers = [];
            if (allLabels == null || allLabels.Count == 0)
                return potentialBlockers;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? other = allLabels[i];
                if (other == null || ReferenceEquals(other, targetLabel))
                    continue;

                if (!TryGetLabelClientRect(other, out RectangleF otherRect))
                    continue;

                if (otherRect.Right <= targetRect.Left
                    || otherRect.Left >= targetRect.Right
                    || otherRect.Bottom <= targetRect.Top
                    || otherRect.Top >= targetRect.Bottom)
                {
                    continue;
                }

                potentialBlockers.Add(otherRect);
            }

            return potentialBlockers;
        }

        internal static List<RectangleF> BuildIntersectionOverlaps(RectangleF targetRect, IReadOnlyList<RectangleF> potentialBlockers)
        {
            List<RectangleF> blockedAreas = new List<RectangleF>();
            if (potentialBlockers == null || potentialBlockers.Count == 0)
                return blockedAreas;

            for (int i = 0; i < potentialBlockers.Count; i++)
            {
                RectangleF otherRect = potentialBlockers[i];
                if (TryGetIntersection(targetRect, otherRect, out RectangleF overlap))
                {
                    blockedAreas.Add(overlap);
                }
            }

            return blockedAreas;
        }

        private static List<RectangleF> CollectBlockingOverlaps(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
        {
            List<RectangleF> potentialBlockers = CollectPotentialBlockingLabelRects(targetLabel, targetRect, allLabels);
            return BuildIntersectionOverlaps(targetRect, potentialBlockers);
        }
    }
}