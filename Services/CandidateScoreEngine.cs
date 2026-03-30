namespace ClickIt.Services
{
    internal static class CandidateScoreEngine
    {
        internal readonly struct CandidateScoreContext
        {
            internal CandidateScoreContext(
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

            internal IReadOnlyDictionary<string, int> PriorityIndexMap { get; }
            internal IReadOnlySet<string> IgnoreDistanceSet { get; }
            internal IReadOnlyDictionary<string, int> IgnoreDistanceWithinByMechanicId { get; }
            internal int PriorityDistancePenalty { get; }
        }

        internal readonly struct CandidateScore
        {
            internal CandidateScore(bool ignored, int priorityIndex, float weightedDistance, float rawDistance, float cursorDistance)
            {
                Ignored = ignored;
                PriorityIndex = priorityIndex;
                WeightedDistance = weightedDistance;
                RawDistance = rawDistance;
                CursorDistance = cursorDistance;
            }

            internal bool Ignored { get; }
            internal int PriorityIndex { get; }
            internal float WeightedDistance { get; }
            internal float RawDistance { get; }
            internal float CursorDistance { get; }
        }

        internal static CandidateScore Build(float distance, string? mechanicId, float cursorDistance, in CandidateScoreContext context)
        {
            int priorityIndex = ResolvePriorityIndex(mechanicId, context.PriorityIndexMap);
            bool ignored = IsIgnoreDistanceActive(mechanicId, distance, context.IgnoreDistanceSet, context.IgnoreDistanceWithinByMechanicId);
            float weightedDistance = CalculateWeightedDistance(distance, priorityIndex, context.PriorityDistancePenalty);
            return new CandidateScore(ignored, priorityIndex, weightedDistance, distance, cursorDistance);
        }

        internal static int ResolvePriorityIndex(string? mechanicId, IReadOnlyDictionary<string, int> priorityIndexMap)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                return int.MaxValue;

            return priorityIndexMap.TryGetValue(mechanicId, out int index)
                ? index
                : int.MaxValue;
        }

        internal static bool IsIgnoreDistanceActive(
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

        internal static float CalculateWeightedDistance(float distance, int priorityIndex, int penalty)
            => priorityIndex == int.MaxValue
                ? float.MaxValue
                : distance + (priorityIndex * Math.Max(0, penalty));

        internal static int Compare(CandidateScore left, CandidateScore right)
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
    }
}