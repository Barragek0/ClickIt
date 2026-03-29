using ClickIt.Definitions;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal static bool IsLostShipmentPath(string? path)
            => ContainsAny(path, LostShipmentPathMarker, LostShipmentLoosePathMarker);

        internal static bool IsVerisiumPath(string? path)
            => ContainsAny(path, Definitions.Constants.Verisium)
               && !ContainsAny(path, VerisiumBossSubAreaTransitionPathMarker);

        internal static bool ShouldUseHoldClickForSettlersMechanic(string? mechanicId)
            => string.Equals(mechanicId, VerisiumMechanicId, StringComparison.OrdinalIgnoreCase);

        private static bool IsLostShipmentEntity(string? path, string? renderName)
            => IsLostShipmentPath(path)
               || ContainsAny(renderName, LostGoodsRenderNameMarker, LostShipmentRenderNameMarker);

        internal static bool ShouldSkipLostShipmentEntity(bool isValid, float distance, int clickDistance, bool isOpened)
            => !isValid || isOpened || distance > clickDistance;

        internal static bool ShouldSkipSettlersOreEntity(bool isValid, float distance, int clickDistance)
            => !isValid || distance > clickDistance;

        internal static bool ShouldSkipVerisiumEntity(bool isValid, float distance, int clickDistance)
            => ShouldSkipSettlersOreEntity(isValid, distance, clickDistance);

        private MechanicPriorityContext CreateMechanicPriorityContext()
            => new(
                _cachedMechanicPriorityIndexMap,
                _cachedMechanicIgnoreDistanceSet,
                _cachedMechanicIgnoreDistanceWithinMap,
                settings.MechanicPriorityDistancePenalty.Value);

        private static MechanicCandidateSignal CreateMechanicCandidateSignal(
            string? mechanicId,
            float? distance,
            float? cursorDistance = null)
            => new(mechanicId, distance, cursorDistance);

        internal static bool ShouldPreferLostShipmentOverVisibleCandidates(
            in MechanicCandidateSignal lostShipment,
            in MechanicCandidateSignal label,
            in MechanicCandidateSignal shrine,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(lostShipment, context, label, shrine);

        internal static bool ShouldPreferSettlersOreOverVisibleCandidates(
            in MechanicCandidateSignal settlers,
            in MechanicCandidateSignal label,
            in MechanicCandidateSignal shrine,
            in MechanicCandidateSignal lostShipment,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(settlers, context, label, shrine, lostShipment);

        internal static bool ShouldPreferShrineOverLabelForOffscreen(
            in MechanicCandidateSignal shrine,
            in MechanicCandidateSignal label,
            in MechanicPriorityContext context)
            => ShouldPreferCandidate(shrine, context, label);

        private static bool ShouldPreferCandidate(
            in MechanicCandidateSignal candidate,
            in MechanicPriorityContext context,
            params MechanicCandidateSignal[] others)
        {
            if (!candidate.Exists)
                return false;

            MechanicRank candidateRank = BuildMechanicRank(candidate, context);
            for (int i = 0; i < others.Length; i++)
            {
                MechanicCandidateSignal other = others[i];
                if (!other.Exists)
                    continue;

                MechanicRank otherRank = BuildMechanicRank(other, context);
                if (CompareMechanicRanks(candidateRank, otherRank) >= 0)
                    return false;
            }

            return true;
        }

        private MechanicRank BuildMechanicRank(float distance, string? mechanicId)
            => BuildMechanicRank(
                CreateMechanicCandidateSignal(mechanicId, distance),
                CreateMechanicPriorityContext());

        private static MechanicRank BuildMechanicRank(
            in MechanicCandidateSignal candidate,
            in MechanicPriorityContext context)
        {
            float distance = candidate.Distance ?? float.MaxValue;
            float cursorDistance = candidate.CursorDistance ?? float.MaxValue;
            int priorityIndex = ResolvePriorityIndex(candidate.MechanicId, context.PriorityIndexMap);
            bool ignored = IsIgnoreDistanceActiveForMechanic(candidate.MechanicId, distance, context.IgnoreDistanceSet, context.IgnoreDistanceWithinByMechanicId);

            float weightedDistance = priorityIndex == int.MaxValue
                ? float.MaxValue
                : distance + (priorityIndex * Math.Max(0, context.PriorityDistancePenalty));

            return new MechanicRank(ignored, priorityIndex, weightedDistance, distance, cursorDistance);
        }

        private static int ResolvePriorityIndex(string? mechanicId, IReadOnlyDictionary<string, int> priorityIndexMap)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                return int.MaxValue;

            return priorityIndexMap.TryGetValue(mechanicId, out int index)
                ? index
                : int.MaxValue;
        }

        private static bool IsIgnoreDistanceActiveForMechanic(
            string? mechanicId,
            float distance,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId) || !ignoreDistanceSet.Contains(mechanicId))
                return false;

            int maxDistance = ignoreDistanceWithinByMechanicId.TryGetValue(mechanicId, out int configuredDistance)
                ? configuredDistance
                : 100;

            return distance <= maxDistance;
        }

        private static int CompareMechanicRanks(MechanicRank left, MechanicRank right)
        {
            if (left.Ignored && right.Ignored)
            {
                int byPriority = left.PriorityIndex.CompareTo(right.PriorityIndex);
                if (byPriority != 0)
                    return byPriority;

                int byRawDistanceIgnored = left.RawDistance.CompareTo(right.RawDistance);
                if (byRawDistanceIgnored != 0)
                    return byRawDistanceIgnored;

                return left.CursorDistance.CompareTo(right.CursorDistance);
            }

            if (left.Ignored != right.Ignored)
            {
                return left.Ignored
                    ? (left.PriorityIndex <= right.PriorityIndex ? -1 : 1)
                    : (right.PriorityIndex <= left.PriorityIndex ? 1 : -1);
            }

            int byWeightedDistance = left.WeightedDistance.CompareTo(right.WeightedDistance);
            if (byWeightedDistance != 0)
                return byWeightedDistance;

            int byRawDistanceNonIgnored = left.RawDistance.CompareTo(right.RawDistance);
            if (byRawDistanceNonIgnored != 0)
                return byRawDistanceNonIgnored;

            int byCursorDistance = left.CursorDistance.CompareTo(right.CursorDistance);
            if (byCursorDistance != 0)
                return byCursorDistance;

            return left.PriorityIndex.CompareTo(right.PriorityIndex);
        }

        private static bool ContainsAny(string? value, params string[] markers)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < markers.Length; i++)
            {
                if (value.Contains(markers[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void RefreshMechanicPriorityCaches()
        {
            RefreshPriorityOrderCache();
            RefreshIgnoreDistanceIdsCache();
            RefreshIgnoreDistanceWithinCache();
        }

        private void RefreshPriorityOrderCache()
        {
            IReadOnlyList<string> priorityOrder = settings.GetMechanicPriorityOrder();
            if (ReferenceEquals(_cachedMechanicPriorityOrder, priorityOrder))
                return;

            _cachedMechanicPriorityOrder = priorityOrder;
            var priorityMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < priorityOrder.Count; i++)
            {
                string id = priorityOrder[i] ?? string.Empty;
                if (id.Length == 0 || priorityMap.ContainsKey(id))
                    continue;

                priorityMap[id] = i;
            }

            _cachedMechanicPriorityIndexMap = priorityMap;
        }

        private void RefreshIgnoreDistanceIdsCache()
        {
            IReadOnlyCollection<string> ignoreDistanceIds = settings.GetMechanicPriorityIgnoreDistanceIds();
            if (ReferenceEquals(_cachedMechanicIgnoreDistanceIds, ignoreDistanceIds))
                return;

            _cachedMechanicIgnoreDistanceIds = ignoreDistanceIds;
            _cachedMechanicIgnoreDistanceSet = new HashSet<string>(ignoreDistanceIds, StringComparer.OrdinalIgnoreCase);
        }

        private void RefreshIgnoreDistanceWithinCache()
        {
            IReadOnlyDictionary<string, int> ignoreDistanceWithin = settings.GetMechanicPriorityIgnoreDistanceWithinById();
            if (ReferenceEquals(_cachedMechanicIgnoreDistanceWithinById, ignoreDistanceWithin))
                return;

            _cachedMechanicIgnoreDistanceWithinById = ignoreDistanceWithin;
            _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(ignoreDistanceWithin, StringComparer.OrdinalIgnoreCase);
        }

        private int GetMechanicPriorityIndex(string? mechanicId)
            => ResolvePriorityIndex(mechanicId, _cachedMechanicPriorityIndexMap);

        private static bool IsSettlersMechanicId(string? mechanicId)
            => !string.IsNullOrWhiteSpace(mechanicId)
               && mechanicId.StartsWith("settlers-", StringComparison.OrdinalIgnoreCase);

        private bool IsSettlersMechanicEnabled(string? mechanicId)
        {
            if (!settings.ClickSettlersOre.Value || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            return mechanicId switch
            {
                var id when string.Equals(id, MechanicIds.SettlersCrimsonIron, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersCrimsonIron.Value,
                var id when string.Equals(id, MechanicIds.SettlersCopper, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersCopper.Value,
                var id when string.Equals(id, MechanicIds.SettlersPetrifiedWood, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersPetrifiedWood.Value,
                var id when string.Equals(id, MechanicIds.SettlersBismuth, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersBismuth.Value,
                var id when string.Equals(id, MechanicIds.SettlersHourglass, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersOre.Value,
                var id when string.Equals(id, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersVerisium.Value,
                _ => false
            };
        }
    }
}