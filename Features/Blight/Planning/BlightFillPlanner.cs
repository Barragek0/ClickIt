using static ClickIt.Features.Blight.Planning.BlightBranches;

namespace ClickIt.Features.Blight.Planning;

internal static class BlightFillPlanner
{
    // Hard cap on the steps a single plan carries. Blight maps can have 100+ foundations, so the
    // fill tier alone would otherwise emit hundreds of steps and the executor would try to walk to
    // every one; 30 steps (≈7 fully-upgraded towers) is more than enough per batch — finishing a
    // batch triggers a rebuild that plans the next. Coverage steps come first, so the cap never
    // starves coverage.
    internal const int MaxPlanSteps = 30;

    internal static TowerBuildRule? FindRule(IReadOnlyList<TowerBuildRule> rules, BlightTowerType type)
    {
        for (int r = 0; r < rules.Count; r++)
            if (rules[r].TowerType == type)
                return rules[r];
        return null;
    }

    internal static int RequiredLevelForRadius(BlightTowerType type, float requiredRadiusSq, int maxLevel)
    {
        if (requiredRadiusSq <= 0f)
            return 1;
        for (int lvl = 1; lvl <= maxLevel; lvl++)
        {
            if (Sq(BlightService.GetCoverageRadiusForLevel(type, lvl)) >= requiredRadiusSq)
                return lvl;
        }
        return maxLevel;
    }

    internal static int CountBuilt(IReadOnlyList<BlightCachedTower> knownTowers, BlightTowerType type)
    {
        int count = 0;
        for (int i = 0; i < knownTowers.Count; i++)
            if (knownTowers[i].UpgradeLevel > 0 && knownTowers[i].TowerType == type)
                count++;
        return count;
    }

    internal static float DistanceToNearestAssignedTowerSq(
        IReadOnlyList<BlightCachedTower> knownTowers,
        HashSet<int> assignedIndices,
        int candidateIdx)
    {
        float best = float.MaxValue;
        NumVector2 p = knownTowers[candidateIdx].WorldPosition;
        foreach (int i in assignedIndices)
        {
            if (i == candidateIdx) continue;
            float d = (knownTowers[i].WorldPosition - p).LengthSquared();
            if (d < best) best = d;
        }
        return best;
    }

    internal static void AssignFill(
        List<TowerBuildRule> tierRules,
        IReadOnlyList<BlightCachedTower> knownTowers,
        LaneCoverageResult[] coverage,
        HashSet<NumVector2> failedPositions,
        HashSet<int> assignedIndices,
        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments,
        List<NumVector2> orderedFillPositions,
        NumVector2? pumpPosition,
        NumVector2? playerPosition)
    {
        int[] counts = new int[tierRules.Count];
        for (int r = 0; r < tierRules.Count; r++)
            counts[r] = CountBuilt(knownTowers, tierRules[r].TowerType);

        for (int r = 0; r < tierRules.Count; r++)
        {
            TowerBuildRule rule = tierRules[r];
            if (rule.Placement != BlightPlacementPreference.NearestUncoveredLane)
                continue;

            float maxRadiusSq = Sq(BlightService.GetCoverageRadiusForLevel(rule.TowerType, rule.MaxUpgradeLevel));
            while (rule.MaxBuildCount <= 0 || counts[r] < rule.MaxBuildCount)
            {
                (int fIdx, _) = FindFoundationNearUncoveredLane(
                    knownTowers, coverage, maxRadiusSq,
                    failedPositions, assignedIndices, pumpPosition, playerPosition);
                if (fIdx < 0)
                    break;
                assignments[knownTowers[fIdx].WorldPosition]
                    = (rule.TowerType, BlightTowerData.MaxUpgradeLevel);
                assignedIndices.Add(fIdx);
                orderedFillPositions.Add(knownTowers[fIdx].WorldPosition);
                counts[r]++;
            }
        }

        BlightPlacementPreference placement = tierRules.Count > 0
            ? tierRules[0].Placement
            : BlightPlacementPreference.Default;
        List<(int Index, float Metric)> candidates = [];
        for (int i = 0; i < knownTowers.Count; i++)
        {
            if (assignedIndices.Contains(i)) continue;
            if (failedPositions.Contains(knownTowers[i].WorldPosition)) continue;
            candidates.Add((i, FillPlacementMetric(knownTowers, assignedIndices, i, placement, pumpPosition, playerPosition)));
        }
        candidates.Sort((a, b) => a.Metric.CompareTo(b.Metric));

        foreach ((int index, float _) in candidates)
        {
            int ruleIdx = PickFillRule(tierRules, counts);
            if (ruleIdx < 0)
                break;
            if (tierRules[ruleIdx].Placement == BlightPlacementPreference.NearestUncoveredLane)
                break;

            TowerBuildRule rule = tierRules[ruleIdx];
            assignments[knownTowers[index].WorldPosition]
                = (rule.TowerType, BlightTowerData.MaxUpgradeLevel);
            assignedIndices.Add(index);
            orderedFillPositions.Add(knownTowers[index].WorldPosition);
            counts[ruleIdx]++;
        }
    }

    private static (int Index, float DistSq) FindFoundationNearUncoveredLane(
        IReadOnlyList<BlightCachedTower> knownTowers,
        LaneCoverageResult[] coverage,
        float radiusSq,
        HashSet<NumVector2> failedPositions,
        HashSet<int> assignedIndices,
        NumVector2? pumpPosition,
        NumVector2? playerPosition)
    {
        int bestIdx = -1;
        float bestMetric = float.MaxValue;
        float bestDistSq = float.MaxValue;

        for (int s = 0; s < coverage.Length; s++)
        {
            if (coverage[s].ParentIndex == BlightLaneTopology.OrphanSentinel)
                continue;
            if (coverage[s].IsPumpStub)
                continue;
            if (coverage[s].HasChilling || coverage[s].HasSeismic || coverage[s].HasFireball)
                continue;

            for (int i = 0; i < knownTowers.Count; i++)
            {
                if (assignedIndices.Contains(i)) continue;
                if (failedPositions.Contains(knownTowers[i].WorldPosition)) continue;

                float distSq = SqDist(knownTowers[i].WorldPosition, coverage[s].Midpoint);
                if (distSq > radiusSq)
                    continue;

                float metric = playerPosition.HasValue
                    ? (knownTowers[i].WorldPosition - playerPosition.Value).LengthSquared()
                    : pumpPosition.HasValue
                        ? (knownTowers[i].WorldPosition - pumpPosition.Value).LengthSquared()
                        : distSq;
                if (metric < bestMetric)
                {
                    bestMetric = metric;
                    bestDistSq = distSq;
                    bestIdx = i;
                }
            }
        }

        return (bestIdx, bestDistSq);
    }

    private static float FillPlacementMetric(
        IReadOnlyList<BlightCachedTower> knownTowers,
        HashSet<int> assignedIndices,
        int candidateIdx,
        BlightPlacementPreference placement,
        NumVector2? pumpPosition,
        NumVector2? playerPosition)
    {
        NumVector2 p = knownTowers[candidateIdx].WorldPosition;
        return placement switch
        {
            BlightPlacementPreference.NearestPump when pumpPosition.HasValue
                => (p - pumpPosition.Value).LengthSquared(),
            BlightPlacementPreference.NearestPlayer when playerPosition.HasValue
                => (p - playerPosition.Value).LengthSquared(),
            BlightPlacementPreference.NearExistingTowers
                => DistanceToNearestAssignedTowerSq(knownTowers, assignedIndices, candidateIdx),
            _ => candidateIdx,
        };
    }

    internal static List<BlightPlanStep> BuildOrderedSteps(
        IReadOnlyList<BlightCachedTower> knownTowers,
        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments,
        IReadOnlyList<TowerBuildRule> rules,
        List<BlightPlanner.CoveragePlacement> coveragePlacements,
        List<NumVector2> orderedFillPositions)
    {
        Dictionary<BlightTowerType, TowerBuildRule> ruleByType = [];
        for (int r = 0; r < rules.Count; r++)
            ruleByType[rules[r].TowerType] = rules[r];

        List<BlightPlanStep> steps = [];

        foreach (BlightPlanner.CoveragePlacement p in coveragePlacements)
        {
            BlightCachedTower t = knownTowers[p.KnownTowerIndex];
            if (t.UpgradeLevel > 0)
                continue;
            steps.Add(new BlightPlanStep(BlightPlanAction.Build, t.WorldPosition, p.Type, 1));
        }

        foreach (BlightPlanner.CoveragePlacement p in coveragePlacements)
        {
            if (!ruleByType.TryGetValue(p.Type, out TowerBuildRule rule))
                continue;

            BlightCachedTower t = knownTowers[p.KnownTowerIndex];
            int finalLevel = rule.UpgradePolicy == TowerUpgradePolicy.UpgradeToMax
                ? rule.MaxUpgradeLevel
                : rule.UpgradeBeforeMovingOntoLowerPriority
                    ? rule.MaxUpgradeLevel
                    : RequiredLevelForRadius(p.Type, p.RequiredRadiusSq, rule.MaxUpgradeLevel);

            // A new build is at level 1, so upgrades start at level 2 — never re-emit an "upgrade to 1" step.
            int startLevel = SystemMath.Max(1, t.UpgradeLevel) + 1;
            for (int lvl = startLevel; lvl <= finalLevel; lvl++)
                steps.Add(new BlightPlanStep(BlightPlanAction.Upgrade, t.WorldPosition, p.Type, lvl));
        }

        List<(NumVector2 Pos, BlightTowerType Type, int MaxLevel, int CurrentLevel, bool AlwaysUpgradeBeforeBuildingNew)> fillEntries = [];
        for (int f = 0; f < orderedFillPositions.Count; f++)
        {
            NumVector2 pos = orderedFillPositions[f];
            if (!assignments.TryGetValue(pos, out (BlightTowerType Type, int MaxLevel) assigned))
                continue;
            if (!ruleByType.TryGetValue(assigned.Type, out TowerBuildRule rule))
                continue;
            if (rule.IsCoverageTower)
                continue;

            int currentLevel = BlightHelpers.FindTowerAt(knownTowers, pos)?.UpgradeLevel ?? 0;
            fillEntries.Add((pos, assigned.Type, assigned.MaxLevel, currentLevel, rule.AlwaysUpgradeBeforeBuildingNew));
        }

        foreach ((NumVector2 Pos, BlightTowerType Type, int MaxLevel, int CurrentLevel, bool AlwaysUpgradeBeforeBuildingNew) e in fillEntries)
        {
            if (!e.AlwaysUpgradeBeforeBuildingNew)
                continue;
            if (e.CurrentLevel == 0)
                continue;
            for (int lvl = e.CurrentLevel + 1; lvl <= e.MaxLevel; lvl++)
                steps.Add(new BlightPlanStep(BlightPlanAction.Upgrade, e.Pos, e.Type, lvl));
        }

        foreach ((NumVector2 Pos, BlightTowerType Type, int MaxLevel, int CurrentLevel, bool AlwaysUpgradeBeforeBuildingNew) e in fillEntries)
        {
            if (!e.AlwaysUpgradeBeforeBuildingNew)
                continue;
            if (e.CurrentLevel > 0)
                continue; // already built — handled in Phase A
            steps.Add(new BlightPlanStep(BlightPlanAction.Build, e.Pos, e.Type, 1));
            for (int lvl = 2; lvl <= e.MaxLevel; lvl++)
                steps.Add(new BlightPlanStep(BlightPlanAction.Upgrade, e.Pos, e.Type, lvl));
        }

        // Default — all builds first, then all upgrades.
        foreach ((NumVector2 Pos, BlightTowerType Type, int MaxLevel, int CurrentLevel, bool AlwaysUpgradeBeforeBuildingNew) e in fillEntries)
        {
            if (e.AlwaysUpgradeBeforeBuildingNew)
                continue;
            if (e.CurrentLevel == 0)
                steps.Add(new BlightPlanStep(BlightPlanAction.Build, e.Pos, e.Type, 1));
        }
        foreach ((NumVector2 Pos, BlightTowerType Type, int MaxLevel, int CurrentLevel, bool AlwaysUpgradeBeforeBuildingNew) e in fillEntries)
        {
            if (e.AlwaysUpgradeBeforeBuildingNew)
                continue;
            for (int lvl = e.CurrentLevel + 1; lvl <= e.MaxLevel; lvl++)
                steps.Add(new BlightPlanStep(BlightPlanAction.Upgrade, e.Pos, e.Type, lvl));
        }

        if (steps.Count > MaxPlanSteps)
            steps.RemoveRange(MaxPlanSteps, steps.Count - MaxPlanSteps);

        return steps;
    }

    private static int PickFillRule(List<TowerBuildRule> tierRules, int[] counts)
    {
        int bestIdx = -1;
        int bestCount = int.MaxValue;
        for (int r = 0; r < tierRules.Count; r++)
        {
            if (tierRules[r].MaxBuildCount > 0 && counts[r] >= tierRules[r].MaxBuildCount)
                continue; // capped
            if (counts[r] < bestCount)
            {
                bestCount = counts[r];
                bestIdx = r;
            }
        }
        return bestIdx;
    }
}
