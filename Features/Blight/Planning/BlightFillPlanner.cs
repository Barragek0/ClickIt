using static ClickIt.Features.Blight.Planning.BlightBranches;

namespace ClickIt.Features.Blight.Planning;

internal static class BlightFillPlanner
{
    // Hard cap on the steps a single plan carries (coverage steps come first, so the cap never starves coverage).
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
                    = (rule.TowerType, rule.MaxUpgradeLevel);
                assignedIndices.Add(fIdx);
                orderedFillPositions.Add(knownTowers[fIdx].WorldPosition);
                counts[r]++;
            }
        }

        // Order fill candidates by the first non-default placement among the tier rules. Empowering
        // rules place via FindBestEmpowerFoundation, so the candidate order is driven by the
        // direct-build rules (e.g. the fill Scout rule's NearestPump), which can differ from
        // tierRules[0].
        BlightPlacementPreference placement = BlightPlacementPreference.Default;
        for (int r = 0; r < tierRules.Count && placement == BlightPlacementPreference.Default; r++)
            placement = tierRules[r].Placement;
        List<(int Index, float Metric)> candidates = [];
        for (int i = 0; i < knownTowers.Count; i++)
        {
            if (assignedIndices.Contains(i)) continue;
            if (failedPositions.Contains(knownTowers[i].WorldPosition)) continue;
            candidates.Add((i, PlacementMetric(knownTowers, assignedIndices, i, i, placement, pumpPosition)));
        }
        candidates.Sort((a, b) => a.Metric.CompareTo(b.Metric));

        bool[] ruleDone = new bool[tierRules.Count];

        int c = 0;
        while (c < candidates.Count)
        {
            int index = candidates[c].Index;
            if (assignedIndices.Contains(index)) { c++; continue; }

            int ruleIdx = PickFillRule(tierRules, counts, ruleDone);
            if (ruleIdx < 0)
                break;
            if (tierRules[ruleIdx].Placement == BlightPlacementPreference.NearestUncoveredLane)
            {
                // All NearestUncoveredLane rules are fully placed by the earlier loop; reaching one
                // here means its foundations are exhausted. Mark it done so the OTHER rules in the
                // tier keep placing instead of breaking the whole assignment loop.
                ruleDone[ruleIdx] = true;
                continue;
            }

            TowerBuildRule rule = tierRules[ruleIdx];

            if (rule.EmpowerTargets.Count > 0)
            {
                int empowerIdx = FindBestEmpowerFoundation(
                    rule, knownTowers, assignments,
                    assignedIndices, failedPositions, pumpPosition);
                if (empowerIdx < 0)
                {
                    ruleDone[ruleIdx] = true;
                    continue; // reuse this candidate for the next rule
                }
                assignments[knownTowers[empowerIdx].WorldPosition]
                    = (rule.TowerType, rule.MaxUpgradeLevel);
                assignedIndices.Add(empowerIdx);
                orderedFillPositions.Add(knownTowers[empowerIdx].WorldPosition);
                counts[ruleIdx]++;
                continue; // do not consume this candidate — the empowering tower may sit elsewhere
            }

            assignments[knownTowers[index].WorldPosition]
                = (rule.TowerType, rule.MaxUpgradeLevel);
            assignedIndices.Add(index);
            orderedFillPositions.Add(knownTowers[index].WorldPosition);
            counts[ruleIdx]++;
            c++;
        }
    }

    private static int FindBestEmpowerFoundation(
        TowerBuildRule rule,
        IReadOnlyList<BlightCachedTower> knownTowers,
        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments,
        HashSet<int> assignedIndices,
        HashSet<NumVector2> failedPositions,
        NumVector2? pumpPosition)
    {
        // Target towers: built or planned towers of the types this rule must empower.
        List<(NumVector2 Pos, BlightTowerType Type)> targets = [];
        for (int i = 0; i < knownTowers.Count; i++)
        {
            BlightCachedTower t = knownTowers[i];
            bool built = t.UpgradeLevel > 0;
            BlightTowerType? planned = assignments.TryGetValue(t.WorldPosition, out (BlightTowerType Type, int MaxLevel) a) ? a.Type : null;
            BlightTowerType? type = built ? t.TowerType : planned;
            if (type is BlightTowerType tt && rule.EmpowerTargets.Contains(tt))
                targets.Add((t.WorldPosition, tt));
        }
        if (targets.Count == 0)
            return -1;

        float radiusSq = Sq(BlightService.GetCoverageRadiusForLevel(rule.TowerType, rule.MaxUpgradeLevel));

        // Which targets are already within range of a built or planned Empowering tower?
        bool[] empowered = new bool[targets.Count];
        for (int i = 0; i < knownTowers.Count; i++)
        {
            BlightCachedTower t = knownTowers[i];
            bool isEmpowering = t.UpgradeLevel > 0
                ? t.TowerType == BlightTowerType.Empowering
                : (assignments.TryGetValue(t.WorldPosition, out (BlightTowerType Type, int MaxLevel) ae)
                    && ae.Type == BlightTowerType.Empowering);
            if (!isEmpowering) continue;
            for (int k = 0; k < targets.Count; k++)
                if (!empowered[k] && SqDist(t.WorldPosition, targets[k].Pos) <= radiusSq)
                    empowered[k] = true;
        }

        // Stop once every target tower is already empowered.
        bool allEmpowered = true;
        for (int k = 0; k < targets.Count; k++)
            if (!empowered[k]) { allEmpowered = false; break; }
        if (allEmpowered)
            return -1;

        int targetTypeMask = 0;
        for (int r = 0; r < rule.EmpowerTargets.Count; r++)
            targetTypeMask |= 1 << (int)rule.EmpowerTargets[r];

        int bestIdx = -1;
        int bestNewly = 0;
        bool bestAllTypes = false;
        float bestMetric = float.MaxValue;
        for (int i = 0; i < knownTowers.Count; i++)
        {
            if (assignedIndices.Contains(i)) continue;
            if (failedPositions.Contains(knownTowers[i].WorldPosition)) continue;
            NumVector2 pos = knownTowers[i].WorldPosition;

            int newly = 0;
            int coveredMask = 0;
            for (int k = 0; k < targets.Count; k++)
            {
                if (empowered[k]) continue;
                if (SqDist(pos, targets[k].Pos) > radiusSq) continue;
                newly++;
                coveredMask |= 1 << (int)targets[k].Type;
            }
            if (newly == 0) continue; // must be in range of at least one un-empowered target

            bool allTypes = (coveredMask & targetTypeMask) == targetTypeMask;
            float metric = PlacementMetric(knownTowers, assignedIndices, i, 0f, rule.Placement, pumpPosition);
            if (bestIdx < 0
                || newly > bestNewly
                || (newly == bestNewly && allTypes && !bestAllTypes)
                || (newly == bestNewly && allTypes == bestAllTypes && metric < bestMetric))
            {
                bestIdx = i;
                bestNewly = newly;
                bestAllTypes = allTypes;
                bestMetric = metric;
            }
        }
        return bestIdx;
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

    internal static List<BlightPlanStep> BuildOrderedSteps(
        IReadOnlyList<BlightCachedTower> knownTowers,
        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments,
        IReadOnlyList<TowerBuildRule> rules,
        List<BlightPlanner.CoveragePlacement> coveragePlacements,
        List<NumVector2> orderedFillPositions)
    {
        // A strategy may declare a coverage rule AND a fill rule for the same tower type (e.g.
        // coverage scouts + fill scouts), so resolve each step against the rule matching its role.
        Dictionary<BlightTowerType, TowerBuildRule> coverageRuleByType = [];
        Dictionary<BlightTowerType, TowerBuildRule> fillRuleByType = [];
        for (int r = 0; r < rules.Count; r++)
        {
            TowerBuildRule rule = rules[r];
            if (rule.IsCoverageTower)
                coverageRuleByType[rule.TowerType] = rule;
            else
                fillRuleByType[rule.TowerType] = rule;
        }

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
            if (!coverageRuleByType.TryGetValue(p.Type, out TowerBuildRule rule))
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
            if (!fillRuleByType.TryGetValue(assigned.Type, out TowerBuildRule rule))
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

    private static int PickFillRule(List<TowerBuildRule> tierRules, int[] counts, bool[]? ruleDone = null)
    {
        int bestIdx = -1;
        int bestCount = int.MaxValue;
        for (int r = 0; r < tierRules.Count; r++)
        {
            if (ruleDone != null && ruleDone[r])
                continue; // exhausted — e.g. an Empowering rule with no more valid foundations
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
