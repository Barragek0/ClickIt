using System.Collections;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ClickIt.Utils;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

#nullable enable

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private const string ShrineMechanicId = "shrines";

        private static readonly int[] LabelSearchCaps = [1, 5, 25, 100];
        private IReadOnlyList<string>? _cachedMechanicPriorityOrder;
        private IReadOnlyCollection<string>? _cachedMechanicIgnoreDistanceIds;
        private IReadOnlyDictionary<string, int> _cachedMechanicPriorityIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlySet<string> _cachedMechanicIgnoreDistanceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            var nextShrine = ResolveNextShrineCandidate();

            bool useShrine = ShouldPreferShrineOverLabel(nextLabel, nextShrine);
            if (useShrine && nextShrine != null)
            {
                var shrineScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(nextShrine.PosNum);
                SharpDX.Vector2 shrineClickPos = new SharpDX.Vector2(shrineScreenRaw.X, shrineScreenRaw.Y);
                bool shrineClicked = PerformLabelClick(shrineClickPos, null, gameController);
                if (shrineClicked)
                {
                    shrineService.InvalidateCache();
                }

                yield break;
            }

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

        private Entity? ResolveNextShrineCandidate()
        {
            if (!settings.ClickShrines.Value)
                return null;

            return shrineService.GetNearestShrineInRange(settings.ClickDistance.Value, pos => pointIsInClickableArea(pos, ShrineMechanicId));
        }

        private bool ShouldPreferShrineOverLabel(LabelOnGround? label, Entity? shrine)
        {
            if (shrine == null)
                return false;
            if (label == null)
                return true;

            string? labelMechanicId = labelFilterService.GetMechanicIdForLabel(label);
            if (string.IsNullOrWhiteSpace(labelMechanicId))
                return true;

            RefreshMechanicPriorityCaches();

            float labelDistance = label.ItemOnGround?.DistancePlayer ?? float.MaxValue;
            float shrineDistance = shrine.DistancePlayer;

            var labelRank = BuildMechanicRank(labelDistance, labelMechanicId);
            var shrineRank = BuildMechanicRank(shrineDistance, ShrineMechanicId);

            return CompareMechanicRanks(shrineRank, labelRank) < 0;
        }

        private readonly struct MechanicRank(bool ignored, int priorityIndex, float weightedDistance, float rawDistance)
        {
            public bool Ignored { get; } = ignored;
            public int PriorityIndex { get; } = priorityIndex;
            public float WeightedDistance { get; } = weightedDistance;
            public float RawDistance { get; } = rawDistance;
        }

        private MechanicRank BuildMechanicRank(float distance, string? mechanicId)
        {
            int priorityIndex = GetMechanicPriorityIndex(mechanicId);
            bool ignored = !string.IsNullOrWhiteSpace(mechanicId) && _cachedMechanicIgnoreDistanceSet.Contains(mechanicId);
            float weightedDistance = distance + (priorityIndex == int.MaxValue ? float.MaxValue : priorityIndex * Math.Max(0, settings.MechanicPriorityDistancePenalty.Value));
            return new MechanicRank(ignored, priorityIndex, weightedDistance, distance);
        }

        private static int CompareMechanicRanks(MechanicRank left, MechanicRank right)
        {
            if (left.Ignored && right.Ignored)
            {
                int priorityCompare = left.PriorityIndex.CompareTo(right.PriorityIndex);
                if (priorityCompare != 0)
                    return priorityCompare;
                return left.RawDistance.CompareTo(right.RawDistance);
            }

            if (left.Ignored != right.Ignored)
            {
                return left.Ignored
                    ? (left.PriorityIndex <= right.PriorityIndex ? -1 : 1)
                    : (right.PriorityIndex <= left.PriorityIndex ? 1 : -1);
            }

            int weightedCompare = left.WeightedDistance.CompareTo(right.WeightedDistance);
            if (weightedCompare != 0)
                return weightedCompare;

            int distanceCompare = left.RawDistance.CompareTo(right.RawDistance);
            if (distanceCompare != 0)
                return distanceCompare;

            return left.PriorityIndex.CompareTo(right.PriorityIndex);
        }

        private void RefreshMechanicPriorityCaches()
        {
            IReadOnlyList<string> priorityOrder = settings.GetMechanicPriorityOrder();
            if (!ReferenceEquals(_cachedMechanicPriorityOrder, priorityOrder))
            {
                _cachedMechanicPriorityOrder = priorityOrder;

                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < priorityOrder.Count; i++)
                {
                    string id = priorityOrder[i] ?? string.Empty;
                    if (id.Length == 0 || map.ContainsKey(id))
                        continue;
                    map[id] = i;
                }

                _cachedMechanicPriorityIndexMap = map;
            }

            IReadOnlyCollection<string> ignoreDistanceIds = settings.GetMechanicPriorityIgnoreDistanceIds();
            if (!ReferenceEquals(_cachedMechanicIgnoreDistanceIds, ignoreDistanceIds))
            {
                _cachedMechanicIgnoreDistanceIds = ignoreDistanceIds;
                _cachedMechanicIgnoreDistanceSet = new HashSet<string>(ignoreDistanceIds, StringComparer.OrdinalIgnoreCase);
            }
        }

        private int GetMechanicPriorityIndex(string? mechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                return int.MaxValue;

            return _cachedMechanicPriorityIndexMap.TryGetValue(mechanicId, out int index) ? index : int.MaxValue;
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

            LabelOnGround? hovered = FindLabelByAddress(allLabels, uiHover.Address);
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

            foreach (int cap in LabelSearchCaps)
            {
                int limit = Math.Min(cap, allLabels.Count);
                LabelOnGround? candidate = FindLabelInRange(allLabels, 0, limit);
                if (candidate != null)
                    return candidate;
            }

            // Fallback to full scan (rare).
            return FindLabelInRange(allLabels, 0, allLabels.Count);
        }

        private static LabelOnGround? FindLabelByAddress(IReadOnlyList<LabelOnGround> labels, long address)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label?.Label != null && label.Label.Address == address)
                    return label;
            }

            return null;
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