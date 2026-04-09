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

            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.MirageGoldenDjinnCache);
            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.MirageSilverDjinnCache);
            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.MirageBronzeDjinnCache);
            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.HeistSecureLocker);
            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.HeistSecureRepository);
            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.HeistHazards);
            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.BlightCyst);
            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.BreachGraspingCoffers);
            AddPriorityAliasFromGroup(map, MechanicIds.LeagueChests, MechanicIds.SynthesisSynthesisedStash);
            AddPriorityAliasFromGroup(map, MechanicIds.Doors, MechanicIds.HeistDoors);
            AddPriorityAliasFromGroup(map, MechanicIds.Doors, MechanicIds.AlvaTempleDoors);

            return map;
        }

        private static void AddPriorityAliasFromGroup(Dictionary<string, int> map, string groupId, string specificId)
        {
            if (map.ContainsKey(specificId))
                return;

            if (map.TryGetValue(groupId, out int groupIndex))
                map[specificId] = groupIndex;
        }

        private static void ExpandGroupIgnoreDistanceAliases(HashSet<string> ignoreDistanceSet)
        {
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.MirageGoldenDjinnCache);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.MirageSilverDjinnCache);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.MirageBronzeDjinnCache);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.HeistSecureLocker);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.HeistSecureRepository);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.HeistHazards);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.BlightCyst);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.BreachGraspingCoffers);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.LeagueChests, MechanicIds.SynthesisSynthesisedStash);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.Doors, MechanicIds.HeistDoors);
            AddIgnoreDistanceAliasFromGroup(ignoreDistanceSet, MechanicIds.Doors, MechanicIds.AlvaTempleDoors);
        }

        private static void ExpandGroupIgnoreDistanceWithinAliases(Dictionary<string, int> ignoreDistanceWithinMap)
        {
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.MirageGoldenDjinnCache);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.MirageSilverDjinnCache);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.MirageBronzeDjinnCache);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.HeistSecureLocker);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.HeistSecureRepository);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.HeistHazards);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.BlightCyst);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.BreachGraspingCoffers);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.LeagueChests, MechanicIds.SynthesisSynthesisedStash);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.Doors, MechanicIds.HeistDoors);
            AddIgnoreDistanceWithinAliasFromGroup(ignoreDistanceWithinMap, MechanicIds.Doors, MechanicIds.AlvaTempleDoors);
        }

        private static void AddIgnoreDistanceAliasFromGroup(HashSet<string> ignoreDistanceSet, string groupId, string specificId)
        {
            if (!ignoreDistanceSet.Contains(groupId))
                return;

            ignoreDistanceSet.Add(specificId);
        }

        private static void AddIgnoreDistanceWithinAliasFromGroup(Dictionary<string, int> ignoreDistanceWithinMap, string groupId, string specificId)
        {
            if (ignoreDistanceWithinMap.ContainsKey(specificId))
                return;

            if (ignoreDistanceWithinMap.TryGetValue(groupId, out int groupValue))
                ignoreDistanceWithinMap[specificId] = groupValue;
        }
    }
}