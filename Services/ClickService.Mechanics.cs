using ClickIt.Definitions;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal const string ShrineMechanicId = MechanicIds.Shrines;
        internal const string LostShipmentMechanicId = MechanicIds.LostShipment;
        private const string LostShipmentPathMarker = "Metadata/Chests/LostShipmentCrate";
        private const string LostShipmentLoosePathMarker = "LostShipment";
        private const string LostGoodsRenderNameMarker = "Lost Goods";
        private const string LostShipmentRenderNameMarker = "Lost Shipment";
        private const string VerisiumMechanicId = MechanicIds.SettlersVerisium;
        private const string VerisiumBossSubAreaTransitionPathMarker = MechanicIds.VerisiumBossSubAreaTransitionPathMarker;
        private const string AreaTransitionsMechanicId = MechanicIds.AreaTransitions;
        private const string LabyrinthTrialsMechanicId = MechanicIds.LabyrinthTrials;
        internal const int HiddenFallbackCandidateCacheWindowMs = 150;
        internal const int VisibleMechanicCandidateCacheWindowMs = 80;
        internal const int GroundLabelEntityAddressCacheWindowMs = 150;

        private IReadOnlyList<string>? _cachedMechanicPriorityOrder;
        private IReadOnlyCollection<string>? _cachedMechanicIgnoreDistanceIds;
        private IReadOnlyDictionary<string, int>? _cachedMechanicIgnoreDistanceWithinById;
        private IReadOnlyDictionary<string, int> _cachedMechanicPriorityIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlySet<string> _cachedMechanicIgnoreDistanceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, int> _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal static readonly Vector2[] NearbyClickProbeOffsets =
        [
            new Vector2(0f, 0f),
            new Vector2(12f, 0f),
            new Vector2(-12f, 0f),
            new Vector2(0f, 12f),
            new Vector2(0f, -12f),
            new Vector2(24f, 0f),
            new Vector2(-24f, 0f),
            new Vector2(0f, 24f),
            new Vector2(0f, -24f)
        ];

        internal static bool IsLostShipmentPath(string? path)
            => ContainsAny(path, LostShipmentPathMarker, LostShipmentLoosePathMarker);

        internal static bool ShouldClickShrineWhenGroundItemsHidden(Entity? shrine) => shrine != null;

        internal static bool ShouldUseHoldClickForSettlersMechanic(string? mechanicId)
            => string.Equals(mechanicId, VerisiumMechanicId, StringComparison.OrdinalIgnoreCase);

        internal static bool IsLostShipmentEntity(string? path, string? renderName)
            => IsLostShipmentPath(path)
               || ContainsAny(renderName, LostGoodsRenderNameMarker, LostShipmentRenderNameMarker);

        internal static bool ShouldSkipLostShipmentEntity(bool isValid, float distance, int clickDistance, bool isOpened)
            => !isValid || isOpened || distance > clickDistance;

        internal static bool ShouldSkipSettlersOreEntity(bool isValid, float distance, int clickDistance)
            => !isValid || distance > clickDistance;

        internal static bool ShouldReuseTimedLabelCountCache(long now, long cachedAtMs, int cachedLabelCount, int currentLabelCount, int cacheWindowMs)
        {
            if (cachedAtMs <= 0 || cacheWindowMs <= 0)
                return false;

            if (cachedLabelCount != currentLabelCount)
                return false;

            long age = now - cachedAtMs;
            return age >= 0 && age <= cacheWindowMs;
        }

        private MechanicPriorityContext CreateMechanicPriorityContext()
            => new(
                _cachedMechanicPriorityIndexMap,
                _cachedMechanicIgnoreDistanceSet,
                _cachedMechanicIgnoreDistanceWithinMap,
                settings.MechanicPriorityDistancePenalty.Value);

        internal static MechanicCandidateSignal CreateMechanicCandidateSignal(
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

            CandidateScoreEngine.CandidateScoreContext scoreContext = CreateCandidateScoreContext(context);
            CandidateScoreEngine.CandidateScore score = CandidateScoreEngine.Build(distance, candidate.MechanicId, cursorDistance, scoreContext);
            return new MechanicRank(score.Ignored, score.PriorityIndex, score.WeightedDistance, score.RawDistance, score.CursorDistance);
        }

        private static CandidateScoreEngine.CandidateScoreContext CreateCandidateScoreContext(in MechanicPriorityContext context)
            => new(
                context.PriorityIndexMap,
                context.IgnoreDistanceSet,
                context.IgnoreDistanceWithinByMechanicId,
                context.PriorityDistancePenalty);

        private static int ResolvePriorityIndex(string? mechanicId, IReadOnlyDictionary<string, int> priorityIndexMap)
            => CandidateScoreEngine.ResolvePriorityIndex(mechanicId, priorityIndexMap);

        private static bool IsIgnoreDistanceActiveForMechanic(
            string? mechanicId,
            float distance,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
            => CandidateScoreEngine.IsIgnoreDistanceActive(mechanicId, distance, ignoreDistanceSet, ignoreDistanceWithinByMechanicId);

        internal static int CompareMechanicRanks(MechanicRank left, MechanicRank right)
            => CandidateScoreEngine.Compare(ToCandidateScore(left), ToCandidateScore(right));

        private static CandidateScoreEngine.CandidateScore ToCandidateScore(MechanicRank rank)
            => new(rank.Ignored, rank.PriorityIndex, rank.WeightedDistance, rank.RawDistance, rank.CursorDistance);

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

        internal static bool ArePlayerDistancesEquivalent(float left, float right)
            => Math.Abs(left - right) <= 0.001f;

        internal static bool IsFirstCandidateCloserToCursor(Vector2 firstClickPoint, Vector2 secondClickPoint, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            float first = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, firstClickPoint, windowTopLeft);
            float second = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, secondClickPoint, windowTopLeft);
            return first < second;
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