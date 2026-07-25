namespace ClickIt.Features.Click.State
{
    internal readonly struct LostShipmentCandidate(Entity entity, Vector2 clickPosition, float distance)
    {
        public LostShipmentCandidate(Entity entity, Vector2 clickPosition)
            : this(
                entity,
                clickPosition,
                DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out float distance)
                    ? distance
                    : float.MaxValue)
        {
        }

        public Entity Entity { get; } = entity;
        public Vector2 ClickPosition { get; } = clickPosition;
        public float Distance { get; } = distance;
    }

    internal readonly struct SettlersOreCandidate(
        Entity entity,
        Vector2 clickPosition,
        string mechanicId,
        string entityPath,
        Vector2 worldScreenRaw,
        Vector2 worldScreenAbsolute,
        float distance)
    {
        public SettlersOreCandidate(
            Entity entity,
            Vector2 clickPosition,
            string mechanicId,
            string entityPath,
            Vector2 worldScreenRaw,
            Vector2 worldScreenAbsolute)
            : this(
                entity,
                clickPosition,
                mechanicId,
                entityPath,
                worldScreenRaw,
                worldScreenAbsolute,
                DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out float distance)
                    ? distance
                    : float.MaxValue)
        {
        }

        public Entity Entity { get; } = entity;
        public Vector2 ClickPosition { get; } = clickPosition;
        public string MechanicId { get; } = mechanicId;
        public string EntityPath { get; } = entityPath;
        public Vector2 WorldScreenRaw { get; } = worldScreenRaw;
        public Vector2 WorldScreenAbsolute { get; } = worldScreenAbsolute;
        public float Distance { get; } = distance;
    }

    internal readonly struct MechanicRank(bool ignored, int priorityIndex, float weightedDistance, float rawDistance, float cursorDistance)
    {
        public bool Ignored { get; } = ignored;
        public int PriorityIndex { get; } = priorityIndex;
        public float WeightedDistance { get; } = weightedDistance;
        public float RawDistance { get; } = rawDistance;
        public float CursorDistance { get; } = cursorDistance;
    }

    internal readonly struct MechanicPriorityContext(
        IReadOnlyDictionary<string, int> priorityIndexMap,
        IReadOnlySet<string> ignoreDistanceSet,
        IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
        int priorityDistancePenalty)
    {
        public IReadOnlyDictionary<string, int> PriorityIndexMap { get; } = priorityIndexMap;
        public IReadOnlySet<string> IgnoreDistanceSet { get; } = ignoreDistanceSet;
        public IReadOnlyDictionary<string, int> IgnoreDistanceWithinByMechanicId { get; } = ignoreDistanceWithinByMechanicId;
        public int PriorityDistancePenalty { get; } = priorityDistancePenalty;
    }

    internal readonly struct MechanicCandidateSignal(string? mechanicId, float? distance, float? cursorDistance)
    {
        public string? MechanicId { get; } = mechanicId;
        public float? Distance { get; } = distance;
        public float? CursorDistance { get; } = cursorDistance;
        public bool Exists => Distance.HasValue;

        public static MechanicCandidateSignal None => new(null, null, null);
    }
}