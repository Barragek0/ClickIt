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

        internal static bool ShouldPreferLostShipmentOverVisibleCandidates(
            float lostShipmentDistance,
            float? labelDistance,
            string? labelMechanicId,
            float? shrineDistance,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            MechanicRank candidate = BuildMechanicRank(
                lostShipmentDistance,
                LostShipmentMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            return BeatsOtherCandidate(candidate, labelDistance, labelMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty)
                && BeatsOtherCandidate(candidate, shrineDistance, ShrineMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty);
        }

        internal static bool ShouldPreferSettlersOreOverVisibleCandidates(
            float settlersOreDistance,
            string settlersOreMechanicId,
            float? labelDistance,
            string? labelMechanicId,
            float? shrineDistance,
            float? lostShipmentDistance,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            MechanicRank candidate = BuildMechanicRank(
                settlersOreDistance,
                settlersOreMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            return BeatsOtherCandidate(candidate, labelDistance, labelMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty)
                && BeatsOtherCandidate(candidate, shrineDistance, ShrineMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty)
                && BeatsOtherCandidate(candidate, lostShipmentDistance, LostShipmentMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty);
        }

        internal static bool ShouldPreferVerisiumOverVisibleCandidates(
            float verisiumDistance,
            float? labelDistance,
            string? labelMechanicId,
            float? shrineDistance,
            float? lostShipmentDistance,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
            => ShouldPreferSettlersOreOverVisibleCandidates(
                verisiumDistance,
                VerisiumMechanicId,
                labelDistance,
                labelMechanicId,
                shrineDistance,
                lostShipmentDistance,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

        internal static bool ShouldPreferShrineOverLabelForOffscreen(
            float shrineDistance,
            float labelDistance,
            string? labelMechanicId,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            MechanicRank shrineRank = BuildMechanicRank(
                shrineDistance,
                ShrineMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            MechanicRank labelRank = BuildMechanicRank(
                labelDistance,
                labelMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            return CompareMechanicRanks(shrineRank, labelRank) < 0;
        }

        private static bool BeatsOtherCandidate(
            MechanicRank candidate,
            float? otherDistance,
            string? otherMechanicId,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            if (!otherDistance.HasValue)
                return true;

            MechanicRank other = BuildMechanicRank(
                otherDistance.Value,
                otherMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            return CompareMechanicRanks(candidate, other) < 0;
        }

        private MechanicRank BuildMechanicRank(float distance, string? mechanicId)
            => BuildMechanicRank(
                distance,
                mechanicId,
                _cachedMechanicPriorityIndexMap,
                _cachedMechanicIgnoreDistanceSet,
                _cachedMechanicIgnoreDistanceWithinMap,
                settings.MechanicPriorityDistancePenalty.Value);

        private static MechanicRank BuildMechanicRank(
            float distance,
            string? mechanicId,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            int priorityIndex = ResolvePriorityIndex(mechanicId, priorityIndexMap);
            bool ignored = IsIgnoreDistanceActiveForMechanic(mechanicId, distance, ignoreDistanceSet, ignoreDistanceWithinByMechanicId);

            float weightedDistance = priorityIndex == int.MaxValue
                ? float.MaxValue
                : distance + (priorityIndex * Math.Max(0, priorityDistancePenalty));

            return new MechanicRank(ignored, priorityIndex, weightedDistance, distance);
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
                return byPriority != 0 ? byPriority : left.RawDistance.CompareTo(right.RawDistance);
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

            int byRawDistance = left.RawDistance.CompareTo(right.RawDistance);
            if (byRawDistance != 0)
                return byRawDistance;

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