namespace ClickIt.Features.Mechanics
{
    internal readonly record struct MechanicPrioritySnapshot(
        IReadOnlyDictionary<string, int> PriorityIndexMap,
        IReadOnlySet<string> IgnoreDistanceSet,
        IReadOnlyDictionary<string, int> IgnoreDistanceWithinByMechanicId);

    internal sealed class MechanicPrioritySnapshotService : IMechanicPrioritySnapshotProvider
    {
        private static readonly IReadOnlyDictionary<string, int> EmptyPriorityMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly IReadOnlySet<string> EmptyIgnoreDistanceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly IReadOnlyDictionary<string, int> EmptyIgnoreDistanceWithinMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<string>? _cachedPriorityOrder;
        private IReadOnlyCollection<string>? _cachedIgnoreDistanceIds;
        private IReadOnlyDictionary<string, int>? _cachedIgnoreDistanceWithinById;

        private IReadOnlyDictionary<string, int> _priorityIndexMap = EmptyPriorityMap;
        private IReadOnlySet<string> _ignoreDistanceSet = EmptyIgnoreDistanceSet;
        private IReadOnlyDictionary<string, int> _ignoreDistanceWithinMap = EmptyIgnoreDistanceWithinMap;

        internal MechanicPrioritySnapshot Refresh(
            IReadOnlyList<string> mechanicPriorities,
            IReadOnlyCollection<string> ignoreDistance,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
        {
            if (!ReferenceEquals(_cachedPriorityOrder, mechanicPriorities))
            {
                _cachedPriorityOrder = mechanicPriorities;
                _priorityIndexMap = BuildPriorityIndexMap(mechanicPriorities);
            }

            if (!ReferenceEquals(_cachedIgnoreDistanceIds, ignoreDistance))
            {
                _cachedIgnoreDistanceIds = ignoreDistance;
                _ignoreDistanceSet = new HashSet<string>(ignoreDistance, StringComparer.OrdinalIgnoreCase);
            }

            if (!ReferenceEquals(_cachedIgnoreDistanceWithinById, ignoreDistanceWithinByMechanicId))
            {
                _cachedIgnoreDistanceWithinById = ignoreDistanceWithinByMechanicId;
                _ignoreDistanceWithinMap = new Dictionary<string, int>(ignoreDistanceWithinByMechanicId, StringComparer.OrdinalIgnoreCase);
            }

            return Snapshot;
        }

        internal MechanicPrioritySnapshot Snapshot
            => new(_priorityIndexMap, _ignoreDistanceSet, _ignoreDistanceWithinMap);

        MechanicPrioritySnapshot IMechanicPrioritySnapshotProvider.Refresh(
            IReadOnlyList<string> mechanicPriorities,
            IReadOnlyCollection<string> ignoreDistance,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
            => Refresh(mechanicPriorities, ignoreDistance, ignoreDistanceWithinByMechanicId);

        MechanicPrioritySnapshot IMechanicPrioritySnapshotProvider.Snapshot
            => Snapshot;

        private static IReadOnlyDictionary<string, int> BuildPriorityIndexMap(IReadOnlyList<string> priorities)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < priorities.Count; i++)
            {
                string id = priorities[i] ?? string.Empty;
                if (id.Length > 0)
                    map.TryAdd(id, i);
            }

            return map;
        }
    }
}