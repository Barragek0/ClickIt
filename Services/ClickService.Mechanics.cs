using ClickIt.Definitions;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private const string ShrineMechanicId = MechanicIds.Shrines;
        private const string LostShipmentMechanicId = MechanicIds.LostShipment;
        private const string LostShipmentPathMarker = "Metadata/Chests/LostShipmentCrate";
        private const string LostShipmentLoosePathMarker = "LostShipment";
        private const string LostGoodsRenderNameMarker = "Lost Goods";
        private const string LostShipmentRenderNameMarker = "Lost Shipment";
        private const string VerisiumMechanicId = MechanicIds.SettlersVerisium;
        private const string VerisiumBossSubAreaTransitionPathMarker = MechanicIds.VerisiumBossSubAreaTransitionPathMarker;
        private const string AreaTransitionsMechanicId = MechanicIds.AreaTransitions;
        private const string LabyrinthTrialsMechanicId = MechanicIds.LabyrinthTrials;
        private const int NonVerisiumSettlersRetryWindowMs = 3000;

        private IReadOnlyList<string>? _cachedMechanicPriorityOrder;
        private IReadOnlyCollection<string>? _cachedMechanicIgnoreDistanceIds;
        private IReadOnlyDictionary<string, int>? _cachedMechanicIgnoreDistanceWithinById;
        private IReadOnlyDictionary<string, int> _cachedMechanicPriorityIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlySet<string> _cachedMechanicIgnoreDistanceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, int> _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly struct LostShipmentCandidate(Entity entity, Vector2 clickPosition)
        {
            public Entity Entity { get; } = entity;
            public Vector2 ClickPosition { get; } = clickPosition;
            public float Distance { get; } = entity.DistancePlayer;
        }

        private readonly struct SettlersOreCandidate(
            Entity entity,
            Vector2 clickPosition,
            string mechanicId,
            string entityPath,
            Vector2 worldScreenRaw,
            Vector2 worldScreenAbsolute)
        {
            public Entity Entity { get; } = entity;
            public Vector2 ClickPosition { get; } = clickPosition;
            public string MechanicId { get; } = mechanicId;
            public string EntityPath { get; } = entityPath;
            public Vector2 WorldScreenRaw { get; } = worldScreenRaw;
            public Vector2 WorldScreenAbsolute { get; } = worldScreenAbsolute;
            public float Distance { get; } = entity.DistancePlayer;
        }

        private readonly struct MechanicRank(bool ignored, int priorityIndex, float weightedDistance, float rawDistance)
        {
            public bool Ignored { get; } = ignored;
            public int PriorityIndex { get; } = priorityIndex;
            public float WeightedDistance { get; } = weightedDistance;
            public float RawDistance { get; } = rawDistance;
        }

        internal static bool IsLostShipmentPath(string? path)
            => HasAnyMarker(path, LostShipmentPathMarker, LostShipmentLoosePathMarker);

        internal static bool IsVerisiumPath(string? path)
            => HasAnyMarker(path, Definitions.Constants.Verisium)
               && !HasAnyMarker(path, VerisiumBossSubAreaTransitionPathMarker);

        internal static bool ShouldUseHoldClickForSettlersMechanic(string? mechanicId)
            => string.Equals(mechanicId, VerisiumMechanicId, StringComparison.OrdinalIgnoreCase);

        private static bool IsLostShipmentEntity(string? path, string? renderName)
            => IsLostShipmentPath(path)
               || HasAnyMarker(renderName, LostGoodsRenderNameMarker, LostShipmentRenderNameMarker);

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
            var candidate = BuildMechanicRank(
                lostShipmentDistance,
                LostShipmentMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            return BeatsExistingCandidate(candidate, labelDistance, labelMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty)
                && BeatsExistingCandidate(candidate, shrineDistance, ShrineMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty);
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
            var candidate = BuildMechanicRank(
                settlersOreDistance,
                settlersOreMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            return BeatsExistingCandidate(candidate, labelDistance, labelMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty)
                && BeatsExistingCandidate(candidate, shrineDistance, ShrineMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty)
                && BeatsExistingCandidate(candidate, lostShipmentDistance, LostShipmentMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty);
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
            var shrineRank = BuildMechanicRank(
                shrineDistance,
                ShrineMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            var labelRank = BuildMechanicRank(
                labelDistance,
                labelMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            return CompareMechanicRanks(shrineRank, labelRank) < 0;
        }

        private static bool HasAnyMarker(string? value, params string[] markers)
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

        private static bool BeatsExistingCandidate(
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

            var other = BuildMechanicRank(
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
            int priorityIndex = int.MaxValue;
            if (!string.IsNullOrWhiteSpace(mechanicId)
                && priorityIndexMap.TryGetValue(mechanicId, out int configuredIndex))
            {
                priorityIndex = configuredIndex;
            }

            bool ignored = IsIgnoreDistanceActiveForMechanic(mechanicId, distance, ignoreDistanceSet, ignoreDistanceWithinByMechanicId);
            float weightedDistance = distance + (priorityIndex == int.MaxValue
                ? float.MaxValue
                : priorityIndex * Math.Max(0, priorityDistancePenalty));

            return new MechanicRank(ignored, priorityIndex, weightedDistance, distance);
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

        private void RefreshMechanicPriorityCaches()
        {
            IReadOnlyList<string> priorityOrder = settings.GetMechanicPriorityOrder();
            if (!ReferenceEquals(_cachedMechanicPriorityOrder, priorityOrder))
            {
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

            IReadOnlyCollection<string> ignoreDistanceIds = settings.GetMechanicPriorityIgnoreDistanceIds();
            if (!ReferenceEquals(_cachedMechanicIgnoreDistanceIds, ignoreDistanceIds))
            {
                _cachedMechanicIgnoreDistanceIds = ignoreDistanceIds;
                _cachedMechanicIgnoreDistanceSet = new HashSet<string>(ignoreDistanceIds, StringComparer.OrdinalIgnoreCase);
            }

            IReadOnlyDictionary<string, int> ignoreDistanceWithin = settings.GetMechanicPriorityIgnoreDistanceWithinById();
            if (!ReferenceEquals(_cachedMechanicIgnoreDistanceWithinById, ignoreDistanceWithin))
            {
                _cachedMechanicIgnoreDistanceWithinById = ignoreDistanceWithin;
                _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(ignoreDistanceWithin, StringComparer.OrdinalIgnoreCase);
            }
        }

        private int GetMechanicPriorityIndex(string? mechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                return int.MaxValue;

            return _cachedMechanicPriorityIndexMap.TryGetValue(mechanicId, out int index)
                ? index
                : int.MaxValue;
        }

        private static bool IsSettlersMechanicId(string? mechanicId)
            => !string.IsNullOrWhiteSpace(mechanicId)
               && mechanicId.StartsWith("settlers-", StringComparison.OrdinalIgnoreCase);

        private void MarkSuccessfulSettlersInteraction(long entityAddress, string? mechanicId)
        {
            if (entityAddress == 0
                || string.IsNullOrWhiteSpace(mechanicId)
                || !IsSettlersMechanicId(mechanicId)
                || ShouldUseHoldClickForSettlersMechanic(mechanicId))
            {
                return;
            }

            _lastSuccessfulNonVerisiumSettlersTargetAddress = entityAddress;
            _lastSuccessfulNonVerisiumSettlersMechanicId = mechanicId;
            _lastSuccessfulNonVerisiumSettlersTimestampMs = Environment.TickCount64;
        }

        private bool IsRecentSuccessfulNonVerisiumSettlersRetryTarget(long entityAddress, string? mechanicId)
        {
            long now = Environment.TickCount64;
            return IsRecentSuccessfulNonVerisiumSettlersRetryTarget(
                _lastSuccessfulNonVerisiumSettlersTargetAddress,
                _lastSuccessfulNonVerisiumSettlersMechanicId,
                _lastSuccessfulNonVerisiumSettlersTimestampMs,
                entityAddress,
                mechanicId,
                now,
                NonVerisiumSettlersRetryWindowMs);
        }

        internal static bool IsRecentSuccessfulNonVerisiumSettlersRetryTarget(
            long lastSuccessfulTargetAddress,
            string? lastSuccessfulMechanicId,
            long lastSuccessfulTimestampMs,
            long candidateEntityAddress,
            string? candidateMechanicId,
            long now,
            int retryWindowMs)
        {
            if (lastSuccessfulTargetAddress == 0
                || candidateEntityAddress == 0
                || !IsSettlersMechanicId(candidateMechanicId)
                || ShouldUseHoldClickForSettlersMechanic(candidateMechanicId)
                || !string.Equals(lastSuccessfulMechanicId, candidateMechanicId, StringComparison.OrdinalIgnoreCase)
                || candidateEntityAddress != lastSuccessfulTargetAddress
                || retryWindowMs <= 0
                || lastSuccessfulTimestampMs <= 0)
            {
                return false;
            }

            long elapsed = now - lastSuccessfulTimestampMs;
            return elapsed >= 0 && elapsed <= retryWindowMs;
        }

        private bool IsSettlersMechanicEnabled(string? mechanicId)
        {
            if (!settings.ClickSettlersOre.Value || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            if (string.Equals(mechanicId, MechanicIds.SettlersCrimsonIron, StringComparison.OrdinalIgnoreCase))
                return settings.ClickSettlersCrimsonIron.Value;
            if (string.Equals(mechanicId, MechanicIds.SettlersCopper, StringComparison.OrdinalIgnoreCase))
                return settings.ClickSettlersCopper.Value;
            if (string.Equals(mechanicId, MechanicIds.SettlersPetrifiedWood, StringComparison.OrdinalIgnoreCase))
                return settings.ClickSettlersPetrifiedWood.Value;
            if (string.Equals(mechanicId, MechanicIds.SettlersBismuth, StringComparison.OrdinalIgnoreCase))
                return settings.ClickSettlersBismuth.Value;
            if (string.Equals(mechanicId, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase))
                return settings.ClickSettlersVerisium.Value;

            return false;
        }
    }
}