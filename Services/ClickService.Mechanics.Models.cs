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
        private const int HiddenFallbackCandidateCacheWindowMs = 150;
        private const int VisibleMechanicCandidateCacheWindowMs = 80;
        private const int GroundLabelEntityAddressCacheWindowMs = 150;

        private IReadOnlyList<string>? _cachedMechanicPriorityOrder;
        private IReadOnlyCollection<string>? _cachedMechanicIgnoreDistanceIds;
        private IReadOnlyDictionary<string, int>? _cachedMechanicIgnoreDistanceWithinById;
        private IReadOnlyDictionary<string, int> _cachedMechanicPriorityIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlySet<string> _cachedMechanicIgnoreDistanceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, int> _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private long _hiddenFallbackCandidateCacheTimestampMs;
        private int _hiddenFallbackCandidateLabelCount = -1;
        private bool _hiddenFallbackCandidateCacheHasValue;
        private LostShipmentCandidate? _hiddenFallbackCachedLostShipmentCandidate;
        private SettlersOreCandidate? _hiddenFallbackCachedSettlersCandidate;
        private long _visibleMechanicCandidateCacheTimestampMs;
        private int _visibleMechanicCandidateLabelCount = -1;
        private bool _visibleMechanicCandidateCacheHasValue;
        private LostShipmentCandidate? _visibleMechanicCachedLostShipmentCandidate;
        private SettlersOreCandidate? _visibleMechanicCachedSettlersCandidate;
        private readonly HashSet<long> _cachedGroundLabelEntityAddresses = [];
        private long _cachedGroundLabelEntityAddressesTimestampMs;
        private int _cachedGroundLabelEntityLabelCount = -1;

        private readonly struct LostShipmentCandidate
        {
            public LostShipmentCandidate(Entity entity, Vector2 clickPosition)
            {
                Entity = entity;
                ClickPosition = clickPosition;
                Distance = entity.DistancePlayer;
            }

            public Entity Entity { get; }
            public Vector2 ClickPosition { get; }
            public float Distance { get; }
        }

        private readonly struct SettlersOreCandidate
        {
            public SettlersOreCandidate(
                Entity entity,
                Vector2 clickPosition,
                string mechanicId,
                string entityPath,
                Vector2 worldScreenRaw,
                Vector2 worldScreenAbsolute)
            {
                Entity = entity;
                ClickPosition = clickPosition;
                MechanicId = mechanicId;
                EntityPath = entityPath;
                WorldScreenRaw = worldScreenRaw;
                WorldScreenAbsolute = worldScreenAbsolute;
                Distance = entity.DistancePlayer;
            }

            public Entity Entity { get; }
            public Vector2 ClickPosition { get; }
            public string MechanicId { get; }
            public string EntityPath { get; }
            public Vector2 WorldScreenRaw { get; }
            public Vector2 WorldScreenAbsolute { get; }
            public float Distance { get; }
        }

        private readonly struct MechanicRank
        {
            public MechanicRank(bool ignored, int priorityIndex, float weightedDistance, float rawDistance, float cursorDistance)
            {
                Ignored = ignored;
                PriorityIndex = priorityIndex;
                WeightedDistance = weightedDistance;
                RawDistance = rawDistance;
                CursorDistance = cursorDistance;
            }

            public bool Ignored { get; }
            public int PriorityIndex { get; }
            public float WeightedDistance { get; }
            public float RawDistance { get; }
            public float CursorDistance { get; }
        }

        internal readonly struct MechanicPriorityContext
        {
            public MechanicPriorityContext(
                IReadOnlyDictionary<string, int> priorityIndexMap,
                IReadOnlySet<string> ignoreDistanceSet,
                IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
                int priorityDistancePenalty)
            {
                PriorityIndexMap = priorityIndexMap;
                IgnoreDistanceSet = ignoreDistanceSet;
                IgnoreDistanceWithinByMechanicId = ignoreDistanceWithinByMechanicId;
                PriorityDistancePenalty = priorityDistancePenalty;
            }

            public IReadOnlyDictionary<string, int> PriorityIndexMap { get; }
            public IReadOnlySet<string> IgnoreDistanceSet { get; }
            public IReadOnlyDictionary<string, int> IgnoreDistanceWithinByMechanicId { get; }
            public int PriorityDistancePenalty { get; }
        }

        internal readonly struct MechanicCandidateSignal
        {
            public MechanicCandidateSignal(string? mechanicId, float? distance, float? cursorDistance)
            {
                MechanicId = mechanicId;
                Distance = distance;
                CursorDistance = cursorDistance;
            }

            public string? MechanicId { get; }
            public float? Distance { get; }
            public float? CursorDistance { get; }
            public bool Exists => Distance.HasValue;

            public static MechanicCandidateSignal None => new(null, null, null);
        }
    }
}