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
                HashSet<string> expandedIgnoreDistanceSet = new(ignoreDistance, StringComparer.OrdinalIgnoreCase);
                ExpandGroupIgnoreDistanceAliases(expandedIgnoreDistanceSet);
                _ignoreDistanceSet = expandedIgnoreDistanceSet;
            }

            if (!ReferenceEquals(_cachedIgnoreDistanceWithinById, ignoreDistanceWithinByMechanicId))
            {
                _cachedIgnoreDistanceWithinById = ignoreDistanceWithinByMechanicId;
                Dictionary<string, int> expandedIgnoreDistanceWithinMap = new(ignoreDistanceWithinByMechanicId, StringComparer.OrdinalIgnoreCase);
                ExpandGroupIgnoreDistanceWithinAliases(expandedIgnoreDistanceWithinMap);
                _ignoreDistanceWithinMap = expandedIgnoreDistanceWithinMap;
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

        private static Dictionary<string, int> BuildPriorityIndexMap(IReadOnlyList<string> priorities)
        {
            Dictionary<string, int> map = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < priorities.Count; i++)
            {
                string id = priorities[i] ?? string.Empty;
                if (id.Length > 0)
                    map.TryAdd(id, i);
            }

            AddPriorityAliasesFromGroups(map);

            return map;
        }

        private static void ExpandGroupIgnoreDistanceAliases(HashSet<string> ignoreDistanceSet)
        {
            AddIgnoreDistanceAliasesFromGroups(ignoreDistanceSet);
        }

        private static void ExpandGroupIgnoreDistanceWithinAliases(Dictionary<string, int> ignoreDistanceWithinMap)
        {
            AddIgnoreDistanceWithinAliasesFromGroups(ignoreDistanceWithinMap);
        }

        private static readonly (string Group, string Specific)[] GroupAliases =
        [
            (MechanicIds.LeagueChests, MechanicIds.MirageGoldenDjinnCache),
            (MechanicIds.LeagueChests, MechanicIds.MirageSilverDjinnCache),
            (MechanicIds.LeagueChests, MechanicIds.MirageBronzeDjinnCache),
            (MechanicIds.LeagueChests, MechanicIds.HeistSecureLocker),
            (MechanicIds.LeagueChests, MechanicIds.HeistSecureRepository),
            (MechanicIds.LeagueChests, MechanicIds.HeistHazards),
            (MechanicIds.LeagueChests, MechanicIds.BlightCyst),
            (MechanicIds.LeagueChests, MechanicIds.BreachGraspingCoffers),
            (MechanicIds.LeagueChests, MechanicIds.SynthesisSynthesisedStash),
            (MechanicIds.Doors, MechanicIds.HeistDoors),
            (MechanicIds.Doors, MechanicIds.AlvaTempleDoors),
        ];

        private static void AddPriorityAliasesFromGroups(Dictionary<string, int> map)
        {
            foreach ((string group, string specific) in GroupAliases)
            {
                if (map.ContainsKey(specific) || !map.TryGetValue(group, out int groupIndex))
                    continue;
                map[specific] = groupIndex;
            }
        }

        private static void AddIgnoreDistanceAliasesFromGroups(HashSet<string> ignoreDistanceSet)
        {
            foreach ((string group, string specific) in GroupAliases)
            {
                if (ignoreDistanceSet.Contains(group))
                    ignoreDistanceSet.Add(specific);
            }
        }

        private static void AddIgnoreDistanceWithinAliasesFromGroups(Dictionary<string, int> ignoreDistanceWithinMap)
        {
            foreach ((string group, string specific) in GroupAliases)
            {
                if (ignoreDistanceWithinMap.ContainsKey(specific) || !ignoreDistanceWithinMap.TryGetValue(group, out int groupValue))
                    continue;
                ignoreDistanceWithinMap[specific] = groupValue;
            }
        }
    }
}