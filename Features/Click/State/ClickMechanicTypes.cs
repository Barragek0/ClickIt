using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Features.Click.State
{
    internal readonly struct LostShipmentCandidate
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

    internal readonly struct SettlersOreCandidate
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

    internal readonly struct MechanicRank
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